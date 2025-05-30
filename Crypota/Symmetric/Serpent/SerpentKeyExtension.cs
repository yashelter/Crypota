using System.Buffers.Binary;

namespace Crypota.Symmetric.Serpent;

public class SerpentKeyExtension
{
    private const uint PHI = 0x9E3779B9u;
    private const int TOTAL_ROUNDS_FOR_KEYS = 32;
    private readonly byte[] _userKey;

    private uint[][]? _generatedRoundKeys = null;

    public SerpentKeyExtension(byte[] key)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));

        if (key.Length != 16 && key.Length != 24 && key.Length != 32)
        {
            throw new ArgumentException("Key length must be 16, 24, or 32 bytes (128, 192, or 256 bits).", nameof(key));
        }

        _userKey = (byte[])key.Clone(); 
    }

    private static uint RotateLeft(uint value, int shift)
    {
        return (value << shift) | (value >> (32 - shift));
    }

    private void ApplySBoxToKeyWords(int sboxNum, uint[] words)
    {
        for (int i = 0; i < 4; i++)
        {
            uint currentWord = words[i];
            uint newWord = 0;
            for (int j = 0; j < 8; j++)
            {
                uint nibble = (currentWord >> (j * 4)) & 0xF;
                uint transformedNibble = Serpent.S_BOXES[sboxNum][(int)nibble];
                newWord |= (transformedNibble << (j * 4));
            }

            words[i] = newWord;
        }
    }

    public uint[][] GetRoundKeys()
    {
        if (_generatedRoundKeys != null) return _generatedRoundKeys;

        uint[] w = new uint[132];

        byte[] paddedKeyContainer = new byte[32];
        Array.Copy(_userKey, 0, paddedKeyContainer, 0, _userKey.Length);

        if (_userKey.Length < 32)
        {
            paddedKeyContainer[_userKey.Length] = 0x80;
        }

        for (int i = 0; i < 8; i++)
        {
            w[i] = BinaryPrimitives.ReadUInt32LittleEndian(paddedKeyContainer.AsSpan(i * 4));
        }

        for (int i = 8; i < 132; i++)
        {
            uint term = w[i - 8] ^ w[i - 5] ^ w[i - 3] ^ w[i - 1] ^ PHI ^ (uint)i;
            w[i] = RotateLeft(term, 11);
        }
        
        _generatedRoundKeys = new uint[TOTAL_ROUNDS_FOR_KEYS + 1][];

        for (int i = 0; i <= TOTAL_ROUNDS_FOR_KEYS; i++)
        {
            _generatedRoundKeys[i] = new uint[4];
            _generatedRoundKeys[i][0] = w[4 * i];
            _generatedRoundKeys[i][1] = w[4 * i + 1];
            _generatedRoundKeys[i][2] = w[4 * i + 2];
            _generatedRoundKeys[i][3] = w[4 * i + 3];

            int sboxIndex = (3 - (i % 8) + 8) % 8;
            ApplySBoxToKeyWords(sboxIndex, _generatedRoundKeys[i]);
        }

        return _generatedRoundKeys;
    }
}