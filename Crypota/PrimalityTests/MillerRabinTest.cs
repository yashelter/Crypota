using System.Numerics;
using static Crypota.CryptoMath.CryptoMath;
namespace Crypota.PrimalityTests;

public class MillerRabinTest : ProbabilityPrimaryTest
{
    public override int AccuracyParam { get; } = 4;

    protected override Probability GetProbability(BigInteger testingValue, BigInteger a)
    {
        BigInteger s = BigInteger.Zero;
        BigInteger d = testingValue - 1;
        while (d % 2 == 0)
        {
            ++s;
            d /= 2;
        }

        if (BinaryPowerByMod(a, d, testingValue) == 1)
        {
            return Probability.PossiblePrimal;
        }

        BigInteger val = testingValue - BigInteger.One;
        for (BigInteger r = BigInteger.Zero; r < s; r++)
        {
            if (BinaryPowerByMod(a, d * BinaryPower(2, r), testingValue) == val)
            {
                return Probability.PossiblePrimal;
            }
        }

        return Probability.Composite;
    }
}