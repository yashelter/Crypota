namespace Crypota;


public interface ISymmetricCipher
{
    public byte[]? Key { get; set; }
    
    public byte[] EncryptBlock(byte[] block);
    
    public byte[] DecryptBlock(byte[] block);
}