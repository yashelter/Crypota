using Crypota.CryptoMath;
using Crypota.Interfaces;

namespace Crypota.Symmetric;
using static SymmetricUtils;



public class FeistelNetwork(IKeyExtension keyExtension, IEncryptionTransformation transformation)
    : ISymmetricCipher
{
    protected uint Rounds { get; init; }
    public byte[]? Key { get; set; }

    private byte[] Network(Memory<byte>[] keys, byte[] block)
    {
        var (left, right) = SplitToTwoParts(block);
        
        for (int i = 0; i < Rounds; i++)
        {
            var originalR = right.ToArray();
            
            transformation.EncryptionTransformation(right, keys[i].Span);
            SymmetricUtils.XorInPlace(left, right);
            
            var nextR = left;
            left = originalR;
            right = nextR; 
        }
        return MergeFromTwoParts(right, left);
    }


    public virtual void EncryptBlock(Span<byte> state)
    {
        if (Key is null) throw new ArgumentException("You should set-up key before encryption");
        
        var tmp = Network(keyExtension.GetRoundKeys(Key), state.ToArray());
        tmp.CopyTo(state);
    }

    public virtual void DecryptBlock(Span<byte> state)
    {
        if (Key is null) throw new ArgumentException("You should set-up key before encryption");
        var keys = keyExtension.GetRoundKeys(Key);
        var rev = keys.Reverse().ToArray();
        
        var tmp = Network(rev, state.ToArray());
        tmp.CopyTo(state);
    }

    public virtual int BlockSize => 0;
    public virtual int KeySize => 0;
    public EncryptionState? EncryptionState { get; } = null;
    public virtual object Clone()
    {
        throw new NotImplementedException();
    }
}