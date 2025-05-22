using System.Buffers;
using Crypota.CryptoMath;
using Crypota.Interfaces;

namespace Crypota.Symmetric.Handlers;

public class PcbcHandler
{
    public static void EncryptBlocksInPlace(
        Memory<byte> state,
        ISymmetricCipher encryptor,
        byte[]? iv,
        CancellationToken cancellationToken = default)
    {
        if (encryptor == null)
            throw new ArgumentNullException(nameof(encryptor));
        if (iv == null)
            throw new ArgumentNullException(nameof(iv), "IV is required for CBC mode.");

        int blockSize = encryptor.BlockSize;
        if (blockSize <= 0)
            throw new ArgumentException("ISymmetricCipher must provide a positive BlockSize.", nameof(encryptor));
        if (iv.Length != blockSize)
            throw new ArgumentException($"IV length ({iv.Length}) must match the block size ({blockSize}).",
                nameof(iv));

        if (state.Length == 0)
            return;

        if (state.Length % blockSize != 0)
            throw new ArgumentException(
                $"Data length ({state.Length}) must be a multiple of the block size ({blockSize}) for CBC mode.",
                nameof(state));

        int totalBlocks = state.Length / blockSize;
        
        byte[] prevBlockReal = ArrayPool<byte>.Shared.Rent(blockSize);
        byte[] tempReal = ArrayPool<byte>.Shared.Rent(blockSize);
        
        
        try
        {
            var prevBlock = new Span<byte>(prevBlockReal).Slice(0, blockSize);
            var temp = new Span<byte>(tempReal).Slice(0, blockSize);
            
            iv.CopyTo(prevBlock);
            for (int i = 0; i < totalBlocks; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int startOffset = i * blockSize;

                var currentBlock = state.Span.Slice(startOffset, blockSize);
                currentBlock.CopyTo(temp);
                
                SymmetricUtils.XorInPlace(currentBlock, prevBlock);
                encryptor.EncryptBlock(currentBlock);

                currentBlock.CopyTo(prevBlock);
                SymmetricUtils.XorInPlace(prevBlock, temp);

            }
            prevBlock.CopyTo(iv);
            
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(prevBlockReal);
            ArrayPool<byte>.Shared.Return(tempReal);
        }
    }

    
    public static void DecryptBlocksInPlace(
        Memory<byte> state,
        ISymmetricCipher decryptor,
        byte[]? iv,
        CancellationToken cancellationToken = default)
    {
        if (decryptor == null)
            throw new ArgumentNullException(nameof(decryptor));
        if (iv == null)
            throw new ArgumentNullException(nameof(iv), "IV is required for CBC mode.");

        int blockSize = decryptor.BlockSize;
        if (blockSize <= 0)
            throw new ArgumentException("ISymmetricCipher must provide a positive BlockSize.", nameof(decryptor));
        if (iv.Length != blockSize)
            throw new ArgumentException($"IV length ({iv.Length}) must match the block size ({blockSize}).",
                nameof(iv));

        if (state.Length == 0)
            return;

        if (state.Length % blockSize != 0)
            throw new ArgumentException(
                $"Data length ({state.Length}) must be a multiple of the block size ({blockSize}) for CBC mode.",
                nameof(state));

        int totalBlocks = state.Length / blockSize;
        byte[] prevBlockReal = ArrayPool<byte>.Shared.Rent(blockSize);
        byte[] tempReal = ArrayPool<byte>.Shared.Rent(blockSize);
        

        try
        {
            var prevBlock = new Span<byte>(prevBlockReal).Slice(0, blockSize);
            var temp = new Span<byte>(tempReal).Slice(0, blockSize);
            
            iv.CopyTo(prevBlock);
            
            Span<byte> prev = prevBlock;

            for (int i = 0; i < totalBlocks; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int startOffset = i * blockSize;
                Span<byte> currentBlock = state.Span.Slice(startOffset, blockSize);
                currentBlock.CopyTo(temp);
                
                decryptor.DecryptBlock(currentBlock);
                SymmetricUtils.XorInPlace(currentBlock, prev);
                
                currentBlock.CopyTo(prev);
                SymmetricUtils.XorInPlace(prev, temp);
            }
            prevBlock.CopyTo(iv);

        }
        finally
        {
            ArrayPool<byte>.Shared.Return(prevBlockReal);
            ArrayPool<byte>.Shared.Return(tempReal);
        }
    }
}