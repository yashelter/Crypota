namespace UnitTests.Tests.Rijndael;

[TestClass]
public sealed class RijndaelTests
{
    [DataTestMethod]
    [DataRow()]
    public void Empty()
    {
        Assert.AreEqual(1, 1);
    }
}