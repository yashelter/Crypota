using System.Runtime.CompilerServices;
using System.Text;

namespace Crypota.CryptoMath;

public static class GaloisFieldTwoPowEight
{
    public struct PolynomialInGf(byte k3, byte k2, byte k1, byte k0)
    {
        public byte k3 = k3, k2 = k2, k1 = k1, k0 = k0;
        // k3 * x^3 + k2 * x^2 +..
        public static PolynomialInGf GetCx() => new PolynomialInGf(0x03, 0x01, 0x01, 0x02);
        public static PolynomialInGf GetInvCx() => new PolynomialInGf(0x0B, 0x0D, 0x09, 0x0E);
    }
    
    
    public static readonly Lazy<byte[]> IrreducibleEightDegree = new (GetEightDegreeIrreduciblePolynoms);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static byte AdditionPolynoms(byte a, byte b)
    {
        return (byte) (a ^ b);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static byte MultiplyPolynomByXByMod(byte a, byte mod)
    {
        //if (!IrreducibleEightDegree.Value.Contains(mod)) throw new NotIrreduciblePolynomException();
        
        if ((a & 0x80) == 0x80)
        {
            return (byte) ((a << 1) ^ mod);
        }
        return (byte) (a << 1);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static byte MultiplyPolynomsByMod(byte a, byte b, byte mod)
    {
        //if (!IrreducibleEightDegree.Value.Contains(mod)) throw new NotIrreduciblePolynomException();
        
        byte result = 0;
        for (int i = 0; i < 8; i++)
        {
            if ((b & 1) != 0) result ^= a;
            a = MultiplyPolynomByXByMod(a, mod);
            b >>= 1;
        }
        return result;
    }
    
    
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static int GetDegree(int poly)
    {
        if (poly == 0) return -1;
        int degree = 0;
        int temp = poly;
        while (temp > 1)
        {
            temp >>= 1;
            degree++;
        }
        return degree;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static int ModuloPolynoms(int a, int b)
    {
        if (a == 0) return 0;

        int dividendDegree = GetDegree(a);
        int divisorDegree = GetDegree(b);

        while (dividendDegree >= divisorDegree)
        {
            int shift = dividendDegree - divisorDegree;
            int alignedDivisor = b << shift;

            a ^= alignedDivisor;

            dividendDegree = GetDegree(a);
        }
        
        return a;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static int DividePolynoms(int a, int b)
    {
        if (b == 0)
        {
            throw new DivideByZeroException("Полином-делитель не может быть нулевым.");
        }
        if (a == 0) return 0;

        int dividendDegree = GetDegree(a);
        int divisorDegree = GetDegree(b);
        int res = 0;

        while (dividendDegree >= divisorDegree)
        {
            int shift = dividendDegree - divisorDegree;
            res |= (1 << shift);
            int alignedDivisor = b << shift;

            a ^= alignedDivisor;

            dividendDegree = GetDegree(a);
        }
        
        return res;
    }

    
    public static string PolynomToString(int poly)
    {
        if (poly == 0) return "0";

        StringBuilder sb = new StringBuilder();
        int degree = GetDegree(poly);

        for (int i = degree; i >= 0; i--)
        {
            if (((poly >> i) & 1) == 1)
            {
                if (sb.Length > 0)  sb.Append(" + ");
                if (i == 0)  sb.Append("1");
                else if (i == 1)  sb.Append("x");
                else  sb.Append($"x^{i}");
            }
        }
        return sb.ToString();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool PolynomIsIrreducible(int poly, int degree)
    {
        int maxCheckValue = 1 << ((degree / 2) + 1);

        for (int i = 2; i < maxCheckValue; i+=1)
        {
            int mod = ModuloPolynoms(poly, i);
            if (mod == 0)
            {
                return false;
            }
        }
        return true;

    }

    
    public static List<int> CalculateAllIrreduciblePolynoms(int degree)
    {
        if (degree < 1)
            throw new ArgumentOutOfRangeException(nameof(degree), "Degree cannot be less than one");
        
        List<int> result = new List<int>();
        
        int val = 1 << degree;
        int maxValue = 1 << (degree + 1);
        
        while (val < maxValue)
        {
            if (PolynomIsIrreducible(val, degree))
            {
                result.Add(val);
            }
            val++;
        }
        return result;
    }

    public static List<int> GetAllDividersPolynoms(int poly)
    {
        List<int> result = new List<int>();
        List<int> irreduciblePolynoms = new List<int>();
        int degree = Math.Min(GetDegree(poly), 7);

        for (int i = 1; i < degree; i++)
        {
            irreduciblePolynoms.AddRange(CalculateAllIrreduciblePolynoms(GetDegree(poly)));
        }

        foreach (int i in irreduciblePolynoms)
        {
            while (ModuloPolynoms(poly, i) == 0)
            {
                poly = DividePolynoms(poly, i);
                result.Add(i);
            }
        }
        return result;
    }

    private static byte[] GetEightDegreeIrreduciblePolynoms()
    {
        var lst = CalculateAllIrreduciblePolynoms(8);
        byte[] result = new byte[lst.Count];
        for (int i = 0; i < lst.Count; i++)
            result[i] = (byte) (lst[i] & 0xFF);
        return result;
    }
    

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static byte BinaryPowerPolynomByMod(byte poly, int power, byte mod)
    {
        byte res = 1;
        while (power != 0)
        {
            if ((power & 1) == 1)
            {
                res = MultiplyPolynomsByMod(res, poly, mod);
            }

            poly = MultiplyPolynomsByMod(poly, poly, mod);
            power >>= 1;
        }

        return res;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static byte GetOppositePolynom(byte poly, byte mod)
    {
        //if (!IrreducibleEightDegree.Value.Contains(mod)) throw new NotIrreduciblePolynomException();
        
        return BinaryPowerPolynomByMod(poly, 254, mod);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static PolynomialInGf AdditionPolynoms(PolynomialInGf a, PolynomialInGf b)
    {
        return new PolynomialInGf(
            AdditionPolynoms(a.k0, b.k0),
            AdditionPolynoms(a.k1, b.k1),
            AdditionPolynoms(a.k2, b.k2),
            AdditionPolynoms(a.k3, b.k3));
    }
    
    
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static PolynomialInGf MultiplicationPolynoms(PolynomialInGf a, PolynomialInGf b, byte mod)
    {
        byte d0 = (byte)((MultiplyPolynomsByMod(a.k0, b.k0, mod) ^ MultiplyPolynomsByMod(a.k3, b.k1, mod)) ^ (MultiplyPolynomsByMod(a.k2, b.k2, mod) ^ MultiplyPolynomsByMod(a.k1, b.k3, mod)));
        byte d1 = (byte)((MultiplyPolynomsByMod(a.k1, b.k0, mod) ^ MultiplyPolynomsByMod(a.k0, b.k1, mod)) ^ (MultiplyPolynomsByMod(a.k3, b.k2, mod) ^ MultiplyPolynomsByMod(a.k2, b.k3, mod)));
        byte d2 = (byte)((MultiplyPolynomsByMod(a.k2, b.k0, mod) ^ MultiplyPolynomsByMod(a.k1, b.k1, mod)) ^ (MultiplyPolynomsByMod(a.k0, b.k2, mod) ^ MultiplyPolynomsByMod(a.k3, b.k3, mod)));
        byte d3 = (byte)((MultiplyPolynomsByMod(a.k3, b.k0, mod) ^ MultiplyPolynomsByMod(a.k2, b.k1, mod)) ^ (MultiplyPolynomsByMod(a.k1, b.k2, mod) ^ MultiplyPolynomsByMod(a.k0, b.k3, mod)));
        
        return new PolynomialInGf(d3, d2, d1, d0);
    }
}