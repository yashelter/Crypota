using System.Buffers;
using Crypota.CryptoMath;
using Crypota.Interfaces;

namespace Crypota.Symmetric.Handlers;

public class CbcHandler
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

        // C_i = Encrypt(P_i XOR C_{i-1}), where C_0 = IV
        
        byte[] prevBlockReal = ArrayPool<byte>.Shared.Rent(blockSize);
        var prevBlock = prevBlockReal.AsSpan(0, blockSize);
        try
        {
            iv.CopyTo(prevBlock);
            for (int i = 0; i < totalBlocks; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int startOffset = i * blockSize;

                var currentBlock = state.Span.Slice(startOffset, blockSize);
                SymmetricUtils.XorInPlace(currentBlock, prevBlock);
                encryptor.EncryptBlock(currentBlock);
                currentBlock.CopyTo(prevBlock);
            }

            prevBlock.CopyTo(iv);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(prevBlockReal);
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

        // M_i = Decrypt(C_i) XOR C_{i-1}, where C_0 = IV
        
        byte[] prevBlock = ArrayPool<byte>.Shared.Rent(blockSize);
        byte[] tempBlock = ArrayPool<byte>.Shared.Rent(blockSize);
        
        try
        {
            iv.CopyTo(prevBlock.AsSpan(0, blockSize));
            Span<byte> prevBlockSpan = prevBlock.AsSpan(0, blockSize);
            Span<byte> temp = tempBlock.AsSpan(0, blockSize);

            for (int i = 0; i < totalBlocks; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int startOffset = i * blockSize;

                Span<byte> currentBlockSpan = state.Span.Slice(startOffset, blockSize);
                currentBlockSpan.CopyTo(temp);
                decryptor.DecryptBlock(currentBlockSpan);

                SymmetricUtils.XorInPlace(currentBlockSpan, prevBlockSpan);
                temp.CopyTo(prevBlockSpan);
            }
            prevBlockSpan.CopyTo(iv);
            
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(prevBlock);
            ArrayPool<byte>.Shared.Return(tempBlock);
        }
    }

    public static async Task DecryptBlocksParallelAsync(
        Memory<byte> ciphertextAndPlaintext,
        ISymmetricCipher decryptor,
        byte[]? iv, // here not copy (so one more reason not use this)
        CancellationToken cancellationToken = default)
    {
        if (decryptor == null) throw new ArgumentNullException(nameof(decryptor));
        if (iv == null) throw new ArgumentNullException(nameof(iv), "IV is required for CBC mode.");
        int blockSize = decryptor.BlockSize;
        if (blockSize <= 0) throw new ArgumentException("...", nameof(decryptor));
        if (iv.Length != blockSize) throw new ArgumentException("...", nameof(iv));
        if (ciphertextAndPlaintext.Length == 0) return;
        if (ciphertextAndPlaintext.Length % blockSize != 0)
            throw new ArgumentException("...", nameof(ciphertextAndPlaintext));

        int totalBlocks = ciphertextAndPlaintext.Length / blockSize;


        byte[] plaintextBuffer = new byte[ciphertextAndPlaintext.Length];
        Memory<byte> plaintextMemory = plaintextBuffer.AsMemory();

        // --- Parallel Pass: Compute P_i = (Decrypt(C_i)) XOR C_{i-1} -> Store P_i in aux buffer ---
        await Parallel.ForEachAsync(
            source: Enumerable.Range(0, totalBlocks),
            parallelOptions: new ParallelOptions { CancellationToken = cancellationToken },
            async (blockIndex, ct) =>
            {
                int startOffset = blockIndex * blockSize;

                ReadOnlySpan<byte> currBlock = ciphertextAndPlaintext.Span.Slice(startOffset, blockSize);
                ReadOnlySpan<byte> prevBlock;
                
                if (blockIndex == 0)
                {
                    prevBlock = iv.AsSpan();
                }
                else
                {
                    prevBlock= ciphertextAndPlaintext.Span.Slice(startOffset - blockSize, blockSize);
                }

                Span<byte> targetBlock = plaintextMemory.Span.Slice(startOffset, blockSize);
                
                byte[] tempDecryptionBuffer = ArrayPool<byte>.Shared.Rent(blockSize);
                try
                {
                    Span<byte> tempDecryptionSpan = tempDecryptionBuffer.AsSpan(0, blockSize);

                    currBlock.CopyTo(tempDecryptionSpan);
                    decryptor.DecryptBlock(tempDecryptionSpan);
                    tempDecryptionSpan.CopyTo(targetBlock);

                    SymmetricUtils.XorInPlace(targetBlock, prevBlock);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(tempDecryptionBuffer);
                }

                await Task.CompletedTask;
            });

        cancellationToken.ThrowIfCancellationRequested();
        plaintextMemory.CopyTo(ciphertextAndPlaintext);
        
    }
}

