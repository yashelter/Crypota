using System.Numerics;
using Crypota;
using Crypota.CryptoMath;
using Crypota.Symmetric;
using Crypota.PrimalityTests;
using Crypota.RSA;

namespace UnitTests;
using static CryptoMath;

[TestClass]
public sealed class RsaTests
{
    [DataTestMethod]
    [DataRow("2", "10", "1024", "0")]
    [DataRow("2", "10", "1025", "1024")]
    [DataRow("7", "5", "13", "11")]
    [DataRow("64", "3", "5", "4")]
    [DataRow("123456789", "987654321", "1000000007", "652541198")]
    [DataRow("5", "0", "17", "1")]
    [DataRow("10", "5", "1", "0")]
    [DataRow("-3", "3", "5", "3")]
    [DataRow("3", "10000000000", "19", "16")]
    public void TestBinaryPowerByMod(string aStr, string powerStr, string modStr, string expectedStr)
    {
        BigInteger a = BigInteger.Parse(aStr);
        BigInteger power = BigInteger.Parse(powerStr);
        BigInteger mod = BigInteger.Parse(modStr);
        BigInteger expected = BigInteger.Parse(expectedStr);

        var result = BinaryPowerByMod(a, power, mod);
        Assert.AreEqual(expected, result, $"Failed for ({aStr})^{powerStr} mod {modStr}");
    }

    [DataTestMethod]
    [DataRow(64, 3, 1)]
    [DataRow(65, 7, 1)]
    [DataRow(15651566, 1000007, -1)]
    [DataRow(151, 13, -1)]
    [DataRow(655536, 53, -1)]
    [DataRow(-888, 53, 1)]
    [DataRow(0, 53, 0)]
    [DataRow(17, 17, 0)]
    [DataRow(3, 3, 0)]
    public void TestLegendreSymbol(int a, int p, int expected)
    {
        BigInteger castedA = new BigInteger(a);
        BigInteger castedP = new BigInteger(p);
        BigInteger castedExpected = new BigInteger(expected);
        
        var result = LegendreSymbol(castedA, castedP);
        Assert.AreEqual(castedExpected, result);
    }
    
    [DataTestMethod]
    [DataRow(64, 3, 1)]
    [DataRow(65, 7, 1)]
    [DataRow(15651566, 1000007, -1)]
    [DataRow(1000009, 1000007, 1)]
    [DataRow(151, 13, -1)]
    [DataRow(655535, 53, -1)]
    [DataRow(17, 17, 0)]
    [DataRow(3, 3, 0)]
    
    [DataRow(65, 123, 1)]
    [DataRow(1001, 9907, -1)]
    [DataRow(5, 21, 1)]
    [DataRow(19, 45, 1)]
    
    public void TestJacobiSymbol(int a, int n, int expected)
    {
        BigInteger castedA = new BigInteger(a);
        BigInteger castedN = new BigInteger(n);
        BigInteger castedExpected = new BigInteger(expected);
        
        var result = JacobiSymbol(castedA, castedN);
        Assert.AreEqual(castedExpected, result);
    }
    
    [DataTestMethod]
    [DataRow("100000000019")] 
    [DataRow("1000000007")] 
    [DataRow("1000000009")] 
    [DataRow("51899")] 
    [DataRow("51907")] 
    [DataRow("51913")] 
    [DataRow("51941")] 
    public void TestPrimaryTestPositive(string n)
    {
        IPrimaryTest test = new FermatTest();
        double prob = 0.9999;

        var result = test.PrimaryTest(BigInteger.Parse(n), prob);
        Assert.AreEqual(Probability.PossiblePrimal, result);
        
        test = new MillerRabinTest();

        result = test.PrimaryTest(BigInteger.Parse(n), prob);
        Assert.AreEqual(Probability.PossiblePrimal, result);
        
        test = new SolovayStrassenTest();

        result = test.PrimaryTest(BigInteger.Parse(n), prob);
        Assert.AreEqual(Probability.PossiblePrimal, result);
    }
    
    
    [DataTestMethod]
    [DataRow("66")]
    [DataRow("1000")]
    [DataRow("27")]
    [DataRow("54")]
    [DataRow("80808080801")]
    [DataRow("8080800000000000000000080801")]
    [DataRow("561")] // Can be, cannot be. As says luck
    
    public void TestPrimaryTestNegative(string n)
    {
        IPrimaryTest test = new FermatTest();
        double prob = 0.9999;

        var result = test.PrimaryTest(BigInteger.Parse(n), prob);
        Assert.AreEqual(Probability.Composite, result);
        
        test = new MillerRabinTest();

        result = test.PrimaryTest(BigInteger.Parse(n), prob);
        Assert.AreEqual(Probability.Composite, result);
        
        test = new SolovayStrassenTest();

        result = test.PrimaryTest(BigInteger.Parse(n), prob);
        Assert.AreEqual(Probability.Composite, result);
    }
    
    // MESSAGE: remember that message should be less than bit Length of a key
    [DataTestMethod]
    [DataRow("C:\\Users\\yashelter\\Desktop\\Crypota\\UnitTests\\Tests\\Input\\message.txt")]
    [DataRow("C:\\Users\\yashelter\\Desktop\\Crypota\\UnitTests\\Tests\\Input\\message.txt")]
    [DataRow("C:\\Users\\yashelter\\Desktop\\Crypota\\UnitTests\\Tests\\Input\\message.txt")]

    public void TestRsaAlgo(string path)
    {
        var rsa = new RsaService (RsaService.PrimaryTestOption.MillerRabinTest, 0.9999, 400).GenerateKeyPair();

        byte[] data = FileUtility.GetFileInBytes(path);
        byte[] encrypted = rsa.EncryptMessage(data);
        byte[] decrypted = rsa.DecryptMessage(encrypted);

        for (int i = 0; i < data.Length; ++i)
        {
            Assert.AreEqual(data[i], decrypted[i]);
        }
        
    }
    
    
}