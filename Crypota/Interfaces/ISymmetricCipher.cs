using Crypota.Symmetric;

namespace Crypota.Interfaces;


public interface ISymmetricCipher : ICloneable
{
    public byte[]? Key { get; set; }
    
    public void EncryptBlock(Span<byte> state);
    
    public void DecryptBlock(Span<byte> state);
    
    public int BlockSize { get; }
    public int KeySize { get; }
    public EncryptionState? EncryptionState { get; }
}