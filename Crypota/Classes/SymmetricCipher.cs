using System.Collections.Concurrent;
using System.Numerics;

namespace Crypota.Classes;

using static CryptoAlgorithms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public enum CipherMode { ECB, CBC, PCBC, CFB, OFB, CTR, RD }
public enum PaddingMode { Zeros, ANSIX923, PKCS7, ISO10126 }

public class SymmetricCipher : ISymmetricCipher
{

    private List<byte>? _key;
    public int BlockSize { get; set; } = 8;
    public int KeySize { get; set; } = 8;

    private readonly List<byte>? _iv;
    private readonly CipherMode _cipherMode;
    private readonly PaddingMode _paddingMode;
    private readonly ISymmetricCipher _implementation;
    private readonly Dictionary<string, object> _params;

    public class RandomDeltaParameters
    {
        public int Delta { get; set; }
    }

    public T GetParam<T>() where T : class
    {
        string key = typeof(T).Name;
        if (_params.TryGetValue(key, out object? value))
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value), "Needed parameter not supposed to be null.");
            }

            return (T)value;
        }

        throw new KeyNotFoundException($"Параметр типа {key} не найден.");
    }

    public List<byte>? Key
    {
        get => _key;
        set
        {
            _key = value;
            _implementation.Key = value;
        }
    }


    public SymmetricCipher(
        List<byte> key,
        CipherMode mode,
        PaddingMode padding,
        ISymmetricCipher implementation, // added
        List<byte>? iv = null,
        params object[] additionalParams // for RD need delta only 
    )
    {
        _cipherMode = mode;
        _paddingMode = padding;
        _iv = iv ?? null;
        _implementation = implementation;
        Key = key;
        _params = additionalParams.ToDictionary(p => p.GetType().Name);
    }


    public List<byte> EncryptBlock(List<byte> block) => _implementation.EncryptBlock(block);

    public List<byte> DecryptBlock(List<byte> block) => _implementation.DecryptBlock(block);


    private List<byte> ApplyPadding(List<byte> block)
    {
        int n = BlockSize - block.Count;
        switch (_paddingMode)
        {
            case PaddingMode.Zeros:
                if (block.Count != 0)
                {
                    block.AddRange(Enumerable.Repeat((byte)0x00, n));
                }

                break;

            case PaddingMode.ANSIX923:
                block.AddRange(Enumerable.Range(1, n - 1).Select(_ => (byte)0x00));
                block.Add((byte)n);
                break;

            case PaddingMode.PKCS7:
                block.AddRange(Enumerable.Range(1, n).Select(_ => (byte)n));
                break;

            case PaddingMode.ISO10126:
                var r1 = new Random();
                block.AddRange(Enumerable.Range(1, n - 1).Select(_ => (byte)r1.Next(0, 256)));
                block.Add((byte)n);
                break;
            
        }

        return block;
    }


    private List<byte> RemovePadding(List<byte> block)
    {
        if (block.Count == 0 || block.Count % BlockSize != 0)
            throw new ArgumentException("Invalid block size or padding");

        int paddingSize;
        byte lastByte = block[^1];

        switch (_paddingMode)
        {
            case PaddingMode.Zeros:
                paddingSize = block.FindLastIndex(b => b != 0x00) + 1;
                block.RemoveRange(paddingSize, block.Count - paddingSize);
                break;

            case PaddingMode.ANSIX923:
                paddingSize = lastByte;
                if (paddingSize == 0 || paddingSize > BlockSize)
                    throw new ArgumentException("Invalid ANSIX923 padding");

                for (int i = block.Count - paddingSize; i < block.Count - 1; i++)
                {
                    if (block[i] != 0x00)
                        throw new ArgumentException("Invalid ANSIX923 padding");
                }

                block.RemoveRange(block.Count - paddingSize, paddingSize);
                break;

            case PaddingMode.PKCS7:
                paddingSize = lastByte;
                if (paddingSize == 0 || paddingSize > BlockSize)
                    throw new ArgumentException("Invalid PKCS7 padding");

                for (int i = block.Count - paddingSize; i < block.Count; i++)
                {
                    if (block[i] != paddingSize)
                        throw new ArgumentException("Invalid PKCS7 padding");
                }

                block.RemoveRange(block.Count - paddingSize, paddingSize);
                break;

            case PaddingMode.ISO10126:
                paddingSize = lastByte;
                if (paddingSize == 0 || paddingSize > BlockSize)
                    throw new ArgumentException("Invalid ISO10126 padding");

                block.RemoveRange(block.Count - paddingSize, paddingSize);
                break;
            

            default:
                throw new NotSupportedException($"Padding mode {_paddingMode} is not supported");
        }

        return block;
    }

    public async Task<List<byte>> EncryptMessageAsync(List<byte> message)
    {
        List<List<byte>> blocks = new List<List<byte>>();
        List<byte> block = new List<byte>(BlockSize);

        for (int i = 0; i < message.Count; i++)
        {
            block.Add(message[i]);
            if (block.Count == BlockSize)
            {
                blocks.Add(new List<byte>(block));
                block.Clear();
            }
        }

        List<byte> last = ApplyPadding(block);
        if (last.Count > 0) blocks.Add(last);


        List<byte> result = new List<byte>();
        List<byte>? prev;

        byte[] ivBytes;
        BigInteger initialCounter;

        switch (_cipherMode)
        {
            case CipherMode.ECB:
                IEnumerable<Task<List<byte>>> tasks = blocks.Select(b => Task.Run(() => EncryptBlock(b)));
                List<byte>[] encryptedBlocks = await Task.WhenAll(tasks);
                result.AddRange(encryptedBlocks.SelectMany(b => b));
                break;

            case CipherMode.CBC:
                if (_iv == null)
                    throw new ArgumentNullException(nameof(_iv), "Setup before Encryption or change cipher mode");

                prev = EncryptBlock(XorTwoParts(blocks[0], _iv));
                result.AddRange(prev);
                foreach (var b in blocks.Skip(1))
                {
                    var encryptedBlock = EncryptBlock(XorTwoParts(b, prev));
                    result.AddRange(encryptedBlock);
                    prev = encryptedBlock;
                }

                break;


            case CipherMode.OFB:
                if (_iv == null)
                    throw new ArgumentNullException(nameof(_iv), "Setup before Encryption or change cipher mode");

                prev = EncryptBlock(_iv);
                result.AddRange(XorTwoParts(prev, blocks[0]));
                foreach (var b in blocks.Skip(1))
                {
                    var encryptedBlock = EncryptBlock(prev);
                    result.AddRange(XorTwoParts(encryptedBlock, b));
                    prev = encryptedBlock;
                }

                break;

            case CipherMode.CFB:
                if (_iv == null)
                    throw new ArgumentNullException(nameof(_iv), "Setup before Encryption or change cipher mode");

                prev = XorTwoParts(EncryptBlock(_iv), blocks[0]);
                result.AddRange(prev);
                foreach (var b in blocks.Skip(1))
                {
                    var encryptedBlock = XorTwoParts(EncryptBlock(prev), b);
                    result.AddRange(encryptedBlock);
                    prev = encryptedBlock;
                }

                break;

            case CipherMode.PCBC:
                if (_iv == null)
                    throw new ArgumentNullException(nameof(_iv), "Setup before Encryption or change cipher mode");

                prev = EncryptBlock(XorTwoParts(blocks[0], _iv));
                result.AddRange(prev);
                for (int i = 1; i < blocks.Count; ++i)
                {
                    var encryptedBlock = EncryptBlock(XorTwoParts(XorTwoParts(blocks[i - 1], prev), blocks[i]));
                    result.AddRange(encryptedBlock);
                    prev = encryptedBlock;
                }

                break;

            case CipherMode.CTR:
            case CipherMode.RD:
                if (_iv == null)
                    throw new ArgumentNullException(nameof(_iv), "Setup before Encryption or change cipher mode");

                // Common initialization
                _iv.Insert(0, 0x00);
                ivBytes = _iv.ToArray();
    
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(ivBytes);

                initialCounter = new BigInteger(ivBytes);
    
                // RD-specific parameters
                int delta = (_cipherMode == CipherMode.RD) ? GetParam<RandomDeltaParameters>().Delta : 1;
                
                var encryptedBlocksDict = new ConcurrentDictionary<int, List<byte>>();
    
                Parallel.ForEach(blocks.Select((b, i) => (Block: b, Index: i)), 
                     (item, ct) =>
                    {
                        BigInteger offset = item.Index * delta;
                        
                        BigInteger blockCounter = initialCounter + offset;
                        byte[] counterBytes = blockCounter.ToByteArray();
                        
                        if (BitConverter.IsLittleEndian)
                            Array.Reverse(counterBytes);
        
                        Array.Resize(ref counterBytes, BlockSize);
                        
                        var encryptedCounter = EncryptBlock(new List<byte>(counterBytes));
                        
                        var encryptedBlock = XorTwoParts(encryptedCounter, item.Block);
                        
                        encryptedBlocksDict[item.Index] = encryptedBlock;
                    });
                
                result.AddRange(encryptedBlocksDict
                    .OrderBy(kv => kv.Key)
                    .SelectMany(kv => kv.Value));
                break;
        }
        
        return result;
    }

    public async Task<List<byte>> DecryptMessageAsync(List<byte> ciphertext)
    {
        List<List<byte>> blocks = new List<List<byte>>();
        for (int i = 0; i < ciphertext.Count; i += BlockSize)
        {
            int remaining = Math.Min(BlockSize, ciphertext.Count - i);
            blocks.Add(ciphertext.GetRange(i, remaining));
            if (remaining % BlockSize != 0)
            {
                throw new ArgumentException("Blocks can't be decrypted because size not equal by mod with BlockSize");
            }
        }

        List<byte> result = new List<byte>();
        List<byte>? prev;

        byte[] ivBytes;
        BigInteger initialCounter;

        switch (_cipherMode)
        {
            case CipherMode.ECB:
                var tasks = blocks.Select(b => Task.Run(() => DecryptBlock(b)));
                List<byte>[] decryptedBlocks = await Task.WhenAll(tasks);
                result.AddRange(decryptedBlocks.SelectMany(b => b));
                break;

            case CipherMode.CBC:
                if (_iv == null) throw new ArgumentNullException(nameof(_iv));

                prev = _iv;
                foreach (var b in blocks)
                {
                    var decrypted = DecryptBlock(b);
                    var plain = XorTwoParts(decrypted, prev);
                    result.AddRange(plain);
                    prev = b;
                }

                break;

            case CipherMode.OFB:
                if (_iv == null) throw new ArgumentNullException(nameof(_iv));

                prev = EncryptBlock(_iv);
                result.AddRange(XorTwoParts(prev, blocks[0]));
                foreach (var b in blocks.Skip(1))
                {
                    prev = EncryptBlock(prev);
                    result.AddRange(XorTwoParts(prev, b));
                }

                break;

            case CipherMode.CFB:
                if (_iv == null) throw new ArgumentNullException(nameof(_iv));

                prev = _iv;
                foreach (var b in blocks)
                {
                    var encrypted = EncryptBlock(prev);
                    var plain = XorTwoParts(encrypted, b);
                    result.AddRange(plain);
                    prev = b;
                }

                break;

            case CipherMode.PCBC:
                if (_iv == null) throw new ArgumentNullException(nameof(_iv));
                
                var firstBlock = DecryptBlock(blocks[0]);
                var prevPlain = XorTwoParts(firstBlock, _iv);
                result.AddRange(prevPlain);

                for (int i = 1; i < blocks.Count; i++)
                {
                    var decrypted = DecryptBlock(blocks[i]);
                    var plain = XorTwoParts(decrypted, XorTwoParts(blocks[i - 1], prevPlain));
                    result.AddRange(plain);
                    prevPlain = plain;
                }

                break;

            case CipherMode.CTR:
            case CipherMode.RD:
                if (_iv == null) throw new ArgumentNullException(nameof(_iv));
                
                _iv.Insert(0, 0x00);
                ivBytes = _iv.ToArray();
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(ivBytes);

                initialCounter = new BigInteger(ivBytes);
                int delta = _cipherMode == CipherMode.RD ? GetParam<RandomDeltaParameters>().Delta : 1;

                var decryptedBlocksDict = new ConcurrentDictionary<int, List<byte>>();

                Parallel.ForEach(blocks, (block, ct) =>
                {
                    int blockIndex = blocks.IndexOf(block);
                    BigInteger offset = blockIndex * delta;

                    byte[] counterBytes = (initialCounter + offset).ToByteArray();
                    if (BitConverter.IsLittleEndian)
                        Array.Reverse(counterBytes);

                    Array.Resize(ref counterBytes, BlockSize);

                    var encryptedCounter = EncryptBlock(new List<byte> (counterBytes));
                    var decryptedBlock = XorTwoParts(encryptedCounter, block);
                    decryptedBlocksDict[blockIndex] = decryptedBlock;
                });
                
                result.AddRange(decryptedBlocksDict
                    .OrderBy(kv => kv.Key)
                    .SelectMany(kv => kv.Value));
                break;

            default:
                throw new NotSupportedException($"Mode {_cipherMode} not supported");
        }
        
        
        if (result.Count > 0)
        {
            int totalBlocks = result.Count / BlockSize;
            int lastBlockStart = (totalBlocks - 1) * BlockSize;
            var lastBlock = result.GetRange(lastBlockStart, BlockSize);
            result.RemoveRange(lastBlockStart, BlockSize);
            result.AddRange(RemovePadding(lastBlock));
        }

        return result;
    }
    
}