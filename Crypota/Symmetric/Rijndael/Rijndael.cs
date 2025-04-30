using Crypota.Interfaces;
using static Crypota.CryptoMath.GaloisFieldTwoPowEight;
namespace Crypota.Symmetric.Rijndael;

public class Rijndael : IKeyExtension, IEncryptionTransformation, ISymmetricCipher
{
    private readonly Lazy<(byte[] sBox, byte[] invSBox)> _sBoxes;
    private readonly Lazy<byte[][]> _rcon;
    
    public byte[]? Key { get; set; }

    private readonly int _keySize;
    private readonly int _blockSize;

    public required byte IrreduciblePolynom { get; init; }
    private readonly int _nb;
    private readonly int _nk;
    private int _nr;

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
            _nb = value / 32;
            _nr = Math.Max(_nk, _nb) + 6;

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
            _nk = value / 32;
            _nr = Math.Max(_nk, _nb) + 6;

        }
    }
    
    public Rijndael()
    {
        _sBoxes = new Lazy<(byte[] sBox, byte[] invSBox)>(GenerateSBoxes);
        _rcon = new Lazy<byte[][]>(GenerateRcon);
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
            state[i] = _sBoxes.Value.sBox[state[i]];
        }
    }
    
    private void InvertedSubBytes(byte[] state)
    {
        for (int i = 0; i < state.Length; i++)
        {
            state[i] = _sBoxes.Value.invSBox[state[i]];
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

    
    private void ShiftRows(byte[] state)
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
    
    private void InvertedShiftRows(byte[] state)
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
        for (int i = 0; i < _nb; i++)
        {
            var p = new PolynomialInGF(state[3 + 4*i], state[2 + 4*i], state[1 + 4*i], state[4*i]);
            p = MultiplicationPolynoms(p, cx, IrreduciblePolynom);
            (state[3 + 4*i], state[2 + 4*i], state[1 + 4*i], state[0 + 4*i]) = (p.k3, p.k2, p.k1, p.k0);
            
        } 
    }
    
    private void InvertedMixColumns(byte[] state)
    {
        var cx = PolynomialInGF.GetInvCx();
        for (int i = 0; i < _nb; i++)
        {
            var p = new PolynomialInGF(state[3 + 4*i], state[2 + 4*i], state[1 + 4*i], state[4*i]);
            p = MultiplicationPolynoms(p, cx, IrreduciblePolynom);
            (state[3 + 4*i], state[2 + 4*i], state[1 + 4*i], state[0 + 4*i]) = (p.k3, p.k2, p.k1, p.k0);
        } 
    }

    private void XorTwoArrays(byte[] a, byte[] b)
    {
        for (int i = 0; i < a.Length; i++)
        {
            a[i] = (byte) (a[i] ^ b[i]);
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

    private static void RotBytes(byte[] b)
    {
        var temp = b[0];
        b[0] = b[1];
        b[1] = b[2];
        b[2] = b[3];
        b[3] = temp;
    }

    private byte[][] GenerateRcon()
    {
        int size = (_nb * (_nr + 1) * 4);
        byte[][] result = new byte[size][];
        result[0] = new byte[4];
        result[1] = new byte[4];
        result[1][0] = 1;

        for (int i = 2; i < size; i++)
        {
            byte poly = result[i - 1][0];
            byte rci = MultiplyPolynomByXByMod(poly, IrreduciblePolynom);
            result[i] = new byte[4];
            result[i][0] = rci;
        }
        return result;
    }
    
    
    private byte[] KeyExpansion(byte[] key)
    {
        int keySizeBytes = _nk * 4;
        int expandedSizeWords = _nb * (_nr + 1); 
        int size = expandedSizeWords * 4; 
        
        byte[] expandedKey = new byte[size];
        
        Array.Copy(key, 0, expandedKey, 0, keySizeBytes);
        
        byte[] temp = new byte[4];
        byte[] w = new byte[4];

        for (int i = _nk; i < expandedSizeWords; i++)
        {
            int prevWordByteIndex = (i - 1) * 4;
            Array.Copy(expandedKey, prevWordByteIndex, temp, 0, 4);
            
            if (i % _nk == 0)
            {
                RotBytes(temp);
                SubBytes(temp);
                XorTwoArrays(temp, _rcon.Value[i / _nk]);
            } else if (_nk > 6 && i % _nk == 4)
            {
                SubBytes(temp);
            }
            Array.Copy(expandedKey, (i - _nk) * 4, w, 0, 4);
            XorTwoArrays(temp, w);
            
            Array.Copy(temp, 0, expandedKey, i * 4, 4);
        }
        
        return expandedKey;
    }
    
    
    public RoundKey[] GetRoundKeys(byte[] key)
    {
        int roundKeySizeBytes = _nb * 4;
        RoundKey[] result = new RoundKey[_nr + 1];
        var expandedKey = KeyExpansion(key);

        for (int i = 0; i <= _nr; i++) 
        {
            result[i] = new RoundKey() { Key = new byte[roundKeySizeBytes] };
            int sourceIndex = i * roundKeySizeBytes;
            Array.Copy(expandedKey, sourceIndex, result[i].Key!, 0, roundKeySizeBytes);
        }
        return result;
    }

    // Round
    public byte[] EncryptionTransformation(byte[] message, RoundKey roundKey)
    {
        SubBytes(message);
        ShiftRows(message);
        MixColumns(message);
        AddRoundKey(message, roundKey);
        return message;
    }

    public byte[] DecryptionTransformation(byte[] message, RoundKey roundKey)
    {
        AddRoundKey(message, roundKey);
        InvertedMixColumns(message);
        InvertedShiftRows(message);
        InvertedSubBytes(message);
        return message;
    }


    public byte[] FinalRound(byte[] message, RoundKey roundKey)
    {
        SubBytes(message);
        ShiftRows(message);
        AddRoundKey(message, roundKey);
        return message;
    }
    
    
    public byte[] InvertedFinalRound(byte[] message, RoundKey roundKey)
    {
        AddRoundKey(message, roundKey);
        InvertedShiftRows(message);
        InvertedSubBytes(message);
        return message;
    }
    
    
    public byte[] EncryptBlock(byte[] block)
    {
        if (Key is null)
        {
            throw new ArgumentException(nameof(Key));
        }
        var keys = GetRoundKeys(Key);
        AddRoundKey(block, keys[0]);
        for (int i = 1; i < _nr; i++)
        {
            EncryptionTransformation(block, keys[i]);
        }
        FinalRound(block, keys[_nr]);
        return block;
    }

    public byte[] DecryptBlock(byte[] block)
    {
        if (Key is null)
        {
            throw new ArgumentException(nameof(Key));
        }
        var keys = GetRoundKeys(Key);
        InvertedFinalRound(block, keys[_nr]);

        for (int i = _nr-1; i > 0; i--)
        {
            DecryptionTransformation(block, keys[i]);
        }
        AddRoundKey(block, keys[0]);

        return block;
    }


}