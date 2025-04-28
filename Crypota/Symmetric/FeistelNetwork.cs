using Crypota.Interfaces;

namespace Crypota.Symmetric;
using static SymmetricMath;



public class FeistelNetwork(IKeyExtension keyExtension, IEncryptionTransformation transformation,
    uint rounds = 16)
    : ISymmetricCipher
{
    public byte[]? Key { get; set; }

    private byte[] Network(RoundKey[] keys, byte[] block)
    {
        var (left, right) = SplitToTwoParts(block);
        
        for (int i = 0; i < rounds; i++)
        {
            var temp = transformation.EncryptionTransformation(right, keys[i]);
            var newLeft = XorTwoParts(ref temp, left);
            
            left = right;
            right = newLeft;
        }
        return MergeFromTwoParts(right, left);
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
        var rev = keys.Reverse().ToArray();
        
        return Network(rev, block);
    }

    public virtual int BlockSize => 0;
    public virtual int KeySize => 0;
}