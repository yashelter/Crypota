using System.Numerics;

namespace Crypota;

public static class Utilities
{
    
    public static BigInteger Gcd(BigInteger a, BigInteger b)
    {
        if (a == BigInteger.Zero)
        {
            return b;
        }
        BigInteger d = Gcd(b % a, a);
        return d;
    }
    
    
    public static BigInteger Gcd(BigInteger a, BigInteger b, ref BigInteger x, ref BigInteger y)
    {
        if (a == BigInteger.Zero)
        {
            x = BigInteger.Zero;
            y = BigInteger.One;
            return b;
        }

        BigInteger x1 = BigInteger.Zero, y1 = BigInteger.Zero;
        BigInteger d = Gcd(b % a, a, ref x1, ref y1);
        x = y1 - (b / a) * x1;
        y = x1;
        
        return d;
    }

    public static BigInteger BinaryPower(BigInteger a, BigInteger power)
    {
        BigInteger res = BigInteger.One;
        while (power != BigInteger.Zero)
            if (!power.IsEven)
            {
                res *= a;
                --power;
            }
            else
            {
                a *= a;
                power >>= 1;
            }

        return res;
    }
    
    public static BigInteger BinaryPowerByMod(BigInteger a, BigInteger power, in BigInteger mod)
    {
        BigInteger res = BigInteger.One;
        a  = (a % mod + mod) % mod;
        while (power > BigInteger.Zero)
        {
            if (!power.IsEven)
            {
                res = (res * a) % mod;
                --power;
            }
            a = (a * a) % mod;
            power >>= 1;
        }

        return res % mod;
    }
    
    

    public static BigInteger LegendreSymbol(BigInteger a, BigInteger p)
    {
        if (p < 3 || p.IsEven)
            throw new ArgumentException("P should be a simple number and not equal to 2");

        BigInteger aModP = a % p;
        if (aModP < 0)
        {
            aModP += p;
        }
        
        if (aModP == 0)
            return BigInteger.Zero;

        // Euler criteria
        BigInteger exponent = (p - BigInteger.One) / new BigInteger(2);
        BigInteger result = BinaryPowerByMod(aModP, exponent, p);

        return (result == BigInteger.One) ? 1 : -1;
        //return result;
    }
    
    public static BigInteger JacobiSymbolRecursive(BigInteger a, BigInteger n)
    {
        if (n.IsEven || n <= 2)
            throw new ArgumentException("Invalid argument n for Jacobi symbol");

        if (a % n == 0)
        {
            return BigInteger.Zero;
        }
        
        if (a == BigInteger.One)
        {
            return BigInteger.One;
        }

        if (a < 0)
        {
            int zn = ((n - 1) / 2).IsEven ? 1 : -1;
            return zn * JacobiSymbolRecursive(-a, n);
        }

        if (a.IsEven)
        {
            int zn = ((n*n - 1) / 8).IsEven ? 1 : -1;
            return zn * JacobiSymbolRecursive(a / 2, n);
        }

        if (a < n)
        {
            int zn = ((a - 1) / 2 * (n - 1) / 2).IsEven ? 1 : -1;
            return zn * JacobiSymbolRecursive(n, a);
        }

        return JacobiSymbolRecursive(a % n, n);
    }
    
    public static BigInteger JacobiSymbol(BigInteger a, BigInteger n)
    {
        if (n <= 0 || n.IsEven)
        {
            throw new ArgumentException("Invalid argument n for Jacobi symbol");
        }

        a = (a % n + n) % n;
        BigInteger t = BigInteger.One;
        
        if (Gcd(a, n) != 1)
        {
            return BigInteger.Zero;
        }
        while (a != 0)
        {
            while (a.IsEven)
            {
                a /= 2;
                var r = n % 8;
                if (r == 3 || r == 5)
                {
                    t = -t;
                }
            }
            (a, n) = (n, a);
            if (a % 4 == 3 && n% 4 == 3)
            {
                t = -t;
            }
            a = a % n;
        }
        if (n == BigInteger.One)  {return t;  }
        return BigInteger.Zero;
    }
}
