namespace Crypota.Classes.DES;
using static CryptoAlgorithms;

public class Deal128EncryptionTransformation : IEncryptionTransformation
{
    public List<byte> EncryptionTransformation(List<byte> message, RoundKey roundKey)
    {
        Des des = new Des() { Key = roundKey.Key};
        return des.EncryptBlock(message);
    }
}


public class Deal128KeyExtension : IKeyExtension
{
    public List<RoundKey> GetRoundKeys(List<byte> key)
    {
        var (l, r, bits) = SplitToTwoParts(key);
        var result = new List<RoundKey>
        {
            new() { Key = [..l] },
            new() { Key = [..r] },
            new() { Key = [..l] },
            new() { Key = [..r] },
            new() { Key = [..l] },
            new() { Key = [..r] }
        };
        return result;
    }
}

// deal128
public class Deal128() : 
    FeistelNetwork(new Deal128KeyExtension(), new Deal128EncryptionTransformation(), 6, LastSwap.NoSwap)
{

}