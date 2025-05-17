using System;
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
    
    public void SetKey(byte[] key)
    {
        
    }

    public void SetIv(byte[] iv)
    {
        
    }

    public byte[] EncryptMessage(byte[] message)
    {
        return message;
    }

    public byte[] DecryptMessage(byte[] message)
    {
        return message;
    }
    // TODO: file management
    // all should be maximum ansync
}