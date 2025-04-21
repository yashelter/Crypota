namespace Crypota;

public static class CryptoAlgorithms
{
    public static byte GetBitInArray(List<byte> value, int position)
    {
        int byteN = position >> 3;
        int bitN = position % 8;

        return (byte)GetBit(value[byteN], (byte) bitN);
    }
    
    public static byte GetByteInArray(List<byte> value, int bitPosition)
    {
        int byteN = bitPosition >> 3;

        return (byte)value[byteN];
    }

    /// <summary>
    /// Вставка происходит либо в левые 4 бита, либо в правые
    /// </summary>
    /// <param name="array"></param>
    /// <param name="value">Значение берётся из последних 4 битов</param>
    /// <param name="bitStartPosition">Позиция начального бита в array, должна быть кратна 4</param>
    /// <returns></returns>
    public static List<byte> SetNextFourBits(List<byte> array, byte value, int bitStartPosition)
    {
        value &= 0x0F;
        int byteN = bitStartPosition >> 3;
        int bitN = bitStartPosition % 8;

        if (bitN == 4)
        {
            array[byteN] = (byte) (array[byteN] | value);
        }
        else if (bitN == 0)
        {
            array[byteN] = (byte)((array[byteN] & 0x0F) | (value << 4));
        }
        else
        {
            throw new ArgumentException("Invalid bitPosition");
        }

        return array;
    }

    public static void Swap<T>(ref T a, ref T b)
    {
        (a, b) = (b, a);
    }
    
    /// <summary>
    /// Если будет не совпадение индексов и размеров кинет исключение
    /// </summary>
    /// <param name="mas">Байты</param>
    /// <param name="permutations"> В массиве лежат индексы из исходного, которые ставим на i-ую позицию</param>
    private static List<byte> PermuteBitsNoConfig(List<byte> mas, List<int> permutations)
    {
        // TODO
        throw new NotImplementedException();
    }
    
    public enum IndexingRules : uint{
        FromSmallestToBiggest = 0,
        FromBiggestToSmallest = 1,
    }

    public static List<byte> PermuteBits(List<byte> sourceValue, List<int> rulesOfPermutations,
        int startBitNumber = 0, IndexingRules indexingRules = IndexingRules.FromBiggestToSmallest)
    {
        // TODO
        List<int> rules = new List<int>(rulesOfPermutations);
        
        if (startBitNumber != 0)
        {
            for (int i = 0; i < rules.Count; i++)
            {
                rules[i] -= startBitNumber;
            }
        }

        if (indexingRules != IndexingRules.FromBiggestToSmallest)
        {
            for (int i = 0; i < rules.Count; i++)
            {
               rules[i] = rulesOfPermutations.Count - 1 - rules[i];
            }
        }
        return PermuteBitsNoConfig(sourceValue, rules);
    }
    

    // bitsSize is total bits
    public static (List<byte> left, List<byte> right, int bitsSize) SplitToTwoParts(List<byte> sourceValue)
    {
        int bytesCount = sourceValue.Count >> 1;
        int bitesCount = (sourceValue.Count << 2) - (bytesCount << 3); // it's 4 or 0

        if (bitesCount != 4 && bitesCount != 0)
        {
            throw new NotSupportedException("Function was written wrong!");
        }

        var left = new List<byte>();
        var right = new List<byte>();

        int bitsSize = bitesCount + (bytesCount << 3);

        for (int i = 0; i < bytesCount; i++)
        {
            left.Add(sourceValue[i]);
        }

        if (bitesCount > 0)
        {
            left.Add((byte)(GetLeftByteHalf(sourceValue[bytesCount]) << 4));
            right.Add((byte)(GetRightByteHalf(sourceValue[bytesCount]) << 4));
            int j = 0;
            for (int i = bytesCount; i < sourceValue.Count - 1; ++i, ++j)
            {
                right[j] = (byte) (right[j] | GetLeftByteHalf(sourceValue[bytesCount + 1]));
                right.Add((byte)(GetRightByteHalf(sourceValue[bytesCount + 1]) << 4));
            }
            //
        }
        else
        {
            for (int i = bytesCount; i < sourceValue.Count; i++)
            {
                right.Add(sourceValue[i]);
            }

        }

        return (left, right, bitsSize);
    }
    
    public static List<byte> XorTwoParts(List<byte> a, List<byte> b)
    {
        if (a.Count != b.Count)
        {
            throw new ArgumentException("The two arrays must have the same length.");
        }
        List<byte> result = new List<byte>(a.Count);
        
        for (int i = 0; i < a.Count; i++)
        {
            result.Add((byte) (a[i] ^ b[i]));
        }
        return result;
    }
}