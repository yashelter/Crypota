using System.Buffers.Binary;
using Crypota.Interfaces;
using Crypota.Symmetric.Exceptions;

namespace Crypota.Symmetric.Twofish;
using static Crypota.CryptoMath.GaloisFieldTwoPowEight;

public class Twofish : ISymmetricCipher
{
    private byte poly = 0x69;

    public byte IrreduciblePolynom
    {
        get => poly;
        init
        {
            _keyExtension = new TwofishKeyExtension(value);
            poly = value;
        }
    }

    private readonly TwofishKeyExtension _keyExtension = new TwofishKeyExtension(0x69);

    private byte[][]? _sBoxes;

    private byte[]? _key;
    private uint[]? _rKeys;

    public byte[]? Key
    {
        get => _key;
        set
        {
            _key = value ?? throw new ArgumentNullException(nameof(Key));
            _rKeys = _keyExtension.GetRoundKeys(_key);
            _sBoxes = _keyExtension.SBoxes;
        }
    }

    private uint Rounds { get; } = 16;
    public int BlockSize { get; private init; }
    public int KeySize { get;  private init;}
    public EncryptionState? EncryptionState { get; set; } = null;

    public required int BlockSizeBits
    {
        get => BlockSize * 8;
        init => BlockSize = value / 8;
    }
    
    public required int KeySizeBits
    {
        get => KeySize * 8;
        init => KeySize = value / 8;
    }

    

    /// <summary>
    /// Функция g из алгоритма Twofish.
    /// </summary>
    /// <param name="x">32-битное входное слово.</param>
    /// <param name="sBoxes">
    /// Массив из 4 S-боксов, каждый — byte[256], key-dependent.
    /// sBoxes[0] применяется к старшему байту (x>>24), sBoxes[3] — к младшему (0xFF).
    /// </param>
    /// <exception cref="ArgumentException"></exception>
    /// <returns>32-битный результат g(x).</returns>
    private uint TwofishFunctionG(uint x)
    {
        if (_sBoxes == null || _sBoxes.Length != 4)
            throw new InvalidKeyException("sBoxes must be an array of 4 byte[256] tables" + nameof(_sBoxes));
        byte mod = IrreduciblePolynom;

        byte x0 = (byte)(x >> 24);
        byte x1 = (byte)(x >> 16);
        byte x2 = (byte)(x >> 8);
        byte x3 = (byte)(x);

        byte y0 = _sBoxes[0][x0];
        byte y1 = _sBoxes[1][x1];
        byte y2 = _sBoxes[2][x2];
        byte y3 = _sBoxes[3][x3];

        // MDS = [ 01 EF 5B 5B
        //         5B EF EF 01
        //         EF 5B 01 EF
        //         EF 01 EF 5B]

        byte z0 = (byte)(MultiplyPolynomsByMod(0x01, y0, mod) ^ MultiplyPolynomsByMod(0xEF, y1, mod)
                                                              ^ MultiplyPolynomsByMod(0x5B, y2, mod)
                                                              ^ MultiplyPolynomsByMod(0x5B, y3, mod)
            );
        byte z1 = (byte)(MultiplyPolynomsByMod(0x5B, y0, mod) ^ MultiplyPolynomsByMod(0xEF, y1, mod)
                                                              ^ MultiplyPolynomsByMod(0xEF, y2, mod)
                                                              ^ MultiplyPolynomsByMod(0x01, y3, mod)
            );
        byte z2 = (byte)(MultiplyPolynomsByMod(0xEF, y0, mod) ^ MultiplyPolynomsByMod(0x5B, y1, mod)
                                                              ^ MultiplyPolynomsByMod(0x01, y2, mod)
                                                              ^ MultiplyPolynomsByMod(0xEF, y3, mod)
            );
        byte z3 = (byte)(MultiplyPolynomsByMod(0xEF, y0, mod) ^ MultiplyPolynomsByMod(0x01, y1, mod)
                                                              ^ MultiplyPolynomsByMod(0xEF, y2, mod)
                                                              ^ MultiplyPolynomsByMod(0x5B, y3, mod)
            );

        return ((uint)z0 << 24) | ((uint)z1 << 16) | ((uint)z2 << 8) | z3;
    }

    /// <summary>
    /// Циклический сдвиг 32-битного слова влево на заданное число бит.
    /// </summary>
    private static uint Rol32(uint v, int shift) =>
        (v << shift) | (v >> (32 - shift));

