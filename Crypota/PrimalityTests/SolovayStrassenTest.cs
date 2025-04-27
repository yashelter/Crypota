using System.Numerics;

namespace Crypota.PrimalityTests;
using static CryptoMath.CryptoMath;

public class SolovayStrassenTest: ProbabilityPrimaryTest
{
    public override int AccuracyParam { get; } = 2;
    
    protected override Probability GetProbability(BigInteger testingValue, BigInteger a)
    {
        if (testingValue.IsEven)
        {
            return Probability.Composite;
        }

        if (Gcd(testingValue, a) != 1)
        {
            return Probability.Composite;
        }
        
        BigInteger left = JacobiSymbol(a, testingValue);
        BigInteger right = BinaryPowerByMod(a, (testingValue - 1) / 2, testingValue);

        left = (left % testingValue + testingValue) % testingValue;
        right = (right % testingValue + testingValue) % testingValue;
        
        return (left == right) ? Probability.PossiblePrimal : Probability.Composite;
    }
}