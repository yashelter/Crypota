using Crypota.Symmetric.Exceptions;
using static Crypota.CryptoMath.GaloisFieldTwoPowEight;

namespace Crypota.Symmetric.Twofish;

public class TwofishKeyExtension(byte mod)
{
    private const int Rounds = 16;
    private const int BlockWords = 4;

    private static readonly byte[,] MdsMatrix = new byte[4, 4]
    {
        { 0x01, 0xEF, 0x5B, 0x5B },
        { 0x5B, 0xEF, 0xEF, 0x01 },
        { 0xEF, 0x5B, 0x01, 0xEF },
        { 0xEF, 0x01, 0xEF, 0x5B }
    };

    private static readonly byte[,] RsMatrix = new byte[4, 8]
    {
        { 0x01, 0xA4, 0x55, 0x87, 0x5A, 0x58, 0xDB, 0x9E },
        { 0xA4, 0x56, 0x82, 0xF3, 0x1E, 0xC6, 0x68, 0xE5 },
        { 0x02, 0xA1, 0xFC, 0xC1, 0x47, 0xAE, 0x3D, 0x19 },
        { 0xA4, 0x55, 0x87, 0x5A, 0x58, 0xDB, 0x9E, 0x03 }
    };
    
    private static uint Rol32(uint v, int shift) =>
        (v << shift) | (v >> (32 - shift));
    
    public byte[][] SBoxes { get; private set; } = new byte[4][];


    
    public uint[] GetRoundKeys(byte[] key)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        if (key.Length != 16 && key.Length != 24 && key.Length != 32)
            throw new InvalidKeyException("Key length must be 16, 24 or 32 bytes.");

        int keyWords = key.Length / 4;
        uint[] Me = new uint[keyWords / 2];
        uint[] Mo = new uint[keyWords / 2];

        for (int i = 0; i < keyWords / 2; i++)
        {
            Me[i] = BitConverter.ToUInt32(key, 8 * i);
            Mo[i] = BitConverter.ToUInt32(key, 8 * i + 4);
        }
        
        byte[] sKey = new byte[4 * 256];
        GenerateSKey(key, sKey);
        BuildSBoxes(sKey);

        uint[] roundKeys = new uint[2 * (Rounds + BlockWords)];

        for (int i = 0; i < Rounds + BlockWords; i++)
        {
            uint idx = (uint)i;
            
            uint A = H(2u * idx * 0x01010101u, Me);
            uint B = Rol32(H((2u * idx + 1u) * 0x01010101u, Mo), 8);
            roundKeys[2 * i] = A + B;
            roundKeys[2 * i + 1] = Rol32(A + 2 * B, 9);
        }

        return roundKeys;
    }
    
    
    private void GenerateSKey(byte[] key, byte[] sKeyOut)
    {
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                byte result = 0;
                for (int k = 0; k < 8; k++)
                {
                    int keyIndex = (4 * j + k) % key.Length; 
                    result ^= MultiplyPolynomsByMod(RsMatrix[i, k], key[keyIndex], mod);

                }
                sKeyOut[i * 4 + j] = result;
            }
        }
    }


    private void BuildSBoxes(byte[] sKey)
    {
        for (int box = 0; box < 4; box++)
        {
            SBoxes[box] = new byte[256];
            for (int x = 0; x < 256; x++)
            {
                byte y = QPermute(box, (byte)x);
                y ^= sKey[box];
                SBoxes[box][x] = MdsTransform(box, y);
            }
        }
    }
    
    private static readonly byte[] Q0_t0 = { 0x8, 0x1, 0x7, 0xD, 0x6, 0xF, 0x3, 0x2, 0x0, 0xB, 0x5, 0x9, 0xE, 0xC, 0xA, 0x4 };
    private static readonly byte[] Q0_t1 = { 0xE, 0xC, 0xB, 0x8, 0x1, 0x2, 0x3, 0x5, 0xF, 0x4, 0xA, 0x6, 0x7, 0x0, 0x9, 0xD };
    private static readonly byte[] Q0_t2 = { 0xB, 0xA, 0x5, 0xE, 0x6, 0xD, 0x9, 0x0, 0xC, 0x8, 0xF, 0x3, 0x2, 0x4, 0x7, 0x1 };
    private static readonly byte[] Q0_t3 = { 0xD, 0x7, 0xF, 0x4, 0x1, 0x2, 0x6, 0xE, 0x9, 0xB, 0x3, 0x0, 0x8, 0x5, 0xC, 0xA };

    private static readonly byte[] Q1_t0 = { 0x2, 0x8, 0xB, 0xD, 0xF, 0x7, 0x6, 0xE, 0x3, 0x1, 0x9, 0x4, 0x0, 0xA, 0xC, 0x5 };
    private static readonly byte[] Q1_t1 = { 0x1, 0xE, 0x2, 0xB, 0x4, 0xC, 0x3, 0x7, 0x6, 0xD, 0xA, 0x5, 0xF, 0x9, 0x0, 0x8 };
    private static readonly byte[] Q1_t2 = { 0x4, 0xC, 0x7, 0x5, 0x1, 0x6, 0x9, 0xA, 0x0, 0xE, 0xD, 0x8, 0x2, 0xB, 0x3, 0xF };
    private static readonly byte[] Q1_t3 = { 0xB, 0x9, 0x5, 0x1, 0xC, 0x3, 0xD, 0xE, 0x6, 0x4, 0x7, 0xF, 0x2, 0x0, 0x8, 0xA };

    private static byte Ror4(byte x, int shift) => (byte)(((x >> shift) | (x << (4 - shift))) & 0xF);


    private static byte QPermute(int box, byte x)
    {
        int a0 = (x >> 4) & 0xF;
        int b0 = x & 0xF;

        int a1 = a0 ^ b0;
        int b1 = a0 ^ Ror4((byte)b0, 1) ^ ((8 * a0) & 0xF);

        byte[] t0 = box == 0 ? Q0_t0 : Q1_t0;
        byte[] t1 = box == 0 ? Q0_t1 : Q1_t1;
        byte[] t2 = box == 0 ? Q0_t2 : Q1_t2;
        byte[] t3 = box == 0 ? Q0_t3 : Q1_t3;
        int a2 = t0[a1];
        int b2 = t1[b1];

        int a3 = a2 ^ b2;
        int b3 = a2 ^ Ror4((byte)b2, 1) ^ ((8 * a2) & 0xF);
        int a4 = t2[a3];
        int b4 = t3[b3];
        
        return (byte)((b4 << 4) | a4);
    }


    private byte MdsTransform(int row, byte y)
    {
        byte result = 0;
        for (int col = 0; col < 4; col++)
        {
            result ^= MultiplyPolynomsByMod(MdsMatrix[row, col], y, mod);
        }
        return result;
    }
    
    private uint H(uint X, uint[] L)
    {
        byte[] bytes = BitConverter.GetBytes(X);
        uint result = 0;
        for (int i = 0; i < L.Length; i++)
        {
            if (i >= 4) break;
            byte b = bytes[i];
            byte y = SBoxes[i][b];
            result |= (uint)y << (8 * i);
        }

        return result;
    }
}
