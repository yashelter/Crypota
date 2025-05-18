using System;
using System.Threading;
using System.Threading.Tasks;
using Crypota.Interfaces;
using Crypota.Symmetric;
using Crypota.Symmetric.Rc6;
using Crypota.Symmetric.Twofish;
using StainsGate;
using PaddingMode = StainsGate.PaddingMode;
using Wrapper = Crypota.Symmetric;
namespace AvaloniaClient.Models;

public class EncryptingManager
{
    private SymmetricCipherWrapper encoder;

    public Action<int>? SetNewProgressState { get; set; } 

    public EncryptingManager(RoomData settings, byte[]? key, int blockSize=128, int keySize=192,  byte[]? iv = null, int? delta = null)
    {
        encoder = new SymmetricCipherWrapper
        (key, (Wrapper.CipherMode)settings.CipherMode, (Wrapper.PaddingMode)settings.Padding, GetImplementation(settings.Algo, blockSize, keySize), iv,
            new SymmetricCipherWrapper.RandomDeltaParameters()
            {
                Delta = delta ?? 3
            });
    }

    private static ISymmetricCipher GetImplementation(EncryptAlgo algo, int blockSize, int keySize)
    {
        switch (algo)
        {
            case EncryptAlgo.Rc6:
                return new Rc6();
            case EncryptAlgo.Twofish:
                return new Twofish()
                {
                    BlockSizeBits = blockSize,
                    KeySizeBits = keySize
                };
            default:
                throw new NotSupportedException("Unknown algo");
        }
    }
    
    public Task SetKey(byte[] key)
    {
        return Task.CompletedTask;
    }

    public Task SetIv(byte[] iv)
    {
        return Task.CompletedTask;
    }

    public async Task<byte[]> EncryptMessage(byte[] message, CancellationToken ct = default)
    {
        return message;
    }

    public async Task<byte[]> DecryptMessage(byte[] message, CancellationToken ct = default)
    {
        return message;
    }
    // TODO: file management
    // all should be maximum ansync
}