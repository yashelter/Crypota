using Crypota.Interfaces;
using static Crypota.CryptoMath.GaloisFieldTwoPowEight;
namespace Crypota.Symmetric.Rijndael;

public class Rijindael : IKeyExtension, IEncryptionTransformation, ISymmetricCipher
{
    public readonly Lazy<(byte[] sBox, byte[] invSBox)> SBoxes;
    
    public byte[]? Key { get; set; }

    private readonly int _keySize;
    private readonly int _blockSize;

    public required byte IrreduciblePolynom { get; init; }
    private readonly int _Nb;
    private readonly int _Nr;

    public required int BlockSize
    {
        get => _blockSize;
        init
        {
            if (value % 32 != 0 || value < 128 || value > 256)
            {
                throw new ArgumentOutOfRangeException(nameof(BlockSize));
            }
            _blockSize = value;
            _Nb = value / 32;
        }
    }

    public required int KeySize
    {
        get => _keySize;
        init
        {
            if (value % 32 != 0 || value < 128 || value > 256)
            {
                throw new ArgumentOutOfRangeException(nameof(KeySize));
            }

            _keySize = value;
            _Nr = value / 32;
        }
    }
    
    public Rijindael()
    {
        SBoxes = new Lazy<(byte[] sBox, byte[] invSBox)>(GenerateSBoxes);
    }
    
    
    private static byte RightCycleShift(byte a, int shift)
    {
        shift %= 8;
        return (byte) ((a >> shift) | (a << (8 - shift)));
    }
    
    private static byte LeftCycleShift(byte b, int shift)
    {
        shift %= 8;
        return (byte)((b << shift) | (b >> (8 - shift)));
    }

    private byte AffineTransformation(byte b)
    {
        return (byte)
            ((b ^  LeftCycleShift(b, 4)) ^ 
             (LeftCycleShift(b, 3) ^ LeftCycleShift(b, 2)) ^
             LeftCycleShift(b, 1));
    }

    private (byte[], byte[]) GenerateSBoxes()
    {
        byte[] sBox = new byte[256];
        byte[] invSBox = new byte[256];

        for (int i = 0; i < 256; i++)
        {
            byte rev = GetOppositePolynom((byte)i, IrreduciblePolynom);
            byte t = AffineTransformation(rev);
            t ^= 0x63;
            
            sBox[i] = t;
            invSBox[t] = (byte)i;
        } 
        
        return (sBox, invSBox);
    }

    private void SubBytes(byte[] state)
    {
        for (int i = 0; i < state.Length; i++)
        {
            state[i] = SBoxes.Value.sBox[state[i]];
        }
    }
    
    private void InvertedSubBytes(byte[] state)
    {
        for (int i = 0; i < state.Length; i++)
        {
            state[i] = SBoxes.Value.invSBox[state[i]];
        }
    }
    
    private static void ShiftRowCycleLeft(ref byte[] line, int shift)
    {
        shift = shift % line.Length;

        if (line.Length == 0 || shift == 0)
            return;
        
        byte[] temp = new byte[shift];
        Array.Copy(line, 0, temp, 0, shift);
        Array.Copy(line, shift, line, 0, line.Length - shift);

        Array.Copy(temp, 0, line, line.Length - shift, shift);
    }

    private static void ShiftRowCycleRight(ref byte[] line, int shift)
    {
        shift = shift % line.Length;
        
        if (line.Length == 0 || shift == 0)
            return;
        
        byte[] temp = new byte[shift];
        Array.Copy(line, line.Length - shift, temp, 0, shift);
        Array.Copy(line, 0, line, shift, line.Length - shift);
        
        Array.Copy(temp, 0, line, 0, shift);
    }

    
    private void ShiftRows(ref byte[] state)
    {
        int len = BlockSize / 32;

        for (int row = 1; row < 4; row++)
        {
            byte[] line = new byte[len];
            
            for (int col = 0; col < len; col++)
            {
                line[col] = state[row + 4 * col];
            }
            
            ShiftRowCycleLeft(ref line, row);

            for (int col = 0; col < len; col++)
            {
                state[row + 4 * col] = line[col];
            }
        }
    }
    
    private void InvertedShiftRows(ref byte[] state)
    {
        int len = BlockSize / 32;

        for (int row = 1; row < 4; row++)
        {
            byte[] line = new byte[len];
            
            for (int col = 0; col < len; col++)
            {
                line[col] = state[row + 4 * col];
            }
            
            ShiftRowCycleRight(ref line, row);

            for (int col = 0; col < len; col++)
            {
                state[row + 4 * col] = line[col];
            }
        }
    }

    private void MixColumns(byte[] state)
    {
        var cx = PolynomialInGF.GetCx();
        for (int i = 0; i < _Nb; i++)
        {
            var p = new PolynomialInGF(state[3 + 4*i], state[2 + 4*i], state[1 + 4*i], state[4*i]);
            p = MultiplicationPolynoms(p, cx, IrreduciblePolynom);
            (state[3 + 4*i], state[2 + 4*i], state[1 + 4*i], state[0 + 4*i]) = (p.k3, p.k2, p.k1, p.k0);
            
        } 
    }
    
    private void InvertedMixColumns(byte[] state)
    {
        var cx = PolynomialInGF.GetInvCx();
        for (int i = 0; i < _Nb; i++)
        {
            var p = new PolynomialInGF(state[3 + 4*i], state[2 + 4*i], state[1 + 4*i], state[4*i]);
            p = MultiplicationPolynoms(p, cx, IrreduciblePolynom);
            (state[3 + 4*i], state[2 + 4*i], state[1 + 4*i], state[0 + 4*i]) = (p.k3, p.k2, p.k1, p.k0);
        } 
    }

    private void AddRoundKey(byte[] state, RoundKey roundKey)
    {
        if (roundKey.Key is null)
        {
            throw new ArgumentNullException(nameof(roundKey));
        }
        
        for (int i = 0; i < state.Length; i++)
        {
            state[i] = (byte) (state[i] ^ roundKey.Key[i]);
        }
    }
    
    public RoundKey[] GetRoundKeys(byte[] key)
    {
        throw new NotImplementedException();
    }

    public byte[] EncryptionTransformation(byte[] message, RoundKey roundKey)
    {
        throw new NotImplementedException();
    }

    public byte[] EncryptBlock(byte[] block)
    {
        throw new NotImplementedException();
    }

    public byte[] DecryptBlock(byte[] block)
    {
        throw new NotImplementedException();
    }


}