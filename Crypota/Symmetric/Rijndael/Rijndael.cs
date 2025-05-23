using System.Buffers;
using System.Runtime.CompilerServices;
using Crypota.Interfaces;
using static Crypota.CryptoMath.GaloisFieldTwoPowEight;
namespace Crypota.Symmetric.Rijndael;

public class Rijndael : IKeyExtension, IEncryptionTransformation, ISymmetricCipher
{
    private readonly Lazy<(byte[] sBox, byte[] invSBox)> _sBoxes;
    private readonly Lazy<byte[][]> _rcon;
    
    private Memory<byte>[] _keys;

    private byte[] _key;
    public byte[]? Key
    {
        get => _key;
        set
        {
            _key = value ?? throw new ArgumentNullException(nameof(Key));
            _keys = GetRoundKeys(_key);
        }
    }


    private readonly int _keySizeBits;
    private readonly int _blockSizeBits;

    public required byte IrreduciblePolynom { get; init; }
    private readonly int _nb;
    private readonly int _nk;
    private int _nr;

    public required int BlockSizeBits
    {
        get => _blockSizeBits;
        init
        {
            if (value % 32 != 0 || value < 128 || value > 256)
            {
                throw new ArgumentOutOfRangeException(nameof(BlockSizeBits));
            }
            _blockSizeBits = value;
            BlockSize = _blockSizeBits / 8;
            _nb = value / 32;
            _nr = Math.Max(_nk, _nb) + 6;

        }
    }

    public required int KeySizeBits
    {
        get => _keySizeBits;
        init
        {
            if (value % 32 != 0 || value < 128 || value > 256)
            {
                throw new ArgumentOutOfRangeException(nameof(KeySizeBits));
            }

            _keySizeBits = value;
            KeySize = _keySizeBits / 8;
            _nk = value / 32;
            _nr = Math.Max(_nk, _nb) + 6;

        }
    }

    public int KeySize { get; private init; }
    public int BlockSize { get; private init;}
    
    public EncryptionState? EncryptionState { get; } = null;

    public Rijndael()
    {
        _sBoxes = new Lazy<(byte[] sBox, byte[] invSBox)>(GenerateSBoxes);
        _rcon = new Lazy<byte[][]>(GenerateRcon);
    }
    
    
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static byte RightCycleShift(byte a, int shift)
    {
        shift %= 8;
        return (byte) ((a >> shift) | (a << (8 - shift)));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]

