using System.Numerics;

namespace Crypota.PrimalityTests;
using static CryptoMath.CryptoMath;

public class FermatTest : ProbabilityPrimaryTest
{
    public override int AccuracyParam { get; } = 2;
    protected override Probability GetProbability(BigInteger testingValue, BigInteger a)
    {
        BigInteger probableOne = BinaryPowerByMod(a, testingValue - 1, testingValue);
        if (probableOne == BigInteger.One)
        {
            return Probability.PossiblePrimal;
        }

        return Probability.Composite;
    }
    
}