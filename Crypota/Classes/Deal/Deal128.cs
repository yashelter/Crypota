namespace Crypota.Classes.DES;
using static CryptoAlgorithms;

public class Deal128EncryptionTransformation : IEncryptionTransformation
{
    public byte[] EncryptionTransformation(byte[] message, RoundKey roundKey)
    {
        Des des = new Des() { Key = (byte[]) (roundKey.Key).Clone() };
        return des.EncryptBlock(message);
    }
}


public class Deal128KeyExtension : IKeyExtension
{
    public RoundKey[] GetRoundKeys(byte[] key)
    {
        if (key.Length != 16)
            throw new ArgumentException("DEAL-128 requires a 16-byte (128-bit) key.");

        var (k1, k2) = SplitToTwoParts(key);

        RoundKey[] roundKeys = new RoundKey[6];
        Des des = new Des() { Key = k1 };

        roundKeys[0] = (new RoundKey() { Key = des.EncryptBlock(k1) });
        roundKeys[1] = (new RoundKey() { Key = des.EncryptBlock(XorTwoPartsCopy(k2, roundKeys[0].Key)) });
        
        for (int i = 2; i < 6; i++) {
            long constant = 1L << (64 - (1 << (i - 2)));
            byte[] crntI = BitConverter.GetBytes(constant);
            if (BitConverter.IsLittleEndian) 
            {
                Array.Reverse(crntI);
            }

            roundKeys[i] = (new RoundKey() { Key = des.EncryptBlock(
                XorTwoPartsCopy(k1, XorTwoPartsCopy(roundKeys[i-1].Key, crntI))) });
        }
        
        return roundKeys;
    }
}

// deal128
public class Deal128() : 
    FeistelNetwork(new Deal128KeyExtension(), new Deal128EncryptionTransformation(), 6)
{
    public override int BlockSize => 16;
    public override int KeySize => 16;
}