    private static byte LeftCycleShift(byte b, int shift)
    {
        shift %= 8;
        return (byte)((b << shift) | (b >> (8 - shift)));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
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

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void SubBytes(Span<byte> state)
    {
        for (int i = 0; i < state.Length; i++)
        {
            state[i] = _sBoxes.Value.sBox[state[i]];
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void InvertedSubBytes(Span<byte> state)
    {
        for (int i = 0; i < state.Length; i++)
        {
            state[i] = _sBoxes.Value.invSBox[state[i]];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void ShiftRowCycleLeft(Span<byte> line, int shift)
    {
        shift = shift % line.Length;

        if (line.Length == 0 || shift == 0)
            return;

        byte[] temp = ArrayPool<byte>.Shared.Rent(shift);

        line.Slice(0, shift).CopyTo(temp);
        line.Slice(shift).CopyTo(line);
        temp.AsSpan(0, shift).CopyTo(line.Slice(line.Length - shift));

        ArrayPool<byte>.Shared.Return(temp);

    }




    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void ShiftRowCycleRight(Span<byte> line, int shift)
    {
        shift = shift % line.Length;
        
        if (line.Length == 0 || shift == 0)
            return;
        
        byte[] temp = ArrayPool<byte>.Shared.Rent(shift);
        
       
            line.Slice(line.Length - shift, shift).CopyTo(temp);
            line.Slice(0, line.Length - shift).CopyTo(line.Slice(shift));
            temp.AsSpan(0, shift).CopyTo(line.Slice(0, shift));
            
     
        ArrayPool<byte>.Shared.Return(temp);
        
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void ShiftRows(Span<byte> state)
    {
        int len = _nb;
        byte[] line = ArrayPool<byte>.Shared.Rent(len);

        for (int row = 1; row < 4; row++)
        {
            Span<byte> lineSpan = line.AsSpan(0, len);

            for (int col = 0; col < len; col++)
            {
                lineSpan[col] = state[row + 4 * col];
            }

            ShiftRowCycleLeft(lineSpan, row);

            for (int col = 0; col < len; col++)
            {
                state[row + 4 * col] = lineSpan[col];
            }
        }

        ArrayPool<byte>.Shared.Return(line);

    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void InvertedShiftRows(Span<byte> state)
    {
        int len = BlockSizeBits / 32;

        byte[] line = ArrayPool<byte>.Shared.Rent(len);

        for (int row = 1; row < 4; row++)
        {
            Span<byte> lineSpan = line.AsSpan(0, len);
            
            for (int col = 0; col < len; col++)
            {
                lineSpan[col] = state[row + 4 * col];
            }
            
            ShiftRowCycleRight(lineSpan, row);

            for (int col = 0; col < len; col++)
            {
                state[row + 4 * col] = lineSpan[col];
            }
        }
        ArrayPool<byte>.Shared.Return(line);

    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void MixColumns(Span<byte> state)
    {
        var cx = PolynomialInGf.GetCx();
        for (int i = 0; i < _nb; i++)
        {
            var p = new PolynomialInGf(state[3 + 4*i], state[2 + 4*i], state[1 + 4*i], state[4*i]);
            p = MultiplicationPolynoms(p, cx, IrreduciblePolynom);
            (state[3 + 4*i], state[2 + 4*i], state[1 + 4*i], state[0 + 4*i]) = (p.k3, p.k2, p.k1, p.k0);
        } 
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void InvertedMixColumns(Span<byte> state)
    {
        var cx = PolynomialInGf.GetInvCx();
        for (int i = 0; i < _nb; i++)
        {
            var p = new PolynomialInGf(state[3 + 4*i], state[2 + 4*i], state[1 + 4*i], state[4*i]);
            p = MultiplicationPolynoms(p, cx, IrreduciblePolynom);
            (state[3 + 4*i], state[2 + 4*i], state[1 + 4*i], state[0 + 4*i]) = (p.k3, p.k2, p.k1, p.k0);
        } 
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void XorTwoArrays(Span<byte> a, Span<byte> b)
    {
        for (int i = 0; i < a.Length; i++)
        {
            a[i] = (byte) (a[i] ^ b[i]);
        }
    }

    
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void AddRoundKey(Span<byte> state, Span<byte> roundKey)
    {
        
        for (int i = 0; i < state.Length; i++)
        {
            state[i] = (byte) (state[i] ^ roundKey[i]);
        }
    }
    
    
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void RotBytes(Span<byte> b)
    {
        var temp = b[0];
        b[0] = b[1];
        b[1] = b[2];
        b[2] = b[3];
        b[3] = temp;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
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
    
    
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
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
    
    
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Memory<byte>[] GetRoundKeys(byte[] key)
    {
        int roundKeySizeBytes = _nb * 4;
        Memory<byte>[] result = new Memory<byte>[_nr + 1];
        var expandedKey = KeyExpansion(key);

        for (int i = 0; i <= _nr; i++) 
        {
            int sourceIndex = i * roundKeySizeBytes;
            result[i] = new Memory<byte>(expandedKey, start: sourceIndex, length: roundKeySizeBytes);
        }
        
        return result;
    }

    // Round
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void EncryptionTransformation(Span<byte> state, Span<byte> roundKey)
    {
        SubBytes(state);
        ShiftRows(state);
        MixColumns(state);
        AddRoundKey(state, roundKey);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void DecryptionTransformation(Span<byte> state, Span<byte> roundKey)
    {
        AddRoundKey(state, roundKey);
        InvertedMixColumns(state);
        InvertedShiftRows(state);
        InvertedSubBytes(state);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void FinalRound(Span<byte> message, Span<byte> roundKey)
    {
        SubBytes(message);
        ShiftRows(message);
        AddRoundKey(message, roundKey);
    }
    
    
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void InvertedFinalRound(Span<byte> message, Span<byte> roundKey)
    {
        AddRoundKey(message, roundKey);
        InvertedShiftRows(message);
        InvertedSubBytes(message);
    }
    
    
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void EncryptBlock(Span<byte> state)
    {
        if (Key is null)
        {
            throw new ArgumentException(nameof(Key));
        }
        AddRoundKey(state, _keys[0].Span);
        for (int i = 1; i < _nr; i++)
        {
            EncryptionTransformation(state, _keys[i].Span);
        }
        FinalRound(state, _keys[_nr].Span);
    }

    
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void DecryptBlock(Span<byte> state)
    {
        if (Key is null)
        {
            throw new ArgumentException(nameof(Key));
        }
        InvertedFinalRound(state, _keys[_nr].Span);

        for (int i = _nr-1; i > 0; i--)
        {
            DecryptionTransformation(state, _keys[i].Span);
        }
        AddRoundKey(state, _keys[0].Span);
    }

    public virtual object Clone()
    {
        throw new NotImplementedException();
    }
}