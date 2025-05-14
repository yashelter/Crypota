using System.Numerics;
using Crypota.RSA;
using static Crypota.CryptoMath.CryptoMath;

namespace Crypota.DiffieHellman;

public static class Protocol
{
    private static double probability = 0.999;
    private const int Bitlen = 3056;
    
    public static (BigInteger p, BigInteger q) GeneratePair(int bitlen = Bitlen)
    {
        KeyGenForDh genForDh = new KeyGenForDh(RsaService.PrimaryTestOption.MillerRabinTest, probability, bitlen);
        BigInteger g = 1, p;
        bool great = false;
        do
        {
            p = genForDh.GeneratePrimaryNumber();
            var phi = p - 1;
        
            var q = (phi / 2);
            for (BigInteger probG = 2; probG < phi; probG++)
            {
                if (BinaryPowerByMod(probG, (phi / q), p) != 1 &&
                    BinaryPowerByMod(probG, (phi / 2), p) != 1)
                {
                    g = probG;
                    great = true;
                    break;
                }
            }
        } while (!great);
        return (g, p);
    }

    public static BigInteger GeneratePart(BigInteger g, BigInteger secretPrimal, BigInteger p)
    {
        return BinaryPowerByMod(g, secretPrimal, p);
    }

    public static BigInteger GenerateSecret(BigInteger a, BigInteger b, BigInteger p)
    {
        return BinaryPowerByMod(a, b, p);
    }
}