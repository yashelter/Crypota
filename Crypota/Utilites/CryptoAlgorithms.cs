namespace Crypota;

public static class CryptoAlgorithms
{
    public static void Swap<T>(ref T a, ref T b)
    {
        (a, b) = (b, a);
    }
    
    
    
    public enum IndexingRules : uint{
        FromSmallestToBiggest = 0,
        FromBiggestToSmallest = 1,
    }


    public static byte GetBitOnPositon(in byte[] array, int position)
    {
        int byteIndex = position / 8;
        int bitIndex = 7 - (position % 8);
        
        return (byte) ((array[byteIndex] & (1 << bitIndex)) >> bitIndex);
    }
    
    

    public static byte[] PermuteBits(byte[] sourceValue, int[] rulesOfPermutations,
        int startBitNumber = 0, IndexingRules indexingRules = IndexingRules.FromBiggestToSmallest)
    {
        int size = rulesOfPermutations.Length / 8 + (rulesOfPermutations.Length % 8 == 0 ? 0 : 1);
        byte[] result = new byte[size];

        int byteIndex = 0;
        int bitIndex = 0;
        
        foreach (int position in rulesOfPermutations)
        {
            if (indexingRules == IndexingRules.FromSmallestToBiggest)
            {
                int invesion = sourceValue.Length * 8 - 1 - position;
                result[byteIndex] |= (byte) (GetBitOnPositon(sourceValue, invesion + startBitNumber) << (7 - bitIndex));

            }
            else
            {
                result[byteIndex] |= (byte) (GetBitOnPositon(sourceValue, position - startBitNumber) << (7 - bitIndex));

            }
  
            ++bitIndex;
            if (bitIndex == 8)
            {
                bitIndex = 0;
                ++byteIndex;
            }
        }
        
        
        return result;
    }
    

    public static (byte[] left, byte[] right) SplitToTwoParts(byte[] sourceValue)
    {
        int bytesCount = sourceValue.Length / 2;
        if (sourceValue.Length % 2 != 0)
        {
            throw new NotSupportedException("Not supported operation");
        }

        var left = new byte[bytesCount];
        var right = new byte[bytesCount];
        

        for (int i = 0; i < bytesCount; i++)
        {
            left[i] = sourceValue[i];
            right[i] = sourceValue[bytesCount + i];
        }
        
        return (left, right);
    }
    
    
    public static byte[] MergeFromTwoParts(byte[] left, byte[] right)
    {
        if (left.Length != right.Length)
        {
            throw new NotSupportedException("Not supported operation");
        }
        byte[] result = new byte[left.Length + right.Length];

        for (int i = 0; i < left.Length; i++)
        {
            result[i] = left[i];
            result[left.Length + i] = right[i];
        }
        return result;
    }
    
    
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns>ref on array a</returns>
    /// <exception cref="ArgumentException"></exception>
    public static byte[] XorTwoParts(ref byte[] a, in byte[] b)
    {
        if (a.Length != b.Length)
        {
            throw new ArgumentException("The two arrays must have the same length.");
        }
        
        for (int i = 0; i < a.Length; i++)
        {
            a[i] ^= b[i];
        }
        return a;
    }
}