using System.Buffers.Binary;
using System.Diagnostics;

namespace Crypota.Symmetric.Serpent;

public class Serpent : Crypota.Interfaces.ISymmetricCipher
{
    public const int BLOCK_SIZE_BYTES = 16;
    private const int NUM_ROUNDS = 32;

    private byte[]? _key;
    private int _currentKeySizeInBytes;
    private uint[][]? _roundKeys;

    #region Static Tables (S-Boxes, IP, FP, and their Inverses)
    
    internal static readonly byte[][] S_BOXES = new byte[][]
    {
        [3, 8, 15, 1, 10, 6, 5, 11, 14, 13, 4, 2, 7, 0, 9, 12],
        [15, 12, 2, 7, 9, 0, 5, 10, 1, 11, 14, 8, 6, 13, 3, 4],
        [8, 6, 7, 9, 3, 12, 10, 15, 13, 1, 14, 4, 0, 11, 5, 2],
        [0, 15, 11, 8, 12, 9, 6, 3, 13, 1, 2, 4, 10, 7, 5, 14],
        [1, 15, 8, 3, 12, 0, 11, 6, 2, 5, 4, 10, 9, 14, 7, 13],
        [15, 5, 2, 11, 4, 10, 0, 7, 12, 3, 6, 13, 14, 9, 8, 1],
        [7, 2, 12, 5, 8, 4, 6, 11, 14, 9, 1, 15, 13, 3, 10, 0],
        [1, 13, 15, 0, 14, 8, 2, 11, 7, 4, 12, 10, 9, 3, 5, 6]
    };

    internal static readonly byte[][] S_BOXES_INVERSE = new byte[8][];

    private static readonly int[] IP_TABLE =
    [
        0, 32, 64, 96, 1, 33, 65, 97, 2, 34, 66, 98, 3, 35, 67, 99,
        4, 36, 68, 100, 5, 37, 69, 101, 6, 38, 70, 102, 7, 39, 71, 103,
        8, 40, 72, 104, 9, 41, 73, 105, 10, 42, 74, 106, 11, 43, 75, 107,
        12, 44, 76, 108, 13, 45, 77, 109, 14, 46, 78, 110, 15, 47, 79, 111,
        16, 48, 80, 112, 17, 49, 81, 113, 18, 50, 82, 114, 19, 51, 83, 115,
        20, 52, 84, 116, 21, 53, 85, 117, 22, 54, 86, 118, 23, 55, 87, 119,
        24, 56, 88, 120, 25, 57, 89, 121, 26, 58, 90, 122, 27, 59, 91, 123,
        28, 60, 92, 124, 29, 61, 93, 125, 30, 62, 94, 126, 31, 63, 95, 127
    ];

    private static readonly int[] FP_TABLE = new int[128]; 

    private static readonly int[] _initialPermutationTable;
    private static readonly int[] _finalPermutationTable;
    private static readonly int[] _inverseInitialPermutationTable;
    private static readonly int[] _inverseFinalPermutationTable;

    static Serpent()
    {
        for (int i = 0; i < 8; i++)
        {
            S_BOXES_INVERSE[i] = new byte[16];
            for (int j = 0; j < 16; j++)
            {
                S_BOXES_INVERSE[i][S_BOXES[i][j]] = (byte)j;
            }
        }
        
        for (int i = 0; i < 128; ++i) FP_TABLE[IP_TABLE[i]] = i;

        _initialPermutationTable = IP_TABLE;
        _finalPermutationTable = FP_TABLE;

        _inverseInitialPermutationTable = new int[128];
        _inverseFinalPermutationTable = new int[128];
        for (int i = 0; i < 128; i++) _inverseInitialPermutationTable[IP_TABLE[i]] = i;
        for (int i = 0; i < 128; i++) _inverseFinalPermutationTable[FP_TABLE[i]] = i;
    }

    #endregion
    

    public byte[]? Key
    {
        get => (byte[]?)_key?.Clone();
        set
        {
            if (value == null)
            {
                _key = null;
                _roundKeys = null;
                _currentKeySizeInBytes = 0;
                return;
            }

            var keyExt = new SerpentKeyExtension(value);
            _roundKeys = keyExt.GetRoundKeys();
            _key = (byte[])value.Clone();
            _currentKeySizeInBytes = value.Length;
        }
    }

    public int BlockSize => BLOCK_SIZE_BYTES;
    public int KeySize => _currentKeySizeInBytes;
    public EncryptionState? EncryptionState => null;

