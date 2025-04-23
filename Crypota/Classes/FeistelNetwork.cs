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
        
        var (left, right) = SplitToTwoParts(block);
        
        for (int i = 0; i < rounds; i++)
        {
            var temp = transformation.EncryptionTransformation(right, keys[i]);
            var newLeft = XorTwoParts(ref temp, left);
            
            left = right;
            right = newLeft;
        }
        return swap == LastSwap.Swap ? MergeFromTwoParts(right, left) : MergeFromTwoParts(left, right);
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