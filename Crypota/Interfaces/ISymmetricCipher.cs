namespace Crypota.Interfaces;


public interface ISymmetricCipher
{
    public byte[]? Key { get; set; }
    
    public byte[] EncryptBlock(byte[] block);
    
    public byte[] DecryptBlock(byte[] block);
    
    public int BlockSize { get; }
    public int KeySize { get; }
}