    /// <summary>
    /// Функция F в Twofish:
    ///   T0 = G(R0)
    ///   T1 = G(ROL(R1, 8))
    ///   F0 = (T0 + T1 + K[2*r + 8]) mod 2^32
    ///   F1 = (T0 + 2*T1 + K[2*r + 9]) mod 2^32
    /// </summary>
    /// <param name="r0">Первое входное 32-битное слово.</param>
    /// <param name="r1">Второе входное слово.</param>
    /// <param name="r">Номер раунда (0-based).</param>
    /// <param name="_rKeys">
    /// Массив расширенных ключей, минимум длины 2*(numberOfRounds + 4).
    /// Значения K[2*r+8] и K[2*r+9] используются здесь.
    /// </param>
    /// <returns>Кортеж (F0, F1).</returns>
    public (uint F0, uint F1) TwofishFunctionF(uint r0, uint r1, int r)
    {
        if (_rKeys == null) throw new InvalidKeyException("Encryption key is not initialized");

        int k0Index = 2 * r + 8;
        int k1Index = k0Index + 1;
        if (k1Index >= _rKeys.Length)
            throw new InvalidKeyException("roundKeys array too small for this round" + nameof(_rKeys));

        uint T0 = TwofishFunctionG(r0);
        uint rotatedR1 = Rol32(r1, 8);
        uint T1 = TwofishFunctionG(rotatedR1);

        uint F0 = T0 + T1 + _rKeys[k0Index];
        uint F1 = T0 + (T1 << 1) + _rKeys[k1Index];

        return (F0, F1);
    }


    /// <summary>
    /// Шифрование одного блока (16 байт) алгоритмом Twofish.
    /// </summary>
    public void EncryptBlock(Span<byte> state)
    {
        if (state.Length != BlockSize)
            throw new ArgumentException($"Block size must be {BlockSize}", nameof(state));
        if (_rKeys == null)
            throw new InvalidKeyException("Encryption key is not initialized");

        uint[] R = new uint[4];

        for (int i = 0; i < 4; i++)
        {
            R[i] = BinaryPrimitives.ReadUInt32LittleEndian(state.Slice(i*4,4));

        }

        for (int i = 0; i < 4; i++)
        {
            R[i] ^= _rKeys[i];
        }

        // 3) Основной цикл из 16 раундов
        for (int r = 0; r < Rounds; r++)
        {
            (uint F0, uint F1) = TwofishFunctionF(R[0], R[1], r);

            uint temp2 = R[2] ^ F0;
            R[2] = (temp2 >> 1) | (temp2 << 31);

            uint temp3 = (R[3] << 1) | (R[3] >> 31);
            R[3] = temp3 ^ F1;

            if (r < Rounds - 1)
            {
                (R[0], R[2]) = (R[2], R[0]);
                (R[1], R[3]) = (R[3], R[1]);
            }
        }

        // 4) Output whitening (undo swap): Ci = R[(i+2)%4] ^ K[i+4]
        Span<byte> output = state;
        for (int i = 0; i < 4; i++)
        {
            uint val = R[(i + 2) % 4] ^ _rKeys[i + 4];
            BinaryPrimitives.WriteUInt32LittleEndian(state.Slice(i*4,4), val);

        }
    }

    public void DecryptBlock(Span<byte> state)
    {
        if (state.Length != BlockSize)
            throw new ArgumentException($"Block size must be {BlockSize}", nameof(state));
        if (_rKeys == null)
            throw new InvalidKeyException("Encryption key is not initialized");

        uint[] R = new uint[4];
        for (int i = 0; i < 4; i++)
            R[i] = BitConverter.ToUInt32(state.Slice(i * 4, 4));
        
        uint t0 = R[0] ^ _rKeys[4];
        uint t1 = R[1] ^ _rKeys[5];
        uint t2 = R[2] ^ _rKeys[6];
        uint t3 = R[3] ^ _rKeys[7];
        
        R[2] = t0;
        R[3] = t1;
        R[0] = t2;
        R[1] = t3;

        for (int r = (int)Rounds - 1; r >= 0; r--)
        {
            if (r < Rounds - 1)
            {
                (R[0], R[2]) = (R[2], R[0]);
                (R[1], R[3]) = (R[3], R[1]); 
            }

            (uint F0, uint F1) = TwofishFunctionF(R[0], R[1], r);

            R[2] = Rol32(R[2], 1) ^ F0;

            uint temp = R[3] ^ F1;
            R[3] = (temp >> 1) | (temp << 31);
        }
        
        for (int i = 0; i < 4; i++)
        {
            R[i] ^= _rKeys[i];
        }

        for (int i = 0; i < 4; i++)
        {
            BitConverter.GetBytes(R[i]).AsSpan().CopyTo(state.Slice(i * 4, 4));
        }
        
    }
    
    public object Clone()
    {
        var clone = new Twofish
        {
            IrreduciblePolynom = this.IrreduciblePolynom,
            BlockSizeBits = this.BlockSizeBits,
            KeySizeBits   = this.KeySizeBits
        };

        if (this.Key != null)
        {
            var keyCopy = new byte[this.Key.Length];
            Array.Copy(this.Key, keyCopy, keyCopy.Length);
            clone.Key = keyCopy;
        }

        if (this.EncryptionState != null)
        {
            clone.EncryptionState = new EncryptionState();
        }

        return clone;
    }
}