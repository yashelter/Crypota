using Crypota.CryptoMath;
using Crypota.Interfaces;

namespace Crypota.Symmetric.Des;
using static SymmetricUtils;

public partial class Des : FeistelNetwork
{
    public Des() : base(new DesKeyExtension(), new DesRoundTransformation())
    {
        Rounds = 16;
    }
    
    public override int BlockSize => 8;
    public override int KeySize => 8;
    
    
    public override void EncryptBlock(Span<byte> state)
    {
        var ip = PermuteBits(state, Ip, 1);
        base.EncryptBlock(ip);
        var pi = PermuteBits(ip, Pi, 1);
        pi.CopyTo(state);
    }
    
    public override void DecryptBlock(Span<byte> state)
    {
        var ip = PermuteBits(state, Ip, 1);
        base.DecryptBlock(ip);
        var pi = PermuteBits(ip, Pi, 1);
        pi.CopyTo(state);
    }
}