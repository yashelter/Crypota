using Crypota;
using static Crypota.CryptoAlgorithms;

namespace UnitTests;

[TestClass]
public sealed class DesTests
{
    [DataTestMethod]
    [DataRow(new byte[] {}, new byte[] {}, new byte[] {})]
    [DataRow(new byte[] {0xff,0x00}, new byte[] {0xff}, new byte[] {0x00})]
    public void TestSplitFunction(byte[] val, byte[] left, byte[] right )
    {
        var (l, r) = SplitToTwoParts(val);

        for (int i = 0; i < left.Length; i++)
        {
            Assert.AreEqual(l[i], left[i]);
            Assert.AreEqual(r[i], right[i]);
        }
    }
    
    
    [DataTestMethod]
    [DataRow(new byte[] {}, new int[] {}, new byte[] {})]
    [DataRow(new byte[] {0xf0}, new int[] {0,1,2,3}, new byte[] {0xf0})]
    [DataRow(new byte[] {0x0f}, new int[] {0,1,2,3}, new byte[] {0x00})]
    [DataRow(new byte[] {0x0f}, new int[] {0,7,0,7,0,7,0,7}, new byte[] {85})]
    public void TestPermuteBitsFromBiggestToSmallest(byte[] val, int[] rules, byte[] exp)
    {

        var result = PermuteBits(val, rules, 0, IndexingRules.FromBiggestToSmallest);
            
        for (int i = 0; i < result.Length; i++)
        {
            Assert.AreEqual(result[i], exp[i]);
        }
    }
    
    [DataTestMethod]
    [DataRow(new byte[] {}, new int[] {}, new byte[] {})]
    [DataRow(new byte[] {0xf0}, new int[] {0,1,2,3}, new byte[] {0x00})]
    [DataRow(new byte[] {0x0f}, new int[] {7,7,7,7, 0,1,2,3}, new byte[] {0x0f})]
    public void TestPermuteBitsFromSmallestToBiggest(byte[] val, int[] rules, byte[] exp)
    {

        var result = PermuteBits(val, rules, 0, IndexingRules.FromSmallestToBiggest);
            
        for (int i = 0; i < result.Length; i++)
        {
            Assert.AreEqual(result[i], exp[i]);
        }
    }
    
    [DataTestMethod]
    [DataRow(new byte[] {}, new int[] {}, new byte[] {})]
    [DataRow(new byte[] {0xf0}, new int[] {1,2,3,4}, new byte[] {0x00})]
    [DataRow(new byte[] {0x0f}, new int[] {1,2,3,4}, new byte[] {0xf0})]
    public void TestStartBitNumberPermuteBits(byte[] val, int[] rules, byte[] exp)
    {

        var result = PermuteBits(val, rules, 1, IndexingRules.FromSmallestToBiggest);
            
        for (int i = 0; i < result.Length; i++)
        {
            Assert.AreEqual(result[i], exp[i]);
        }
    }
    
}