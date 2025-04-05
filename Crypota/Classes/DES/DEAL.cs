namespace Crypota.Classes.DES;
using static CryptoAlgorithms;

public class Deal128EncryptionTransformation : IEncryptionTransformation
{
    public List<byte> EncryptionTransformation(List<byte> message, RoundKey roundKey)
    {
        Des des = new Des() { Key = new List<byte>(roundKey.Key) };
        return des.EncryptBlock(message);
    }
}


public class Deal128KeyExtension : IKeyExtension
{
    public List<RoundKey> GetRoundKeys(List<byte> key)
    {
        if (key.Count != 16)
            throw new ArgumentException("DEAL-128 requires a 16-byte (128-bit) key.");

        var (k1, k2, _) = SplitToTwoParts(key);

        List<RoundKey> roundKeys = new List<RoundKey>();
        Des des = new Des() { Key = k1 };

        roundKeys.Add(new RoundKey() { Key = des.EncryptBlock(k1) });
        roundKeys.Add(new RoundKey() { Key = des.EncryptBlock(XorTwoParts(k2, roundKeys[0].Key)) });
        
        
        roundKeys.Add(new RoundKey() { Key = des.EncryptBlock(
            XorTwoParts(k1, XorTwoParts(roundKeys[1].Key, [0x80, 0x00,0x00,0x00,0x00,0x00,0x00,0x00]))) });
        
        roundKeys.Add(new RoundKey() { Key = des.EncryptBlock(
            XorTwoParts(k2, XorTwoParts(roundKeys[2].Key, [0x40, 0x00,0x00,0x00,0x00,0x00,0x00,0x00]))) });
        
        roundKeys.Add(new RoundKey() { Key = des.EncryptBlock(
            XorTwoParts(k1, XorTwoParts(roundKeys[3].Key, [0x10, 0x00,0x00,0x00,0x00,0x00,0x00,0x00]))) });
        
        roundKeys.Add(new RoundKey() { Key = des.EncryptBlock(
            XorTwoParts(k2, XorTwoParts(roundKeys[4].Key, [0x01, 0x00,0x00,0x00,0x00,0x00,0x00,0x00]))) });
        
        return roundKeys;
    }
}

// deal128
public class Deal128() : 
    FeistelNetwork(new Deal128KeyExtension(), new Deal128EncryptionTransformation(), 6, LastSwap.NoSwap)
{

}