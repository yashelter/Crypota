using System.Numerics;
using System.Security.Cryptography;
using static Crypota.RSA.HackingTheGate.ChainedFraction;
using static Crypota.CryptoMath.CryptoMath;

namespace Crypota.RSA.HackingTheGate;

public class AttackOnWiener
{
    public static (BigInteger d, BigInteger phi, List<(BigInteger, BigInteger)>) HackTheGate(BigInteger e, BigInteger n)
    {
        List<(BigInteger, BigInteger)> result = [];
        var koefs = Decompose(e, n);
        
        for (int i = 1; i < koefs.Count+1; i++)
        {
            var (k, d) = Compose(koefs.GetRange(0, i));
            result.Add((k, d));

            BigInteger temp = (e * d - 1);
            Console.WriteLine($"k:{k}, d:{d}");
            
            if (k == 0 || temp % k != 0)
            {
                continue;
            }
            
            BigInteger phi = (temp / k);
            temp = n - phi + 1;
            
            var (p, q) = SolveQuadrantic(temp, n);
            
            if (p == null || q == null)
            {
                continue;
            }
            if (p * q == n)
            {
                return (d, phi, result);
            }
        }

        throw new CryptographicException("Can't provide Wiener's attack");

    }
}