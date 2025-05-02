using Crypota.Interfaces;

namespace Crypota.Symmetric.Handlers;

public static class EcbHandler
{
    public static async Task EncryptBlocksInPlaceAsync(
        Memory<byte> state,
        ISymmetricCipher encryptor,
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
            throw new ArgumentException($"Data length ({state.Length}) must be a multiple of the block size ({blockSize}) for in-place mode.", nameof(state));

        int totalBlocks = state.Length / blockSize;

        if (encryptor.EncryptionState != null) encryptor.EncryptionState.EncryptedBlocks = 0;
        
        await Parallel.ForEachAsync(
            source: Enumerable.Range(0, totalBlocks),
            parallelOptions: new ParallelOptions { CancellationToken = cancellationToken },
            async (blockIndex, ct) =>
            {
                int startOffset = blockIndex * blockSize;

                Span<byte> blockSpan = state.Span.Slice(startOffset, blockSize);
                
                encryptor.EncryptBlock(blockSpan);
                if (encryptor.EncryptionState != null) encryptor.EncryptionState.EncryptedBlocks += 1;

                await Task.CompletedTask;
            });
    }


    public static async Task DecryptBlocksInPlaceAsync(
        Memory<byte> state,
        ISymmetricCipher decryptor,
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
            throw new ArgumentException($"Data length ({state.Length}) must be a multiple of the block size ({blockSize}) for in-place.", nameof(state));

        int totalBlocks = state.Length / blockSize;
        
        if (decryptor.EncryptionState != null) decryptor.EncryptionState.EncryptedBlocks = 0;


        await Parallel.ForEachAsync(
             source: Enumerable.Range(0, totalBlocks),
             parallelOptions: new ParallelOptions { CancellationToken = cancellationToken },
             async (blockIndex, ct) =>
             {
                 int startOffset = blockIndex * blockSize;
                 Span<byte> blockSpan = state.Span.Slice(startOffset, blockSize);
                 
                 decryptor.DecryptBlock(blockSpan);
                 
                 if (decryptor.EncryptionState != null) decryptor.EncryptionState.EncryptedBlocks += 1;
                 
                 await Task.CompletedTask;
             });
    }
}