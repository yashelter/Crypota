namespace Crypota;

using static Crypota.RoundKey;


public interface IEncryptionTransformation
{
    public byte[] EncryptionTransformation(byte[] message, RoundKey roundKey);
}