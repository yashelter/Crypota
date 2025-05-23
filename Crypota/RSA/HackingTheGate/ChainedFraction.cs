using System.Numerics;
using static Crypota.CryptoMath.CryptoMath;
using static Crypota.CryptoMath.SymmetricUtils;

namespace Crypota.RSA.HackingTheGate;

public class ChainedFraction
{
    public static List<BigInteger> Decompose(BigInteger a, BigInteger b)
    {
        List<BigInteger> result;
        Gcd(a, b, out result);
        return result; 
    }

    public static (BigInteger a, BigInteger b) Compose(List<BigInteger> coefficients)
    {
        BigInteger numerator = coefficients[^1];
        BigInteger denominator = BigInteger.One;

        foreach (var q in coefficients.Reverse<BigInteger>().Skip(1))
        {
            Swap(ref numerator, ref denominator);
            numerator += (q * denominator);
        }
        return (numerator, denominator);
    }
    
    
}