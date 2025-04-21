namespace Crypota.Classes;
using static CryptoAlgorithms;

public enum LastSwap
{
    NoSwap = 0,
    Swap = 1,
}

public class FeistelNetwork(IKeyExtension keyExtension, IEncryptionTransformation transformation,
    uint rounds = 16, LastSwap swap = LastSwap.Swap)
    : ISymmetricCipher
{
    public byte[]? Key { get; set; }

    private byte[] Network(RoundKey[] keys, byte[] block)
    {
        // int rounds = keys.Count;
        
        var (left, right, bitCnt) = SplitToTwoParts(block);
        
        for (int i = 0; i < rounds; i++)
        {
            var newLeft = XorTwoParts(transformation.EncryptionTransformation(right, keys[i]), left);
            
            left = right;
            right = newLeft;
        }
        return swap == LastSwap.Swap ? MergeTwoParts(right, left, bitCnt) : MergeTwoParts(left, right, bitCnt);
    }


    public virtual byte[] EncryptBlock(byte[] block)
    {
        if (Key is null) throw new ArgumentException("You should set-up key before encryption");
        
        return Network(keyExtension.GetRoundKeys(Key), block);
    }

    public virtual byte[] DecryptBlock(byte[] block)
    {
        if (Key is null) throw new ArgumentException("You should set-up key before encryption");
        var keys = keyExtension.GetRoundKeys(Key);
        keys.Reverse();
        
        return Network(keys, block);
    }
}