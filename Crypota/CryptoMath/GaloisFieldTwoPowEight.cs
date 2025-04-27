using System.Text;

namespace Crypota.CryptoMath;

public class GaloisFieldTwoPowEight
{
    public static readonly Lazy<byte[]> IrreducibleEightDegree = new (GetEightDegreeIrreduciblePolynoms());
    
    public static byte AdditionPolynoms(byte a, byte b)
    {
        return (byte) (a ^ b);
    }

    private static byte MultiplyPolynomByXByMod(byte a, byte mod)
    {
        if (!IrreducibleEightDegree.Value.Contains(mod))
        {
            throw new NotIrreduciblePolynomException();
        }
        
        if ((a & 0x80) == 0x80)
        {
            return (byte) ((a << 1) ^ mod);
        }
        return (byte) (a << 1);
    }
    
    public static byte MultiplyPolynomsByMod(byte a, byte b, byte mod)
    {
        if (!IrreducibleEightDegree.Value.Contains(mod))
        {
            throw new NotIrreduciblePolynomException();
        }
        byte result = 0;
        for (int i = 0; i < 8; i++)
        {
            if ((b & 1) != 0) result ^= a;
            a = MultiplyPolynomByXByMod(a, mod);
            b >>= 1;
        }
        return result;
    }
    
    
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

    private static byte[] GetEightDegreeIrreduciblePolynoms()
    {
        var lst = CalculateAllIrreduciblePolynoms(8);
        byte[] result = new byte[lst.Count];
        for (int i = 0; i < lst.Count; i++)
            result[i] = (byte) (lst[i] & 0xFF);
        return result;
    }

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
    
    public static byte GetOppositePolynom(byte poly, byte mod)
    {
        if (!IrreducibleEightDegree.Value.Contains(mod))
        {
            throw new NotIrreduciblePolynomException();
        }
        return BinaryPowerPolynomByMod(poly, 254, mod);
    }
    
}