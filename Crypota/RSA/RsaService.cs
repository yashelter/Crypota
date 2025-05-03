using System.Numerics;
using System.Security.Cryptography;
using Crypota.PrimalityTests;
using static Crypota.CryptoMath.CryptoMath;

namespace Crypota.RSA;

public class RsaService(RsaService.PrimaryTestOption primaryTestOption, double probability, int bitLength)
{
    private (BigInteger e, BigInteger d, BigInteger n) KeyPair { get; set; }

    public (BigInteger e, BigInteger n) GetPublicKey()
    {
        return (KeyPair.e, KeyPair.n);
    }
    
    public (BigInteger d, BigInteger n) GetPrivateKey()
    {
        return (KeyPair.d, KeyPair.n);
    }
    
    
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

        public (BigInteger e, BigInteger d, BigInteger N) GenerateKeyPair()
        {
            BigInteger p, q, N, gcd = BigInteger.One;
            BigInteger d, y = BigInteger.Zero;
            BigInteger minDiff = BigInteger.One << (_bitLength / 2 - 100);
            do
            {
                p = GeneratePrimaryNumber();
                q = GeneratePrimaryNumber();
                if (BigInteger.Abs(p - q) < minDiff)
                {
                    N = BigInteger.One;
                    d = BigInteger.Zero;
                    continue;
                }

                N = q * p;
                var phi = (p - BigInteger.One) * (q - BigInteger.One);
                d = BigInteger.Zero;
                
                gcd = Gcd(phi, PublicExponent, ref y, ref d);
                d = (d % phi + phi) % phi;
                
            } while (81 * BigInteger.Pow(d, 4) < N || gcd != 1);

            return (PublicExponent, d, N);
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

    public RsaService GenerateKeyPair()
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