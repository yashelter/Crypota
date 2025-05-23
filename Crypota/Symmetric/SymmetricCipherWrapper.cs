using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Crypota.Interfaces;
using Crypota.Symmetric.Exceptions;
using Crypota.Symmetric.Handlers;

namespace Crypota.Symmetric;

public enum CipherMode { ECB = 2, CBC = 3, OFB = 4, PCBC = 5, CTR = 1, RD = 0, CFB = 6 }

public enum PaddingMode { Zeros = 3, ANSIX923 = 1, PKCS7 = 2, ISO10126 = 0 }

public class SymmetricCipherWrapper : ISymmetricCipher
{
     public class SymmetricCipherWrapperBuilder
    {
        private byte[]? _key;
        private byte[]? _iv;
        private CipherMode _mode = CipherMode.RD;
        private PaddingMode _padding = PaddingMode.ISO10126;
        private ISymmetricCipher? _implementation;
        private readonly List<object> _additionalParams = [];
        private int? _blockSize = null;
        private int? _keySize = null;

        public int? GetBlockSize()
        {
            return _blockSize;
        }
        
        public int? GetKeySize()
        {
            return _keySize;
        }
        
        public SymmetricCipherWrapperBuilder WithBlockSize(int? size)
        {
            if (size == null) return this;
            _blockSize = size;
            return this;
        }

        
        public SymmetricCipherWrapperBuilder WithKeySize(int? size)
        {
            if (size == null) return this;
            _keySize = size;
            return this;
        }

        
        public SymmetricCipherWrapperBuilder WithKey(byte[]? key)
        {
            if (key == null) return this;
            _key = key.ToArray();
            return this;
        }
        
        public SymmetricCipherWrapperBuilder WithIv(byte[]? iv)
        {
            if (iv == null) return this;
            _iv = iv.ToArray();
            return this;
        }
        
        public SymmetricCipherWrapperBuilder WithCipherMode(CipherMode mode)
        {
            _mode = mode;
            return this;
        }
        
        public SymmetricCipherWrapperBuilder WithPadding(PaddingMode padding)
        {
            _padding = padding;
            return this;
        }
        
        public SymmetricCipherWrapperBuilder WithImplementation(ISymmetricCipher impl)
        {
            _implementation = impl.Clone() as ISymmetricCipher;
            return this;
        }
        
        public SymmetricCipherWrapperBuilder AddParam<T>(T param) where T : class
        {
            _additionalParams.Add(param!);
            return this;
        }

        public SymmetricCipherWrapper Build()
        {
            if (_implementation == null)
                throw new InvalidOperationException("Implementation must be provided via WithImplementation().");
            
            var keyCopy = _key?.ToArray();
            var ivCopy  = _iv?.ToArray();
            
            var wrapper = new SymmetricCipherWrapper(
                keyCopy,
                _mode,
                _padding,
                _implementation.Clone() as ISymmetricCipher ?? throw new InvalidOperationException("Implementation must be clonable"),
                ivCopy,
                _additionalParams.ToArray()
            );
            if (_keySize != null)
            {
                wrapper.KeySize = _keySize.Value;
            }

            if (_blockSize != null)
            {
                wrapper.BlockSize = _blockSize.Value;
            }
            return wrapper;
        }
    }
    
    private byte[]? _key;

    public int BlockSize { get; set; }
    public int KeySize { get; set; }

    private byte[]? _iv;
    private readonly CipherMode _cipherMode;
    private readonly PaddingMode _paddingMode;
    private readonly ISymmetricCipher _implementation;
    private readonly Dictionary<string, object> _params;
    
    public EncryptionState EncryptionState { get; private set; } = new EncryptionState();

    public class RandomDeltaParameters
    {
        public required int Delta { get; init; }
    }

