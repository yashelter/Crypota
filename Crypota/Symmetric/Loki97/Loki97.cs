using System.Buffers.Binary;
using System.Diagnostics;
using Crypota.Interfaces;

namespace Crypota.Symmetric.Loki97;

public class Loki97 : ISymmetricCipher
{
    public const int BLOCK_SIZE_BYTES = 16;
    private const int NUM_ROUNDS = 16;

    private byte[]? _key;
    private int _currentKeySizeInBytes;
    private RoundSubkeys[]? _roundKeySets;

    private static readonly byte[] S1_TABLE = new byte[1 << 13];
    private static readonly byte[] S2_TABLE = new byte[1 << 11];


    private static readonly int[] E_PERMUTATION_TABLE = new int[48]
    {
        31, 0, 1, 2, 3, 4, 3, 4, 5, 6, 7, 8,
        7, 8, 9, 10, 11, 12, 11, 12, 13, 14, 15, 16,
        15, 16, 17, 18, 19, 20, 19, 20, 21, 22, 23, 24,
        23, 24, 25, 26, 27, 28, 27, 28, 29, 30, 31, 0
    };
    
    public byte[]? Key
    {
        get => (byte[]?)_key?.Clone();
        set
        {
            if (value == null)
            {
                _key = null;
                _roundKeySets = null;
                _currentKeySizeInBytes = 0;
                return;
            }

            var keyCopy = (byte[])value.Clone();
            var keyExt = new Loki97KeyExtension(keyCopy);
            _roundKeySets = keyExt.GetRoundKeys();
            _key = keyCopy;
            _currentKeySizeInBytes = value.Length;
        }
    }
    
    static Loki97()
    {
        InitializeSBoxes();
    }

    private static uint ModPow(uint baseVal, uint exponent, uint modulus)
    {
        if (modulus == 0) throw new ArgumentOutOfRangeException(nameof(modulus), "Modulus cannot be zero.");
        if (modulus == 1) return 0; // All results are 0 mod 1

        ulong result = 1;
        ulong b = baseVal % modulus;
        ulong exp = exponent;

        while (exp > 0)
        {
            if ((exp % 2) == 1) result = (result * b) % modulus;
            b = (b * b) % modulus;
            exp /= 2;
        }

        return (uint)result;
    }


    private static void InitializeSBoxes()
    {
        S1_TABLE[0] = 0;
        for (uint i = 1; i < S1_TABLE.Length; i++)
        {
            S1_TABLE[i] = (byte)(ModPow(i, 3, 8191) & 0xFF);
        }
        
        S2_TABLE[0] = 0;
        for (uint i = 1; i < S2_TABLE.Length; i++)
        {
            S2_TABLE[i] = (byte)(ModPow(i, 5, 2047) & 0xFF);
        }
    }

    

    public int BlockSize => BLOCK_SIZE_BYTES;
    public int KeySize => _currentKeySizeInBytes;
    
    public EncryptionState? EncryptionState => null;

    public void EncryptBlock(Span<byte> state)
    {
        if (state.Length != BLOCK_SIZE_BYTES)
            throw new ArgumentException($"State length must be {BLOCK_SIZE_BYTES} bytes.", nameof(state));
        if (_roundKeySets == null || _key == null)
            throw new InvalidOperationException("Key must be set before encryption.");

        ulong left = BinaryPrimitives.ReadUInt64BigEndian(state.Slice(0, 8));
        ulong right = BinaryPrimitives.ReadUInt64BigEndian(state.Slice(8, 8));

        for (int i = 0; i < NUM_ROUNDS; i++)
        {
            RoundSubkeys currentRoundKeys = _roundKeySets[i];
            ulong fResult = F(right, currentRoundKeys.Ka, currentRoundKeys.Kb, currentRoundKeys.Kc);

            ulong tempL = left;
            left = right;
            right = tempL ^ fResult;
        }

        BinaryPrimitives.WriteUInt64BigEndian(state.Slice(0, 8), left);
        BinaryPrimitives.WriteUInt64BigEndian(state.Slice(8, 8), right);
    }

    public void DecryptBlock(Span<byte> state)
    {
        if (state.Length != BLOCK_SIZE_BYTES)
            throw new ArgumentException($"State length must be {BLOCK_SIZE_BYTES} bytes.", nameof(state));
        if (_roundKeySets == null || _key == null)
            throw new InvalidOperationException("Key must be set before decryption.");

        ulong left = BinaryPrimitives.ReadUInt64BigEndian(state.Slice(0, 8));
        ulong right = BinaryPrimitives.ReadUInt64BigEndian(state.Slice(8, 8));
        
        for (int i = NUM_ROUNDS - 1; i >= 0; i--)
        {
            RoundSubkeys currentRoundKeys = _roundKeySets[i];
            ulong fResult = F(left, currentRoundKeys.Ka, currentRoundKeys.Kb, currentRoundKeys.Kc);

            ulong tempR = right;
            right = left;
            left = tempR ^ fResult;
        }

        BinaryPrimitives.WriteUInt64BigEndian(state.Slice(0, 8), left);
        BinaryPrimitives.WriteUInt64BigEndian(state.Slice(8, 8), right);
    }
    
    private ulong F(ulong B, ulong Ka, ulong Kb, ulong Kc)
    {
        ulong t0 = B;
        ulong t1 = G(t0, Ka);
        ulong t2 = G(t1, Kb);
        ulong t3 = G(t2, Kc);
        return t3;
    }


    private ulong G(ulong A, ulong Ki)
    {
        uint AL = (uint)(A >> 32); // Left 32 bits of A
        uint AR = (uint)(A & 0xFFFFFFFFUL); // Right 32 bits of A

        ulong expandedAR = E_Permute(AR); // AR expanded to 48 bits
        ulong ki_48 = Ki & 0xFFFFFFFFFFFFUL; // Low 48 bits of the 64-bit subkey Ki

        ulong y = expandedAR ^ ki_48; // 48-bit result
        
        uint y0_val = (uint)((y >> 35) & 0x1FFFUL);
        uint y1_val = (uint)((y >> 24) & 0x07FFUL);
        uint y2_val = (uint)((y >> 11) & 0x1FFFUL);
        uint y3_val = (uint)(y & 0x07FFUL);
        
        byte s1_y0_out = S1_TABLE[y0_val];
        byte s2_y1_out = S2_TABLE[y1_val];
        byte s1_y2_out = S1_TABLE[y2_val];
        byte s2_y3_out = S2_TABLE[y3_val];
        
        uint sp_result = ((uint)s1_y0_out << 24) |
                         ((uint)s2_y1_out << 16) |
                         ((uint)s1_y2_out << 8) |
                         s2_y3_out;
        uint g_L_new = AL ^ sp_result;
        return ((ulong)g_L_new << 32) | AR;
    }
    
    private static ulong E_Permute(uint val)
    {
        ulong expandedValue = 0;
        for (int i = 0; i < 48; i++)
        {
            if (((val >> E_PERMUTATION_TABLE[i]) & 1U) != 0)
            {
                expandedValue |= (1UL << i);
            }
        }

        return expandedValue;
    }
    
    public object Clone()
    {
        Loki97 clone = new Loki97();
        if (_key != null)
        {
            clone.Key = this.Key;
        }
        return clone;
    }
}