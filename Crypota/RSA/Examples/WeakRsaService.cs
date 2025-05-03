namespace Crypota.RSA.Examples;

using System.Numerics;
using System.Security.Cryptography;
using Crypota.PrimalityTests;
using static CryptoMath.CryptoMath;


/// <summary>
/// Class not for usage for encrypting, made be weak for attacks
/// </summary>
/// <param name="primaryTestOption"></param>
/// <param name="probability"></param>
/// <param name="bitLength"></param>
public class WeakRsaService(WeakRsaService.PrimaryTestOption primaryTestOption, double probability, int bitLength)
{
    public (BigInteger e, BigInteger d, BigInteger n, BigInteger phi) KeyPair { get; private set; }
    public enum PrimaryTestOption
    {
        FermatTest = 0,
        SolovayStrassenTest = 1,
        MillerRabinTest = 2,
    }
    
    public class RsaKeyGen
    {
        public BigInteger PublicExponent { get; set; } = 65537;
        
        private readonly RandomNumberGenerator _rng = RandomNumberGenerator.Create();
        private readonly IPrimaryTest _primaryTest;
        private readonly double _probability;
        private readonly int _bitLength;
        
        
        public RsaKeyGen(PrimaryTestOption primaryTestOption, double probability, int bitLength)
        {
            
            if (probability < 0.5 || probability >= 1)
            {
                throw new ArgumentOutOfRangeException(nameof(probability),"Probability is not reachable");
            }

            if (bitLength < 3)
            {
                throw new ArgumentOutOfRangeException(nameof(bitLength),"BitLength is too small");
            }
            
            _probability = probability;
            _bitLength = bitLength;
            
            switch (primaryTestOption)
            {
                case PrimaryTestOption.FermatTest:
                    _primaryTest = new FermatTest();
                    break;
                case PrimaryTestOption.SolovayStrassenTest:
                    _primaryTest = new SolovayStrassenTest();
                    break;
                case PrimaryTestOption.MillerRabinTest:
                    _primaryTest = new MillerRabinTest();
                    break;
                default:
                    throw new ArgumentException("Unknown primary test");
            }
        }

        private byte[] GetRandomBytes(int maxBits)
        {
            int size =  maxBits / 8 + (maxBits % 8 == 0 ? 0 : 1);
            byte mask = (byte)(1 << ((maxBits % 8) - 1));
            byte[] bytes = new byte[size];
            _rng.GetBytes(bytes);
            bytes[^1] &= mask;
            
            return bytes;
        }

        private BigInteger GeneratePrimaryNumber()
        {
            BigInteger candidate;
            Probability state;
            int bits = _bitLength % 8;
            if (bits == 0) bits = 8;
            do 
            {
                byte[] bytes = GetRandomBytes(_bitLength);
                bytes[^1] |= (byte) (1 << (bits - 1));

                candidate = new BigInteger(bytes, isUnsigned: true, isBigEndian: false);
                state = _primaryTest.PrimaryTest(candidate, _probability);
            } while (state == Probability.Composite);
            
            return candidate;
        }
        
        private BigInteger GenerateClosePrimary(BigInteger p)
        {
            BigInteger candidate = p + 2;
            while (true)
            {
                if (candidate.IsEven)
                    candidate += 1;
                if (_primaryTest.PrimaryTest(candidate, _probability) == Probability.PossiblePrimal)
                    return candidate;
                candidate += 2;
            }
        }

        public (BigInteger e, BigInteger d, BigInteger N, BigInteger phi) GenerateKeyPair()
        {
            BigInteger p, q, N;
            BigInteger d = 3, y = BigInteger.Zero, phi = BigInteger.Zero;
            BigInteger e, gcd = BigInteger.One;

            do
            {
                p = GeneratePrimaryNumber();
                q = GenerateClosePrimary(p);

                N = q * p;
                phi = (p - BigInteger.One) * (q - BigInteger.One);
                e = BigInteger.Zero;

                gcd = Gcd(phi, d, ref y, ref e);
                e = (e % phi + phi) % phi;
            } while (81 * BinaryPower(d, 4) >= N || gcd != 1);

            return (e, d, N, phi);
        }
    }
    
    private readonly RsaKeyGen _keyGen = new(primaryTestOption, probability, bitLength);

    public byte[] EncryptMessage(BigInteger e, BigInteger n, byte[] message)
    {
        BigInteger m = new BigInteger(message, isBigEndian: true, isUnsigned: true);
        return BinaryPowerByMod(m, e, n).ToByteArray(isBigEndian: true, isUnsigned: true);
    }
    
    public byte[] DecryptMessage(BigInteger d, BigInteger n, byte[] cipher)
    {
        BigInteger c = new BigInteger(cipher, isBigEndian: true, isUnsigned: true);
        return BinaryPowerByMod(c, d, n).ToByteArray(isUnsigned: true, isBigEndian: true);
    }

    public WeakRsaService GenerateKeyPair()
    {
        KeyPair = _keyGen.GenerateKeyPair();
        return this;
    }

    public byte[] EncryptMessage(byte[] message)
    {
        return EncryptMessage(KeyPair.e, KeyPair.n, message);
    }
    
    public byte[] DecryptMessage(byte[] message)
    {
        return DecryptMessage(KeyPair.d, KeyPair.n, message);
    }   
}