using System.Numerics;
using System.Runtime.CompilerServices;

namespace Crypota.CryptoMath;

public static partial class CryptoMath
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static BigInteger Gcd(BigInteger a, BigInteger b)
    {
        if (a == BigInteger.Zero)
        {
            return b;
        }
        BigInteger d = Gcd(b % a, a);
        return d;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
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

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static BigInteger GcdIterative(BigInteger a, BigInteger b, out BigInteger x, out BigInteger y)
    {
        if (a < 0)
            throw new ArgumentOutOfRangeException((nameof(a)), "a must be non-negative.");

        if (b < 0)
            throw new ArgumentOutOfRangeException((nameof(b)), "b must be non-negative.");

        BigInteger x0 = BigInteger.One;
        BigInteger y0 = BigInteger.Zero;
        BigInteger x1 = BigInteger.Zero;
        BigInteger y1 = BigInteger.One;

        while (a != BigInteger.Zero)
        {
            BigInteger q = b / a;
            BigInteger tempB = b;
            b = a;
            a = tempB % a;

            BigInteger tempX0 = x0;
            x0 = x1;
            x1 = tempX0 - q * x1;

            BigInteger tempY0 = y0;
            y0 = y1;
            y1 = tempY0 - q * y1;
        }

        x = x0;
        y = y0;
        return b;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static BigInteger Gcd(BigInteger a, BigInteger b, out List<BigInteger> coefficients)
    {
        coefficients = [];
        while (b != 0)
        {
            BigInteger q = a / b;
            coefficients.Add(q);

            BigInteger temp = b;
            b = a % b;
            a = temp;
        }

        return a; 
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static BigInteger BinaryPower(BigInteger a, BigInteger power)
    {
        BigInteger res = BigInteger.One;
        while (power != BigInteger.Zero)
        {
            if (!power.IsEven)
            {
                res *= a;
            }
            
            a *= a;
            power >>= 1;
        }

        return res;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static BigInteger BinaryPowerByMod(BigInteger a, BigInteger power, in BigInteger mod)
    {
        BigInteger res = BigInteger.One;
        a  = (a % mod + mod) % mod;
        while (power > BigInteger.Zero)
        {
            if (!power.IsEven)
            {
                res = (res * a) % mod;
            }
            a = (a * a) % mod;
            power >>= 1;
        }

        return res % mod;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
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
    
    // метод Ньютона-Рафсона
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static BigInteger Sqrt(BigInteger n)
    {
        if (n < 0)
            throw new ArgumentException("Отрицательное число не имеет действительного квадратного корня.");
        
        if (n == 0 || n == 1)
            return n;

        BigInteger x = n;
        BigInteger y = (x + 1) / 2;
        
        while (y < x)
        {
            x = y;
            y = (x + n / x) / 2;
        }
        
        return x;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static (BigInteger? x1, BigInteger? x2) SolveQuadrantic(BigInteger b, BigInteger c)
    {
        BigInteger d = b * b - 4 * c;
        if (d < 0)
        {
            return (null, null);
        }
        var prob = Sqrt(d);
        if (prob * prob != d)
        {
            return (null, null);
        }

        BigInteger x1 = (b - prob) / 2;
        BigInteger x2 = (b + prob) / 2;
        
        return (x1, x2);
    }
}
