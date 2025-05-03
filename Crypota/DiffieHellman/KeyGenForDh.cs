using System.Numerics;
using System.Security.Cryptography;
using Crypota.PrimalityTests;
using Crypota.RSA;
namespace Crypota.DiffieHellman;

public class KeyGenForDh
{
    private readonly RandomNumberGenerator _rng = RandomNumberGenerator.Create();
    private readonly IPrimaryTest _primaryTest;
    private readonly double _probability;
    private readonly int _bitLength;   
    
    public KeyGenForDh(RsaService.PrimaryTestOption primaryTestOption, double probability, int bitLength)
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
            case RsaService.PrimaryTestOption.FermatTest:
                _primaryTest = new FermatTest();
                break;
            case RsaService.PrimaryTestOption.SolovayStrassenTest:
                _primaryTest = new SolovayStrassenTest();
                break;
            case RsaService.PrimaryTestOption.MillerRabinTest:
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

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public BigInteger GeneratePrimaryNumber()
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
            if (state == Probability.PossiblePrimal)
            {
                candidate = candidate * 2 + 1;
                state = _primaryTest.PrimaryTest(candidate, _probability);
            }
        } while (state == Probability.Composite);
            
        return candidate;
    }
}