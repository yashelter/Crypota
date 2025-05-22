using System;
using static Crypota.CryptoMath.SymmetricUtils;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Crypota.Interfaces;
using Crypota.Symmetric;
using Crypota.Symmetric.Rc6;
using Crypota.Symmetric.Twofish;
using Serilog;
using StainsGate;
using PaddingMode = StainsGate.PaddingMode;
using Wrapper = Crypota.Symmetric;
namespace AvaloniaClient.Models;

public class EncryptingManager
{
    private readonly SymmetricCipherWrapper.SymmetricCipherWrapperBuilder encoderBuilder;
    
    public Action<int>? SetNewProgressState { get; set; } 
    

    public EncryptingManager(RoomData settings, byte[]? key, int blockSize=128, int keySize=192, byte[]? iv = null, int? delta = null)
    {
        encoderBuilder = SymmetricCipherWrapper.CreateBuilder();
        encoderBuilder = encoderBuilder.WithImplementation(GetImplementation(settings.Algo, blockSize, keySize)) // TODO: check
            .WithCipherMode((Wrapper.CipherMode)settings.CipherMode)
            .WithPadding((Wrapper.PaddingMode)settings.Padding)
            .WithIv(iv ?? new byte[blockSize])
            .AddParam(new SymmetricCipherWrapper.RandomDeltaParameters() { Delta = delta ?? 3 })
            .WithKey(key)
            .WithBlockSize(blockSize / 8)
            .WithKeySize(keySize / 8);

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
    
    public void SetKey(byte[]? sharedSecret)
    {
        if (sharedSecret == null) { throw new ArgumentNullException(nameof(sharedSecret)); }
        
        byte[] okm = HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            ikm: sharedSecret,
            outputLength: 24);
        
        Log.Debug("Set key for session: {Key}", ArrayToHexString(okm));
        encoderBuilder.WithKey(okm);
    }

    public void SetIv(byte[]? iv)
    {
        encoderBuilder.WithIv(iv);
    }
    
    public byte[] GenerateNewIv()
    {
        byte[] iv = new byte[encoderBuilder.GetBlockSize() ?? 16];
        RandomNumberGenerator.Fill(iv); 
        encoderBuilder.WithIv(iv);
        
        return iv;
    }

    public async Task<byte[]> EncryptMessage(byte[] message, CancellationToken ct = default)
    {
        var encoder = encoderBuilder.Build();
        return await Task.Run(async () => await encoder.EncryptMessageAsync(message, true, ct), ct);
    }

    public async Task<byte[]> DecryptMessage(byte[] message, CancellationToken ct = default)
    {
        var encoder = encoderBuilder.Build();
        return await Task.Run(async () => await encoder.DecryptMessageAsync(message, true, ct), ct);
    }

    public SymmetricCipherWrapper BuildEncoder()
    {
        return encoderBuilder.Build();
    }
    
    public static async Task<byte[]> EncryptMessageManual(SymmetricCipherWrapper encoder, byte[] message, CancellationToken ct = default)
    {
        return await Task.Run(async () => await encoder.EncryptMessageAsync(message, true, ct), ct);
    }

    public static async Task<byte[]> DecryptMessageManual(SymmetricCipherWrapper encoder, byte[] message, CancellationToken ct = default)
    {
        return await Task.Run(async () => await encoder.DecryptMessageAsync(message, true, ct), ct);
    }
    
}