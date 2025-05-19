using Crypota.CryptoMath;
using Crypota.Symmetric.Exceptions;

namespace Crypota.Symmetric.Des;

using static SymmetricUtils;

public class DesKeyExtension : IKeyExtension
{
    public bool DoCheckKey { get; set; } = false;

    #region Tables
    
    // Таблица PC1 (Первоначальная перестановка ключа)
    private static readonly int[] Pc1 =
    [
        57, 49, 41, 33, 25, 17,  9,
        1, 58, 50, 42, 34, 26, 18,
        10,  2, 59, 51, 43, 35, 27,
        19, 11,  3, 60, 52, 44, 36,
    
        63, 55, 47, 39, 31, 23, 15,
        7, 62, 54, 46, 38, 30, 22,
        14,  6, 61, 53, 45, 37, 29,
        21, 13,  5, 28, 20, 12,  4
    ];

    // Таблица PC2 (Сжатие ключа до 48 бит)
    private static readonly int[] Pc2 =
    [
        14, 17, 11, 24,  1,  5,
        3, 28, 15,  6, 21, 10,
        23, 19, 12,  4, 26,  8,
        16,  7, 27, 20, 13,  2,
        41, 52, 31, 37, 47, 55,
        30, 40, 51, 45, 33, 48,
        44, 49, 39, 56, 34, 53,
        46, 42, 50, 36, 29, 32
    ];


    private static readonly int[]  ShiftBits = 
    [ 
     /* 1   2   3   4   5   6   7   8   9  10  11  12  13  14  15  16 */
        1,  1,  2,  2,  2,  2,  2,  2,  1,  2,  2,  2,  2,  2,  2,  1
    ];
    
    #endregion
    
    public Memory<byte>[] GetRoundKeys(byte[] key)
    {
        if (key.Length != 8)
            throw new InvalidKeyException("Key must be 8 bytes (64 bits [56]).");

        if (DoCheckKey && !CheckKey(key))
            throw new InvalidKeyException("Key not corresponds to rules of key");
        
        
        var permutedKey = PermuteBits(key, Pc1, 1);

        int c = 0, d;

        for (int i = 0; i < 3; i++) {
            c = (c << 8) | (permutedKey[i] & 0xFF);
        }

        c = (c << 4) | ((permutedKey[3] & 0xF0) >> 4);

        d = (permutedKey[3] & 0x0F);
        for (int i = 4; i < 7; i++) {
            d = (d << 8) | (permutedKey[i] & 0xFF);
        }

        Memory<byte>[] roundKeys = new Memory<byte>[16];
        
        for (int i = 0; i < 16; i++)
        {

            c = CycleLeftShift28(c, ShiftBits[i]); // 1 or 2
            d = CycleLeftShift28(d, ShiftBits[i]); // 1 or 2

            long combined = (((long) c) << 28) | (uint)(d & 0x0FFFFFFF);
            byte[] cd = new byte[7];
            for (int j = 0; j < 7; j++) {
                cd[j] = (byte) ((combined >> ((6 - j) * 8)) & 0xFF);
            }
            
            var rKey = PermuteBits(cd, Pc2, 1);
            roundKeys[i] = new Memory<byte>(rKey);
        }
        return roundKeys;
    }

    private static int CycleLeftShift28(int value, int shift) {
        return ((value << shift) | (value >> (28 - shift))) & 0x0FFFFFFF;
    }

    private static byte XorFirst7Bits(byte value)
    {
        byte relevantBits = (byte)(value & 0xFE);
        relevantBits ^= (byte)(relevantBits >> 4);
        relevantBits ^= (byte)(relevantBits >> 2);
        relevantBits ^= (byte)(relevantBits >> 1);

        return relevantBits;
    }

    private static bool CheckKey(Span<byte> key)
    {
        foreach (var t in key)
        {
            if (XorFirst7Bits(t) == (t & 0x01))
            {
                return false;
            }
        }

        return true;
    }
}
