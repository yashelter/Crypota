using System.Numerics;
using static Crypota.CryptoMath.CryptoMath;

namespace Crypota.RSA.HackingTheGate;

public static class AttackOnFermat
{

    private static (BigInteger p, BigInteger q) GetKeyParts(BigInteger n)
    {
        BigInteger a = Sqrt(n) + 1;
        BigInteger bSquare = (a * a) - n;
        BigInteger b = Sqrt(bSquare);
        
        while (b * b != bSquare)
        {
            a += 1;
            bSquare = a * a - n;
            b = Sqrt(bSquare);
        }
        
        BigInteger p = a + b;
        BigInteger q = a - b;
        
        return (p, q);
    }
    
    public static (BigInteger d, BigInteger phi) HackTheGate(BigInteger publicE, BigInteger n)
    {
        var (p, q) = GetKeyParts(n);

        BigInteger phi = (p - 1) * (q - 1);
        BigInteger d = BigInteger.Zero, y = BigInteger.Zero;
        
        Gcd(phi, publicE, ref y, ref d);
        d = (d % phi + phi) % phi;

        return (d, phi);
    }
}