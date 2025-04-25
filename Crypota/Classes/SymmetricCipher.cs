using System.Collections.Concurrent;
using System.Numerics;
using System.Security.Cryptography;

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

    private byte[]? _key;
    public int BlockSize { get;  }
    public int KeySize { get;  }

    private readonly byte[]? _iv;
    private readonly CipherMode _cipherMode;
    private readonly PaddingMode _paddingMode;
    private readonly ISymmetricCipher _implementation;
    private readonly Dictionary<string, object> _params;
    
    public EncryptionState EncryptionState { get; private set; } = new EncryptionState();

    public class RandomDeltaParameters
    {
        public required int Delta { get; set; }
    }

    public T GetParam<T>() where T : class
    {
        string key = typeof(T).Name;
        if (_params.TryGetValue(key, out object? value))
        {
            if (value is null)
            {
                throw new ArgumentException($"Needed parameter {nameof(value)} not supposed to be null.");
            }

            return (T)value;
        }

        throw new KeyNotFoundException($"Параметр типа {key} не найден.");
    }

    public byte[]? Key
    {
        get => _key;
        set
        {
            _key = value;
            _implementation.Key = value;
        }
    }


    public SymmetricCipher(
        byte[] key,
        CipherMode mode,
        PaddingMode padding,
        ISymmetricCipher implementation, // added
        byte[]? iv = null,
        params object[] additionalParams // for RD need delta only 
    )
    {
        _cipherMode = mode;
        _paddingMode = padding;
        _iv = iv ?? null;
        if (BitConverter.IsLittleEndian && _iv is not null)
        {
            Array.Reverse(_iv);
        }
        BlockSize = implementation.BlockSize;
        KeySize = implementation.KeySize;
        
        _implementation = implementation;
        Key = key;
        _params = additionalParams.ToDictionary(p => p.GetType().Name);
    }


    public byte[] EncryptBlock(byte[] block) => _implementation.EncryptBlock(block);

    public byte[] DecryptBlock(byte[] block) => _implementation.DecryptBlock(block);


    /// <summary>
    /// Добавляет отступы к последнему блоку данных, чтобы его длина стала кратной BlockSize.
    /// Всегда добавляет отступы, даже если длина входных данных уже кратна BlockSize (кроме режима Zeros и None).
    /// </summary>
    /// <param name="inputData">Входные данные (последний, возможно неполный, блок).</param>
    /// <returns>Новый массив байт с добавленными отступами.</returns>
    private byte[] ApplyPadding(byte[] inputData)
    {
        EncryptionState.Transform = EncryptionStateTransform.ApplyingPadding;
        if (inputData == null) throw new ArgumentNullException(nameof(inputData));

        int startPos = inputData.Length;
        int paddingBytesNeeded = BlockSize - (startPos % BlockSize);
        
        byte[] paddedBlock = new byte[BlockSize];
        Array.Copy(inputData, 0, paddedBlock, 0, startPos);

        startPos = startPos == BlockSize ? 0 : startPos;
        
        switch (_paddingMode)
        {
            case PaddingMode.Zeros:
                if (BlockSize == inputData.Length)
                {
                    return inputData;
                }
                break;
            
            case PaddingMode.ANSIX923:
                for (int i = 0; i < paddingBytesNeeded - 1; i++)
                {
                    paddedBlock[startPos + i] = 0x00;
                }
                paddedBlock[startPos + paddingBytesNeeded - 1] = (byte)paddingBytesNeeded;
                break;

            case PaddingMode.PKCS7:
                for (int i = 0; i < paddingBytesNeeded; i++)
                {
                    paddedBlock[startPos + i] = (byte)paddingBytesNeeded;
                }
                break;

            case PaddingMode.ISO10126:
                byte[] randomBytes = new byte[paddingBytesNeeded - 1];
                 using (var rng = RandomNumberGenerator.Create()) {
                      rng.GetBytes(randomBytes);
                 }
                Array.Copy(randomBytes, 0, paddedBlock, startPos, randomBytes.Length);
                paddedBlock[startPos + paddingBytesNeeded - 1] = (byte)paddingBytesNeeded;
                break;
            
            default:
                 throw new NotSupportedException($"Padding mode {_paddingMode} is not supported");
        }

        return paddedBlock;
    }


    /// <summary>
    /// Удаляет отступы из последнего блока данных.
    /// </summary>
    /// <param name="paddedData">Данные с отступами (предполагается, что это последний блок).</param>
    /// <returns>Новый массив байт без отступов.</returns>
    /// <exception cref="ArgumentException">Если данные некорректны или отступы повреждены.</exception>
    private byte[] RemovePadding(byte[] paddedData)
    {
        EncryptionState.Transform = EncryptionStateTransform.RemovingPadding;
        if (paddedData == null || paddedData.Length == 0)
            throw new ArgumentException("Input data cannot be null or empty.");

        if (paddedData.Length % BlockSize != 0)
            throw new ArgumentException("Invalid padded data length (must be multiple of block size).");

        int originalLength = paddedData.Length;
        int paddingSize;
        byte lastByte = paddedData[originalLength - 1];

        switch (_paddingMode)
        {
            case PaddingMode.Zeros:
                int lastNonZeroIndex = originalLength - 1;
                while (lastNonZeroIndex >= 0 && paddedData[lastNonZeroIndex] == 0x00)
                {
                    lastNonZeroIndex--;
                }
                if (lastNonZeroIndex < 0) return [0];

                int dataLengthZeros = lastNonZeroIndex + 1;
                byte[] unpaddedBlockZeros = new byte[dataLengthZeros];
                Array.Copy(paddedData, 0, unpaddedBlockZeros, 0, dataLengthZeros);
                return unpaddedBlockZeros;

            case PaddingMode.ANSIX923:
                paddingSize = lastByte;
                if (paddingSize == 0 || paddingSize > BlockSize)
                    throw new ArgumentException($"Invalid ANSIX923 padding size: {paddingSize}");

                for (int i = originalLength - paddingSize; i < originalLength - 1; i++)
                {
                    if (paddedData[i] != 0x00)
                        throw new ArgumentException($"Invalid ANSIX923 padding byte at index {i}");
                }
                break;

            case PaddingMode.PKCS7:
                paddingSize = lastByte;
                if (paddingSize == 0 || paddingSize > BlockSize)
                    throw new ArgumentException($"Invalid PKCS7 padding size: {paddingSize}");

                for (int i = originalLength - paddingSize; i < originalLength; i++)
                {
                    if (paddedData[i] != paddingSize)
                        throw new ArgumentException($"Invalid PKCS7 padding byte at index {i}. Expected {paddingSize}, got {paddedData[i]}");
                }
                break;

            case PaddingMode.ISO10126:
                paddingSize = lastByte;
                if (paddingSize == 0 || paddingSize > BlockSize)
                    throw new ArgumentException($"Invalid ISO10126 padding size: {paddingSize}");
                break;

            default:
                throw new NotSupportedException($"Padding mode {_paddingMode} is not supported for removal");
        }


        int dataLength = originalLength - paddingSize;
        if (dataLength < 0)
             throw new ArgumentException("Padding size calculation resulted in negative data length.");

        byte[] unpaddedBlock = new byte[dataLength];
        Array.Copy(paddedData, 0, unpaddedBlock, 0, dataLength);
        return unpaddedBlock;
    }

    public async Task<byte[]> EncryptMessageAsync(byte[] message)
    {
        EncryptionState.Transform = EncryptionStateTransform.Analyzing;
        
        List<byte[]> blocks = new List<byte[]>();
        List<byte> block = new List<byte>(BlockSize);

        for (int i = 0; i < message.Length; i++)
        {
            block.Add(message[i]);
            if (block.Count == BlockSize)
            {
                blocks.Add(block.ToArray());
                block.Clear();
            }
        }

        byte[] last = ApplyPadding(block.ToArray());
        if (last.Length > 0) blocks.Add(last);
        int size = message.Length < BlockSize ? BlockSize : message.Length - (message.Length % BlockSize) + last.Length;
        
        EncryptionState.Transform = EncryptionStateTransform.Encrypting;
        EncryptionState.EncryptedBlocks = 0;
        EncryptionState.BlocksToEncrypt = blocks.Count;
        
        byte[] result = new byte[size];
        byte[]? prev;
        
        BigInteger initialCounter;

        int currentOffset;
        switch (_cipherMode)
        {
            case CipherMode.ECB:
                IEnumerable<Task<byte[]>> tasks = blocks.Select(b => Task.Run(() =>
                {
                    var tmp = EncryptBlock(b);
                    EncryptionState.EncryptedBlocks += 1;
                    return tmp;
                }));
                byte[][] encryptedBlocks = await Task.WhenAll(tasks);
                
                currentOffset = 0;
                foreach (byte[] encryptedBlock in encryptedBlocks)
                {
                    Array.Copy(
                        sourceArray: encryptedBlock,
                        sourceIndex: 0,
                        destinationArray: result,
                        destinationIndex: currentOffset,
                        length: encryptedBlock.Length 
                    );
                    
                    currentOffset += encryptedBlock.Length;
                }
                break;

            case CipherMode.CBC:
                if (_iv == null)
                    throw new ArgumentException($"Setup {nameof(_iv)} before Encryption or change cipher mode");

                prev = EncryptBlock(XorTwoPartsCopy(blocks[0], _iv));
                currentOffset = 0;
                Array.Copy(
                    sourceArray: prev,
                    sourceIndex: 0,
                    destinationArray: result,
                    destinationIndex: currentOffset,
                    length: prev.Length 
                );
                EncryptionState.EncryptedBlocks += 1;
                currentOffset += prev.Length;


                foreach (var b in blocks.Skip(1))
                {
                    var encryptedBlock = EncryptBlock(XorTwoPartsCopy(b, prev));
                    Array.Copy(
                        sourceArray: encryptedBlock,
                        sourceIndex: 0,
                        destinationArray: result,
                        destinationIndex: currentOffset,
                        length: encryptedBlock.Length 
                    );
                    currentOffset += encryptedBlock.Length;
                    prev = encryptedBlock;
                    EncryptionState.EncryptedBlocks += 1;
                }
                break;

            case CipherMode.OFB:
                if (_iv == null)
                    throw new ArgumentException($"Setup {nameof(_iv)} before Encryption or change cipher mode");
                
                byte[] currentKeystream = _iv;
                byte[]? prevKeystream = null;
                currentOffset = 0;

                foreach (var b in blocks)
                {
                    prevKeystream = EncryptBlock(currentKeystream);

                    var encryptedBlock = XorTwoPartsCopy(prevKeystream, b);

                    Array.Copy(
                        sourceArray: encryptedBlock,
                        sourceIndex: 0,
                        destinationArray: result,
                        destinationIndex: currentOffset,
                        length: encryptedBlock.Length
                    );
                    currentOffset += encryptedBlock.Length;
                    currentKeystream = prevKeystream;
                    EncryptionState.EncryptedBlocks += 1;
                }
                
                break;

            case CipherMode.CFB:
                if (_iv == null)
                    throw new ArgumentException($"Setup {nameof(_iv)} before Encryption or change cipher mode");

                prev = XorTwoPartsCopy(EncryptBlock(_iv), blocks[0]);
                currentOffset = 0;
                Array.Copy(
                    sourceArray: prev,
                    sourceIndex: 0,
                    destinationArray: result,
                    destinationIndex: currentOffset,
                    length: prev.Length 
                );
                currentOffset += prev.Length;
                EncryptionState.EncryptedBlocks += 1;

                foreach (var b in blocks.Skip(1))
                {
                    var encryptedBlock = XorTwoPartsCopy(EncryptBlock(prev), b);
                    Array.Copy(
                        sourceArray: encryptedBlock,
                        sourceIndex: 0,
                        destinationArray: result,
                        destinationIndex: currentOffset,
                        length: encryptedBlock.Length 
                    );
                    currentOffset += encryptedBlock.Length;
                    prev = encryptedBlock;
                    EncryptionState.EncryptedBlocks += 1;
                }

                break;

            case CipherMode.PCBC:
                if (_iv == null)
                    throw new ArgumentException($"Setup {nameof(_iv)} before Encryption or change cipher mode");

                prev = EncryptBlock(XorTwoPartsCopy(blocks[0], _iv));
                currentOffset = 0;
                Array.Copy(
                    sourceArray: prev,
                    sourceIndex: 0,
                    destinationArray: result,
                    destinationIndex: currentOffset,
                    length: prev.Length 
                );
                currentOffset += prev.Length;
                EncryptionState.EncryptedBlocks += 1;

                for (int i = 1; i < blocks.Count; ++i)
                {
                    var encryptedBlock = EncryptBlock(XorTwoPartsCopy(XorTwoPartsCopy(blocks[i - 1], prev), blocks[i]));
                    Array.Copy(
                        sourceArray: encryptedBlock,
                        sourceIndex: 0,
                        destinationArray: result,
                        destinationIndex: currentOffset,
                        length: encryptedBlock.Length 
                    );
                    currentOffset += encryptedBlock.Length;
                    prev = encryptedBlock;
                    EncryptionState.EncryptedBlocks += 1;
                }

                break;

            case CipherMode.CTR:
            case CipherMode.RD:
                if (_iv == null)
                    throw new ArgumentException($"Setup {nameof(_iv)} before Encryption or change cipher mode");
                byte[] init = new byte[_iv.Length + 1];
                
                Array.Copy(
                    sourceArray: _iv,
                    sourceIndex: 0,
                    destinationArray: init,
                    destinationIndex: 1,
                    length: _iv.Length
                );
                
                initialCounter = new BigInteger(init);
                int delta = (_cipherMode == CipherMode.RD) ? GetParam<RandomDeltaParameters>().Delta : 1;
                currentOffset = 0;

                var encryptedBlocksDict = new ConcurrentDictionary<int, byte[]>();
                
                Parallel.ForEach(blocks.Select((b, i) => (Block: b, Index: i)), 
                     (item, ct) =>
                    {
                        BigInteger offset = item.Index * delta;
                        
                        BigInteger blockCounter = initialCounter + offset;
                        byte[] counterBytes = blockCounter.ToByteArray();
                        
                        if (BitConverter.IsLittleEndian)
                            Array.Reverse(counterBytes);
        
                        Array.Resize(ref counterBytes, BlockSize);
                        var encryptedCounter = EncryptBlock(counterBytes);
                        var encryptedBlock = XorTwoPartsCopy(encryptedCounter, item.Block);
                        encryptedBlocksDict[item.Index] = encryptedBlock;
                        EncryptionState.EncryptedBlocks += 1;
                    });
                
                foreach (var kvp in encryptedBlocksDict.OrderBy(kv => kv.Key))
                {
                    byte[] encryptedBlock = kvp.Value; 
                    if (encryptedBlock.Length > 0) 
                    {
                        Array.Copy(
                            sourceArray: encryptedBlock,
                            sourceIndex: 0,
                            destinationArray: result,
                            destinationIndex: currentOffset,
                            length: encryptedBlock.Length
                        );
                        currentOffset += encryptedBlock.Length;
                    }
                }
                break;
        }
        EncryptionState.Transform = EncryptionStateTransform.Idle;

        return result;
    }
    

    public async Task<byte[]> DecryptMessageAsync(byte[] ciphertext)
    {
        EncryptionState.Transform = EncryptionStateTransform.Analyzing;

        List<byte[]> blocks = new List<byte[]>();
        List<byte> block = new List<byte>(BlockSize);

        for (int i = 0; i < ciphertext.Length; i++)
        {
            block.Add(ciphertext[i]);
            if (block.Count == BlockSize)
            {
                blocks.Add(block.ToArray());
                block.Clear();
            }
        }

        if (block.Count != 0)
        {
            throw new ArgumentException("Decryption failed, wrong blocks size");
        }
        
        EncryptionState.Transform = EncryptionStateTransform.Decrypting;
        EncryptionState.EncryptedBlocks = 0;
        EncryptionState.BlocksToEncrypt = blocks.Count;
        
        List<byte> result = new List<byte>();
        result.Capacity = ciphertext.Length;
        
        byte[]? prev;

        BigInteger initialCounter;

        switch (_cipherMode)
        {
            case CipherMode.ECB:
                var tasks = blocks.Select(b => Task.Run(() =>
                {
                    var tmp = DecryptBlock(b);
                    EncryptionState.EncryptedBlocks += 1;
                    return tmp;
                }));
                
                byte[][] decryptedBlocks = await Task.WhenAll(tasks);
                result.AddRange(decryptedBlocks.SelectMany(b => b));
                break;

            case CipherMode.CBC:
                if (_iv == null) throw new ArgumentException(nameof(_iv));

                prev = _iv;
                foreach (var b in blocks)
                {
                    var decrypted = DecryptBlock(b);
                    var plain = XorTwoPartsCopy(decrypted, prev);
                    result.AddRange(plain);
                    prev = b;
                    EncryptionState.EncryptedBlocks += 1;
                }

                break;

            case CipherMode.OFB:
                if (_iv == null) throw new ArgumentException(nameof(_iv));

                prev = EncryptBlock(_iv);
                EncryptionState.EncryptedBlocks += 1;
                result.AddRange(XorTwoPartsCopy(prev, blocks[0]));
                
                foreach (var b in blocks.Skip(1))
                {
                    prev = EncryptBlock(prev);
                    result.AddRange(XorTwoPartsCopy(prev, b));
                    EncryptionState.EncryptedBlocks += 1;
                }

                break;

            case CipherMode.CFB:
                prev = _iv ?? throw new ArgumentException(nameof(_iv));
                
                foreach (var b in blocks)
                {
                    var encrypted = EncryptBlock(prev);
                    var plain = XorTwoPartsCopy(encrypted, b);
                    result.AddRange(plain);
                    prev = b;
                    EncryptionState.EncryptedBlocks += 1;

                }

                break;

            case CipherMode.PCBC:
                if (_iv == null) throw new ArgumentException(nameof(_iv));
                
                var firstBlock = DecryptBlock(blocks[0]);
                var prevPlain = XorTwoPartsCopy(firstBlock, _iv);
                EncryptionState.EncryptedBlocks += 1;
                result.AddRange(prevPlain);

                for (int i = 1; i < blocks.Count; i++)
                {
                    var decrypted = DecryptBlock(blocks[i]);
                    var plain = XorTwoPartsCopy(decrypted, XorTwoPartsCopy(blocks[i - 1], prevPlain));
                    result.AddRange(plain);
                    prevPlain = plain;
                    EncryptionState.EncryptedBlocks += 1;

                }

                break;

            case CipherMode.CTR:
            case CipherMode.RD:
                if (_iv == null) throw new ArgumentException(nameof(_iv));
                
                byte[] init = new byte[_iv.Length + 1];
                
                Array.Copy(
                    sourceArray: _iv,
                    sourceIndex: 0,
                    destinationArray: init,
                    destinationIndex: 1,
                    length: _iv.Length
                );
                
                initialCounter = new BigInteger(init);
                
                int delta = _cipherMode == CipherMode.RD ? GetParam<RandomDeltaParameters>().Delta : 1;

                var decryptedBlocksDict = new ConcurrentDictionary<int, byte[]>();

                Parallel.ForEach(blocks.Select((b, i) => (Block: b, Index: i)), 
                    (item, ct) => 
                    {
                    int blockIndex = item.Index;
                    BigInteger offset = blockIndex * delta;

                    byte[] counterBytes = (initialCounter + offset).ToByteArray();
                    if (BitConverter.IsLittleEndian)
                        Array.Reverse(counterBytes);

                    Array.Resize(ref counterBytes, BlockSize);

                    var encryptedCounter = EncryptBlock(counterBytes);
                    var decryptedBlock = XorTwoPartsCopy(encryptedCounter, item.Block);
                    decryptedBlocksDict[blockIndex] = decryptedBlock;
                    EncryptionState.EncryptedBlocks += 1;

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
            var lastBlock = result.GetRange(lastBlockStart, BlockSize).ToArray();
            result.RemoveRange(lastBlockStart, BlockSize);
            result.AddRange(RemovePadding(lastBlock));
        }

        EncryptionState.Transform = EncryptionStateTransform.Idle;
        return result.ToArray();
    }
    
}