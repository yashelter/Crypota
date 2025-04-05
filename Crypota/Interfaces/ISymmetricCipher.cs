namespace Crypota;


public interface ISymmetricCipher
{
    public List<byte>? Key { get; set; }
    
    public List<byte> EncryptBlock(List<byte> block);
    
    public List<byte> DecryptBlock(List<byte> block);
}