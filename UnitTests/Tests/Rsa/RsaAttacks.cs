using System.Numerics;
using Crypota;
using Crypota.CryptoMath;
using Crypota.RSA;
using Crypota.RSA.Examples;
using Crypota.RSA.HackingTheGate;

namespace UnitTests;


[TestClass]

public sealed class RsaAttacks
{
    [DataTestMethod]
    [DataRow("4", "2")]
    [DataRow("5", "2")]
    [DataRow("65538", "256")]
    [DataRow("999999999998000000000001", "999999999999")]
    [DataRow("999999999998000000000002", "999999999999")]
    public void TestSqrt(string num, string sqrt)
    {
        BigInteger a = BigInteger.Parse(num);
        BigInteger expected = BigInteger.Parse(sqrt);

        var result = CryptoMath.Sqrt(a);
        Assert.AreEqual(expected, result);
    }
    
    
    [DataTestMethod]
    [DataRow(64)]
    [DataRow(128)]
    [DataRow(256)]
    [DataRow(512)]
    [DataRow(1024)]
    public void TestFermatAttack(int keySize)
    {
        var rsa = new WeakRsaService (WeakRsaService.PrimaryTestOption.MillerRabinTest, 0.9999, keySize).GenerateKeyPair();
        Console.WriteLine("Key pair generated");
        
        var (e, d, n, phi) = rsa.KeyPair;
        var (probD, probPhi) = AttackOnFermat.HackTheGate(e, n);

        Assert.AreEqual(probD, d);
        Assert.AreEqual(probPhi, phi);
    }
    
    
    [DataTestMethod]
    [DataRow(64)]
    [DataRow(128)]
    [DataRow(256)]
    [DataRow(512)]
    [DataRow(1024)]
    public void TestWienerAttack(int keySize)
    {
        var rsa = new WeakRsaService (WeakRsaService.PrimaryTestOption.MillerRabinTest, 0.99999, keySize).GenerateKeyPair();
        Console.WriteLine("Key pair generated");
        
        var (e, d, n, phi) = rsa.KeyPair;
        Console.WriteLine($"Real: d:{d}, n:{n}, e:{e}");
        
        var (probD, probPhi, lst) = AttackOnWiener.HackTheGate(e, n);

        Assert.AreEqual(probD, d);
        Assert.AreEqual(probPhi, phi);
    }
}