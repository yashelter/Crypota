using Crypota.CryptoMath;

namespace UnitTests.Tests.Rijndael;
using static Crypota.CryptoMath.GaloisFieldTwoPowEight;

[TestClass]
public class GaloisFieldTwoPowEightTests
{
    [DataTestMethod]
    [DataRow(8, 30)]
    public void IrreduciblePolynomsTest(int degree, int cnt)
    {
        var result = CalculateAllIrreduciblePolynoms(degree);
        foreach (var item in result)
        {
            Console.WriteLine(PolynomToString(item));
        }
        
        Assert.AreEqual(cnt, result.Count);
    }
    
    [DataTestMethod]
    [DataRow((byte)1, (byte)1)]
    [DataRow((byte)0x02, (byte)0x8D)]
    [DataRow((byte)0xBF, (byte)0x57)]
    [DataRow((byte)0xAE, (byte)0xD2)]
    [DataRow((byte)0xC6, (byte)0xE4)]
    public void TestGainingOppositeByMod(byte a, byte expected)
    {
        var result = GetOppositePolynom(a, 27);
        Assert.AreEqual(expected, result);
    }
    
    [DataTestMethod]
    [DataRow((byte)1, (byte)1)]
    [DataRow((byte)0x02, (byte)0x8D)]
    [DataRow((byte)0xBF, (byte)0x57)]
    [DataRow((byte)0xAE, (byte)0xD2)]
    [DataRow((byte)0xC6, (byte)0xE4)]
    public void TestGainingOppositeByModNegative(byte a, byte expected)
    {
        Assert.ThrowsException<NotIrreduciblePolynomException>(new Action(() => GetOppositePolynom(a, 0)));
    }

}
