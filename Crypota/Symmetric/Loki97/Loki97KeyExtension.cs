using System.Buffers.Binary;

namespace Crypota.Symmetric.Loki97;

public readonly struct RoundSubkeys
{
    public readonly ulong Ka;
    public readonly ulong Kb;
    public readonly ulong Kc;

    public RoundSubkeys(ulong ka, ulong kb, ulong kc)
    {
        Ka = ka;
        Kb = kb;
        Kc = kc;
    }
}

 public class Loki97KeyExtension
    {
        private readonly ulong[] _keyWords;
        private readonly int _numKeyWords;
        private const int NUM_ROUNDS = 16;


        public Loki97KeyExtension(byte[] key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (key.Length != 16 && key.Length != 24 && key.Length != 32)
            {
                throw new ArgumentException("Key length must be 16, 24, or 32 bytes (128, 192, or 256 bits).", nameof(key));
            }

            _numKeyWords = key.Length / 8;
            _keyWords = new ulong[_numKeyWords];

            for (int i = 0; i < _numKeyWords; i++)
            {
                _keyWords[i] = BinaryPrimitives.ReadUInt64BigEndian(key.AsSpan(i * 8));
            }
        }

        public RoundSubkeys[] GetRoundKeys()
        {
            var roundKeySets = new RoundSubkeys[NUM_ROUNDS];
            for (int i = 0; i < NUM_ROUNDS; i++) 
            {
                int j = i + 1;
                ulong ka = _keyWords[(uint)(3 * j - 3) % (uint)_numKeyWords]; 
                ulong kb = _keyWords[(uint)(3 * j - 2) % (uint)_numKeyWords];
                ulong kc = _keyWords[(uint)(3 * j - 1) % (uint)_numKeyWords];
                roundKeySets[i] = new RoundSubkeys(ka, kb, kc);
            }
            return roundKeySets;
        }
    }