    private void BytesToWords(ReadOnlySpan<byte> bytes, uint[] words)
    {
        Debug.Assert(bytes.Length == 16 && words.Length == 4);
        for (int i = 0; i < 4; i++)
        {
            words[i] = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(i * 4));
        }
    }

    private void WordsToBytes(uint[] words, Span<byte> bytes)
    {
        Debug.Assert(bytes.Length == 16 && words.Length == 4);
        for (int i = 0; i < 4; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(i * 4), words[i]);
        }
    }

    private static uint RotateLeft(uint value, int shift) => (value << shift) | (value >> (32 - shift));
    private static uint RotateRight(uint value, int shift) => (value >> shift) | (value << (32 - shift));

    private void ApplyPermutation(uint[] dataWords, int[] permutationTable)
    {
        uint[] tempResult = new uint[4]; // Store permuted bits here
        for (int outputBitPos = 0; outputBitPos < 128; outputBitPos++)
        {
            int inputBitPos = permutationTable[outputBitPos];

            uint bitValue = (dataWords[inputBitPos / 32] >> (inputBitPos % 32)) & 1;

            if (bitValue == 1)
            {
                tempResult[outputBitPos / 32] |= (1u << (outputBitPos % 32));
            }
        }

        Array.Copy(tempResult, dataWords, 4);
    }
    

    private void ApplySBoxesToData(uint[] dataWords, int sboxSetIndex, bool useInverseSboxes)
    {
        byte[][] sboxChoice = useInverseSboxes ? S_BOXES_INVERSE : S_BOXES;
        for (int wordIdx = 0; wordIdx < 4; wordIdx++)
        {
            uint currentWord = dataWords[wordIdx];
            uint resultingWord = 0;
            for (int nibbleIdx = 0; nibbleIdx < 8; nibbleIdx++)
            {
                uint originalNibble = (currentWord >> (nibbleIdx * 4)) & 0xF;
                uint transformedNibble = sboxChoice[sboxSetIndex][(int)originalNibble];
                resultingWord |= (transformedNibble << (nibbleIdx * 4));
            }

            dataWords[wordIdx] = resultingWord;
        }
    }
    

    private void LinearTransform(uint[] dataWords)
    {
        uint x0 = dataWords[0], x1 = dataWords[1], x2 = dataWords[2], x3 = dataWords[3];

        x0 = RotateLeft(x0, 13);
        x2 = RotateLeft(x2, 3);
        x1 = x1 ^ x0 ^ x2;
        x3 = x3 ^ x2 ^ (x0 << 3);
        x1 = RotateLeft(x1, 1);
        x3 = RotateLeft(x3, 7);
        x0 = x0 ^ x1 ^ x3;
        x2 = x2 ^ x3 ^ (x1 << 7);
        x0 = RotateLeft(x0, 5);
        x2 = RotateLeft(x2, 22);

        dataWords[0] = x0;
        dataWords[1] = x1;
        dataWords[2] = x2;
        dataWords[3] = x3;
    }
    

    private void InverseLinearTransform(uint[] dataWords)
    {
        uint x0 = dataWords[0], x1 = dataWords[1], x2 = dataWords[2], x3 = dataWords[3];

        x2 = RotateRight(x2, 22);
        x0 = RotateRight(x0, 5);
        x2 = x2 ^ x3 ^ (x1 << 7);
        x0 = x0 ^ x1 ^ x3;
        x3 = RotateRight(x3, 7);
        x1 = RotateRight(x1, 1);
        x3 = x3 ^ x2 ^ (x0 << 3);
        x1 = x1 ^ x0 ^ x2;
        x2 = RotateRight(x2, 3);
        x0 = RotateRight(x0, 13);

        dataWords[0] = x0;
        dataWords[1] = x1;
        dataWords[2] = x2;
        dataWords[3] = x3;
    }
    

    public void EncryptBlock(Span<byte> state)
    {
        if (state.Length != BLOCK_SIZE_BYTES)
            throw new ArgumentException("State length must be " + BLOCK_SIZE_BYTES + " bytes.", nameof(state));
        if (_roundKeys == null) throw new InvalidOperationException("Key must be set before encryption.");

        uint[] words = new uint[4];
        BytesToWords(state, words);

        ApplyPermutation(words, _initialPermutationTable);

        for (int r = 0; r < NUM_ROUNDS; r++)
        {
            for (int i = 0; i < 4; i++) words[i] ^= _roundKeys[r][i];

            ApplySBoxesToData(words, r % 8, false);

            if (r < NUM_ROUNDS - 1)
            {
                LinearTransform(words);
            }
            else
            {
                for (int i = 0; i < 4; i++) words[i] ^= _roundKeys[NUM_ROUNDS][i];
            }
        }

        ApplyPermutation(words, _finalPermutationTable);
        WordsToBytes(words, state);
    }

    public void DecryptBlock(Span<byte> state)
    {
        if (state.Length != BLOCK_SIZE_BYTES)
            throw new ArgumentException("State length must be " + BLOCK_SIZE_BYTES + " bytes.", nameof(state));
        if (_roundKeys == null) throw new InvalidOperationException("Key must be set before decryption.");

        uint[] words = new uint[4];
        BytesToWords(state, words);

        ApplyPermutation(words, _inverseFinalPermutationTable);

        for (int r = NUM_ROUNDS - 1; r >= 0; r--)
        {
            if (r < NUM_ROUNDS - 1)
            {
                InverseLinearTransform(words);
            }
            else
            {
                for (int i = 0; i < 4; i++) words[i] ^= _roundKeys[NUM_ROUNDS][i];
            }

            ApplySBoxesToData(words, r % 8, true);

            for (int i = 0; i < 4; i++) words[i] ^= _roundKeys[r][i];
        }

        ApplyPermutation(words, _inverseInitialPermutationTable);
        WordsToBytes(words, state);
    }

    public object Clone()
    {
        Serpent clone = new Serpent();
        if (this._key != null)
        {
            clone.Key = this._key;
        }

        return clone;
    }
}