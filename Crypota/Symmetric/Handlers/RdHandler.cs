using System.Numerics;
using System.Runtime.CompilerServices;
using Crypota.CryptoMath;
using Crypota.Interfaces;

namespace Crypota.Symmetric.Handlers;

public class RdHandler
{

    public static async Task EncryptBlocksInPlaceAsync(
        Memory<byte> state,
        ISymmetricCipher encryptor,
        byte[]? ivReal,
        BigInteger iv,
        BigInteger delta,
        CancellationToken cancellationToken = default)
    {
        if (encryptor == null)
            throw new ArgumentNullException(nameof(encryptor));

        int blockSize = encryptor.BlockSize;
        if (blockSize <= 0)
            throw new ArgumentException("ISymmetricCipher must provide a positive BlockSize.", nameof(encryptor));

        if (state.Length == 0)
            return;

        if (state.Length % blockSize != 0)
            throw new ArgumentException(
                $"Data length ({state.Length}) must be a multiple of the block size ({blockSize}) for in-place ECB mode.",
                nameof(state));

        int totalBlocks = state.Length / blockSize;

        if (encryptor.EncryptionState != null) encryptor.EncryptionState.EncryptedBlocks = 0;

        await Parallel.ForEachAsync(
            source: Enumerable.Range(0, totalBlocks),
            parallelOptions: new ParallelOptions { CancellationToken = cancellationToken , MaxDegreeOfParallelism = Environment.ProcessorCount},
             async (blockIndex, ct) =>
            {
                BigInteger diff = blockIndex * delta + iv;
                byte[] counterBytes = diff.ToByteArray();
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(counterBytes);

                Array.Resize(ref counterBytes, blockSize);

                int startOffset = blockIndex * blockSize;
                Span<byte> currentBlock = state.Span.Slice(startOffset, blockSize);

                encryptor.EncryptBlock(counterBytes);
                SymmetricUtils.XorInPlace(currentBlock, counterBytes);

                if (encryptor.EncryptionState != null) encryptor.EncryptionState.EncryptedBlocks += 1;

                await Task.CompletedTask;
            });
        BigInteger diff = totalBlocks * delta + iv;
        byte[] counterBytes = diff.ToByteArray();
        if (BitConverter.IsLittleEndian)
            Array.Reverse(counterBytes);

        Array.Resize(ref counterBytes, blockSize);
        counterBytes.CopyTo(ivReal.AsSpan(0, blockSize));
    }


    public static async Task DecryptBlocksInPlaceAsync(
        Memory<byte> state,
        ISymmetricCipher decryptor,
        byte[]? ivReal,
        BigInteger iv,
        BigInteger delta,
        CancellationToken cancellationToken = default)
    {
        if (decryptor == null)
            throw new ArgumentNullException(nameof(decryptor));

        int blockSize = decryptor.BlockSize;
        if (blockSize <= 0)
            throw new ArgumentException("ISymmetricCipher must provide a positive BlockSize.", nameof(decryptor));

        if (state.Length == 0)
            return;

        if (state.Length % blockSize != 0)
            throw new ArgumentException(
                $"Data length ({state.Length}) must be a multiple of the block size ({blockSize}) for in-place RD mode.",
                nameof(state));

        int totalBlocks = state.Length / blockSize;

        if (decryptor.EncryptionState != null) decryptor.EncryptionState.EncryptedBlocks = 0;


        await Parallel.ForEachAsync(
            source: Enumerable.Range(0, totalBlocks),
            parallelOptions: new ParallelOptions { CancellationToken = cancellationToken },
            async (blockIndex, ct) =>
            {
                BigInteger diff = blockIndex * delta + iv;
                byte[] counterBytes = diff.ToByteArray();
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(counterBytes);

                Array.Resize(ref counterBytes, blockSize);

                int startOffset = blockIndex * blockSize;
                Span<byte> currentBlock = state.Span.Slice(startOffset, blockSize);

                decryptor.EncryptBlock(counterBytes);
                SymmetricUtils.XorInPlace(currentBlock, counterBytes);

                if (decryptor.EncryptionState != null) decryptor.EncryptionState.EncryptedBlocks += 1;

                await Task.CompletedTask;
            });
        BigInteger diff = totalBlocks * delta + iv;
        byte[] counterBytes = diff.ToByteArray();
        if (BitConverter.IsLittleEndian)
            Array.Reverse(counterBytes);

        Array.Resize(ref counterBytes, blockSize);
        counterBytes.CopyTo(ivReal.AsSpan(0, blockSize));
    }
}