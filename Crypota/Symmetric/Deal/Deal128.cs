using Crypota.CryptoMath;
using Crypota.Interfaces;

namespace Crypota.Symmetric.Deal;
using static SymmetricUtils;

public class Deal128EncryptionTransformation : IEncryptionTransformation
{
    public void EncryptionTransformation(Span<byte> state, Span<byte> roundKey)
    {
        var key = new byte[roundKey.Length];
        roundKey.CopyTo(key);
        
        Des.Des des = new Des.Des() { Key = key};
        des.EncryptBlock(state);
    }

    public void DecryptionTransformation(Span<byte> state, Span<byte> roundKey)
    {
        EncryptionTransformation(state, roundKey);
    }
}


public class Deal128KeyExtension : IKeyExtension
{
    private static byte[] EncryptWithCopy(ISymmetricCipher encryptor, byte[] state)
    {
        byte[] buffer = new byte[state.Length];
        state.CopyTo(buffer, 0);
        encryptor.EncryptBlock(state);
        return buffer;
    }
    
    public Memory<byte>[] GetRoundKeys(byte[] key)
    {
        if (key.Length != 16)
            throw new ArgumentException("DEAL-128 requires a 16-byte (128-bit) key.");

        var (k1, k2) = SplitToTwoParts(key);

        Memory<byte>[] roundKeys = new Memory<byte>[6];
        Des.Des des = new Des.Des() { Key = k1 };

        roundKeys[0] = EncryptWithCopy(des, k1);
        roundKeys[1] = EncryptWithCopy(des, XorTwoPartsCopy(k2, roundKeys[0].ToArray()));
        
        for (int i = 2; i < 6; i++) {
            long constant = 1L << (64 - (1 << (i - 2)));
            byte[] crntI = BitConverter.GetBytes(constant);
            if (BitConverter.IsLittleEndian) 
            {
                Array.Reverse(crntI);
            }

            roundKeys[i] = XorTwoPartsCopy(k1, XorTwoPartsCopy(roundKeys[i-1].ToArray(), crntI));
        }
        
        return roundKeys;
    }
}

// deal128
public class Deal128 : FeistelNetwork
{
    public Deal128() : base(new Deal128KeyExtension(), new Deal128EncryptionTransformation())
    {
        Rounds = 6;
    }
    
    public override int BlockSize => 16;
    public override int KeySize => 16;
}