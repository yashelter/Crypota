namespace Crypota;

using static Crypota.RoundKey;


public interface IEncryptionTransformation
{
    public List<byte> EncryptionTransformation(List<byte> message, RoundKey roundKey);
}