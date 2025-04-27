using System.Numerics;
using System.Security.Cryptography;

namespace Crypota.PrimalityTests;

public abstract class ProbabilityPrimaryTest : IPrimaryTest
{
    /// <summary>
    /// Random Number Generator
    /// </summary>
    protected RandomNumberGenerator rng = RandomNumberGenerator.Create();
    
    public virtual int AccuracyParam { get; } = 2;
    
    public Probability PrimaryTest(BigInteger testingValue, double targetProbability)
    {
        if (targetProbability < 0.5 || targetProbability >= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(targetProbability), "Probability must be in range [0.5, 1).");
        }
        
        BigInteger x = 1;
        BigInteger targetP =  new (System.Math.Ceiling(1 / (1 - targetProbability)));
        HashSet<BigInteger> checkedA = new HashSet<BigInteger>();

        do
        {
            BigInteger checkingValue = GetNextA(testingValue, checkedA);
            Probability state = GetProbability(testingValue, checkingValue);
            checkedA.Add(checkingValue);

            if (state == Probability.Composite)
            {
                return state;
            }
            x *= AccuracyParam;
        } while (x < targetP);

        return Probability.PossiblePrimal;
    }
    
    /// <summary>
    /// Method which returns a probable state of testing number (test implementation)
    /// </summary>
    /// <param name="testingValue">Possible primal number p</param>
    /// <param name="a">Number with which will be tested</param>
    /// <returns></returns>
    protected abstract Probability GetProbability(BigInteger testingValue, BigInteger a);

    /// <summary>
    /// Method must not to modify HashSet
    /// </summary>
    /// <param name="testingValue">Possible primal number p</param>
    /// <param name="checkedA">HashSet of already used A</param>
    /// <returns>Unique with other members of HashSet unique numbers</returns>
    protected virtual BigInteger GetNextA(BigInteger testingValue, in HashSet<BigInteger> checkedA)
    {
        BigInteger candidate = 3;
        while (checkedA.Contains(candidate))
        {
            byte[] bytes = new byte[513];
            rng.GetBytes(bytes);
            candidate = ((new BigInteger(bytes, isUnsigned: true, isBigEndian: false) ) % testingValue) + 1;
        }
        
        return candidate; 
    }
}