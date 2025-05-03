using Crypota.Interfaces;
using Crypota.Symmetric.Exceptions;

namespace Crypota.Symmetric.Rc6;

 public class Rc6 : ISymmetricCipher
    {
        private const int WordSizeBits = 32;
        private const int BlockBytesLength = 16; // 128 бит

        private byte[]? _key;
        private uint[]? _S; // расширенные ключи

        public byte[]? Key
        {
            get => _key;
            set
            {
                _key = value ?? throw new ArgumentNullException(nameof(Key));
                if (_key.Length != 16 && _key.Length != 24 && _key.Length != 32)
                    throw new InvalidKeyException("Key length must be 128, 192 or 256 bits.");

                var expander = new Rc6KeyExpansion();
                _S = expander.GenerateRoundKeys(_key);
            }
        }

        public int BlockSize => BlockBytesLength;
        public int KeySize => _key?.Length ?? 0;

        public EncryptionState? EncryptionState { get; }

        public void EncryptBlock(Span<byte> state)
        {
            if (state.Length != BlockBytesLength)
                throw new ArgumentException($"Block size must be {BlockBytesLength}", nameof(state));
            if (_S == null)
                throw new InvalidKeyException("Key is not initialized");

            // Разбиваем блок на четыре слова A, B, C, D
            uint A = BitConverter.ToUInt32(state.Slice(0, 4));
            uint B = BitConverter.ToUInt32(state.Slice(4, 4));
            uint C = BitConverter.ToUInt32(state.Slice(8, 4));
            uint D = BitConverter.ToUInt32(state.Slice(12, 4));

            int r = (_S.Length - 4) / 2;
            // Input whitening
            B += _S[0];
            D += _S[1];

            for (int i = 1; i <= r; i++)
            {
                uint t = RotateLeft(B * (2 * B + 1), 5);
                uint u = RotateLeft(D * (2 * D + 1), 5);
                A = RotateLeft(A ^ t, (int)u) + _S[2 * i];
                C = RotateLeft(C ^ u, (int)t) + _S[2 * i + 1];

                // циклическая перестановка
                uint temp = A;
                A = B;
                B = C;
                C = D;
                D = temp;
            }

            // Output whitening
            A += _S[2 * r + 2];
            C += _S[2 * r + 3];

            // Запись обратно в state
            BitConverter.GetBytes(A).CopyTo(state.Slice(0, 4));
            BitConverter.GetBytes(B).CopyTo(state.Slice(4, 4));
            BitConverter.GetBytes(C).CopyTo(state.Slice(8, 4));
            BitConverter.GetBytes(D).CopyTo(state.Slice(12, 4));
        }

        public void DecryptBlock(Span<byte> state)
        {
            if (state.Length != BlockBytesLength)
                throw new ArgumentException($"Block size must be {BlockBytesLength}", nameof(state));
            if (_S == null)
                throw new InvalidKeyException("Key is not initialized");

            uint A = BitConverter.ToUInt32(state.Slice(0, 4));
            uint B = BitConverter.ToUInt32(state.Slice(4, 4));
            uint C = BitConverter.ToUInt32(state.Slice(8, 4));
            uint D = BitConverter.ToUInt32(state.Slice(12, 4));

            int r = (_S.Length - 4) / 2;
            // Undo output whitening
            C -= _S[2 * r + 3];
            A -= _S[2 * r + 2];

            for (int i = r; i >= 1; i--)
            {
                // обратная перестановка
                uint temp = D;
                D = C;
                C = B;
                B = A;
                A = temp;

                uint t = RotateLeft(B * (2 * B + 1), 5);
                uint u = RotateLeft(D * (2 * D + 1), 5);
                C = RotateRight(C - _S[2 * i + 1], (int)t) ^ u;
                A = RotateRight(A - _S[2 * i], (int)u) ^ t;
            }

            // Undo input whitening
            D -= _S[1];
            B -= _S[0];

            BitConverter.GetBytes(A).CopyTo(state.Slice(0, 4));
            BitConverter.GetBytes(B).CopyTo(state.Slice(4, 4));
            BitConverter.GetBytes(C).CopyTo(state.Slice(8, 4));
            BitConverter.GetBytes(D).CopyTo(state.Slice(12, 4));
        }

        private static uint RotateLeft(uint x, int shift)
        {
            return (x << (shift & 31)) | (x >> (32 - (shift & 31)));
        }

        private static uint RotateRight(uint x, int shift)
        {
            return (x >> (shift & 31)) | (x << (32 - (shift & 31)));
        }
    }