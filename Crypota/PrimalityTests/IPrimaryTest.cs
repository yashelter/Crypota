using System.Numerics;

namespace Crypota.PrimalityTests;

public enum Probability
{
    Composite = 0,
    PossiblePrimal = 1
}

public interface IPrimaryTest
{
    /// <summary>
    /// Probability of one iteration of test
    /// </summary>
    public int AccuracyParam { get; }
    public Probability PrimaryTest(BigInteger testingValue, double targetProbability);
}