    private T GetParam<T>() where T : class
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
            if (value != null) _implementation.Key = value;
        }
    }
    

    public SymmetricCipherWrapper(
        byte[]? key,
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
    
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void EncryptBlock(Span<byte> state) => _implementation.EncryptBlock(state);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void DecryptBlock(Span<byte> state) => _implementation.DecryptBlock(state);


    /// <summary>
    /// Добавляет отступы к последнему блоку данных, чтобы его длина стала кратной BlockSize.
    /// Всегда добавляет отступы, даже если длина входных данных уже кратна BlockSize (кроме режима Zeros и None).
    /// </summary>
    /// <param name="inputData">Входные данные (последний, возможно неполный, блок).</param>
    /// <returns>Новый массив байт с добавленными отступами.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private byte[] ApplyPadding(ReadOnlySpan<byte> inputData)
    {
        EncryptionState.Transform = EncryptionStateTransform.ApplyingPadding;

        int startPos = inputData.Length;
        int paddingBytesNeeded = BlockSize - (startPos % BlockSize);
        
        byte[] paddedBlock = new byte[BlockSize];
        inputData.CopyTo(paddedBlock);

        startPos = startPos == BlockSize ? 0 : startPos;
        
        switch (_paddingMode)
        {
            case PaddingMode.Zeros:
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
                 using (var rng = RandomNumberGenerator.Create()) 
                 {
                      rng.GetBytes(randomBytes);
                 }
                 Array.Copy(randomBytes, 0, paddedBlock, startPos, randomBytes.Length);
                 paddedBlock[startPos + paddingBytesNeeded - 1] = (byte)paddingBytesNeeded;
                 break;
            
            default:
                 throw new InvalidPaddingException($"Padding mode {_paddingMode} is not supported");
        }

        return paddedBlock;
    }


    /// <summary>
    /// Удаляет отступы из последнего блока данных.
    /// </summary>
    /// <param name="paddedData">Данные с отступами (предполагается, что это последний блок).</param>
    /// <returns>Новый массив байт без отступов.</returns>
    /// <exception cref="ArgumentException">Если данные некорректны или отступы повреждены.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
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
                    throw new InvalidPaddingException($"Invalid ANSIX923 padding size: {paddingSize}");

                for (int i = originalLength - paddingSize; i < originalLength - 1; i++)
                {
                    if (paddedData[i] != 0x00)
                        throw new InvalidPaddingException($"Invalid ANSIX923 padding byte at index {i}");
                }
                break;

            case PaddingMode.PKCS7:
                paddingSize = lastByte;
                if (paddingSize == 0 || paddingSize > BlockSize)
                    throw new InvalidPaddingException($"Invalid PKCS7 padding size: {paddingSize}");

                for (int i = originalLength - paddingSize; i < originalLength; i++)
                {
                    if (paddedData[i] != paddingSize)
                        throw new InvalidPaddingException($"Invalid PKCS7 padding byte at index {i}. Expected {paddingSize}, got {paddedData[i]}");
                }
                break;

            case PaddingMode.ISO10126:
                paddingSize = lastByte;
                if (paddingSize == 0 || paddingSize > BlockSize)
                    throw new InvalidPaddingException($"Invalid ISO10126 padding size: {paddingSize}");
                break;

            default:
                throw new InvalidPaddingException($"Padding mode {_paddingMode} is not supported for removal");
        }


        int dataLength = originalLength - paddingSize;
        if (dataLength < 0)
             throw new InvalidPaddingException("Padding size calculation resulted in negative data length.");

        byte[] unpaddedBlock = new byte[dataLength];
        Array.Copy(paddedData, 0, unpaddedBlock, 0, dataLength);
        return unpaddedBlock;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private byte[] GetPaddedMessage(Memory<byte> message)
    {
        EncryptionState.Transform = EncryptionStateTransform.ApplyingPadding;

        int fullBlocksCount = message.Length / BlockSize;
        int lastBlockStartIndex = fullBlocksCount * BlockSize;
        ReadOnlySpan<byte> lastPartSpan = message.Span.Slice(lastBlockStartIndex);

        byte[]? paddedLastBlock;
        int totalSizeInBytes;

        if (lastPartSpan.IsEmpty)
        {
            if (message.Length == 0 && _paddingMode == PaddingMode.Zeros)
            {
                paddedLastBlock = null;
                totalSizeInBytes = 0;
            }
            else if (_paddingMode == PaddingMode.Zeros)
            {
                paddedLastBlock = null;
                totalSizeInBytes = message.Length;
            }
            else
            {
                paddedLastBlock = ApplyPadding(lastPartSpan);
                totalSizeInBytes = message.Length + BlockSize;
            }
        }
        else
        {
            paddedLastBlock = ApplyPadding(lastPartSpan);
            totalSizeInBytes = lastBlockStartIndex + BlockSize;
        }

        byte[] result = new byte[totalSizeInBytes];
        
        if (lastBlockStartIndex > 0)
        {
            message.Span.Slice(0, lastBlockStartIndex).CopyTo(result);
        }
        
        if (paddedLastBlock != null)
        {
            Array.Copy(sourceArray: paddedLastBlock,
                sourceIndex: 0,
                destinationArray: result,
                destinationIndex: lastBlockStartIndex,
                length: paddedLastBlock.Length);
        }
        return result;
    }

    private BigInteger GetInitialCounter()
    {
        if (_iv == null) return BigInteger.Zero;
        return new BigInteger(_iv, isUnsigned: true, isBigEndian: false);
    }

    
    public async Task<byte[]> EncryptMessageAsync(Memory<byte> message, bool saveStateInIv = false, CancellationToken ct = default)
    {
        EncryptionState.Transform = EncryptionStateTransform.Analyzing;

        var buffer = GetPaddedMessage(message);

        EncryptionState.Transform = EncryptionStateTransform.Encrypting;
        EncryptionState.EncryptedBlocks = 0;
        EncryptionState.BlocksToEncrypt = buffer.Length / BlockSize;

        byte[] iv = _iv.ToArray();

        switch (_cipherMode)
        {
            case CipherMode.ECB:
                await EcbHandler.EncryptBlocksInPlaceAsync(buffer, this, ct);
                break;
            case CipherMode.CBC:
                CbcHandler.EncryptBlocksInPlace(buffer, this, _iv, ct);
                break;
            case CipherMode.OFB:
                OfbHandler.EncryptBlocksInPlace(buffer, this, _iv, ct);
                break;
            case CipherMode.CFB:
                CfbHandler.EncryptBlocksInPlace(buffer, this, _iv, ct);
                break;
            case CipherMode.PCBC:
                PcbcHandler.EncryptBlocksInPlace(buffer, this, _iv, ct);
                break;
            case CipherMode.CTR:
                await RdHandler.EncryptBlocksInPlaceAsync(buffer, this, iv, 
                    GetInitialCounter(),1, ct);
                break;
            case CipherMode.RD:    
                await RdHandler.EncryptBlocksInPlaceAsync(buffer, this, iv,
                    GetInitialCounter(), GetParam<RandomDeltaParameters>().Delta, ct);
                break;
        }
        
        if (saveStateInIv)
        {
            _iv = iv;
        }
        
        EncryptionState.Transform = EncryptionStateTransform.Idle;
        return buffer;
    }
    
    
    public async Task<byte[]> DecryptMessageAsync(Memory<byte> message, bool saveStateInIv = false, CancellationToken ct = default)
    {
        EncryptionState.Transform = EncryptionStateTransform.Analyzing;

        byte[] bufferRes = new byte[message.Length];
        message.Span.CopyTo(bufferRes);
        
        var buffer = new Memory<byte>(bufferRes);

        EncryptionState.Transform = EncryptionStateTransform.Decrypting;
        EncryptionState.EncryptedBlocks = 0;
        EncryptionState.BlocksToEncrypt = buffer.Length / BlockSize;
        
        byte[] iv = _iv.ToArray();

        switch (_cipherMode)
        {
            case CipherMode.ECB:
                await EcbHandler.DecryptBlocksInPlaceAsync(buffer, this, ct);
                break;
            case CipherMode.CBC:
                CbcHandler.DecryptBlocksInPlace(buffer, this, iv, ct);
                break;
            case CipherMode.OFB:
                OfbHandler.DecryptBlocksInPlace(buffer, this, iv, ct);
                break;
            case CipherMode.CFB:
                CfbHandler.DecryptBlocksInPlace(buffer, this, iv, ct);
                break;
            case CipherMode.PCBC:
                PcbcHandler.DecryptBlocksInPlace(buffer, this, iv, ct);
                break;
            case CipherMode.CTR:
                await RdHandler.DecryptBlocksInPlaceAsync(buffer, this,  iv, 
                    GetInitialCounter(),1, ct);
                break;
            case CipherMode.RD:    
                await RdHandler.DecryptBlocksInPlaceAsync(buffer, this, iv,
                    GetInitialCounter(),GetParam<RandomDeltaParameters>().Delta, ct);
                break;
        }
        EncryptionState.Transform = EncryptionStateTransform.RemovingPadding;
        if (saveStateInIv)
        {
            _iv = iv;
        }
        
        byte[] last = RemovePadding(buffer.Slice(buffer.Length - BlockSize, BlockSize).ToArray());
        int index = buffer.Length - BlockSize < 0 ? 0 : buffer.Length - BlockSize;
        
        byte[] result = new byte[index + last.Length];
        
        buffer.Slice(0, index).CopyTo(result);
        last.CopyTo(result.AsSpan(index));
        
        EncryptionState.Transform = EncryptionStateTransform.Idle;
        return result;
    }
    
    
    public static SymmetricCipherWrapperBuilder CreateBuilder() => new SymmetricCipherWrapperBuilder();

    public object Clone()
    {
        var builder = CreateBuilder()
            .WithImplementation(_implementation)
            .WithCipherMode(_cipherMode)
            .WithPadding(_paddingMode);

        if (_key != null)
            builder = builder.WithKey(_key);

        if (_iv != null)
            builder = builder.WithIv(_iv);

        foreach (var param in _params.Values)
            builder = builder.AddParam(param);

        var clone = builder.Build();

        clone.EncryptionState = new EncryptionState();

        return clone;
    }
}