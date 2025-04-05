namespace Crypota;

public static class CryptoAlgorithms
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    /// <param name="bit"></param>
    /// <param name="position">Исчисление справа начиная с 0</param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static byte SetBit(byte value, bool bit, byte position)
    {
        if (position > 7)
        {
            throw new ArgumentOutOfRangeException(nameof(position), "Position must be between 0 and 7.");
        }
        
        
        int mask = 1 << position;

        return bit 
            ? (byte)(value | mask)
            : (byte)(value & ~mask);
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    /// <param name="bit"></param>
    /// <param name="position">Исчисление справа начиная с 0</param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static byte SetBit(byte value, byte bit, byte position)
    {
        return SetBit(value, (bit & 1) == 1, position);
    }

    public static byte GetBit(byte value, byte position)
    {
        if (position > 7)
            throw new ArgumentOutOfRangeException(nameof(position), "Position must be between 0 and 7.");
        
        return (byte)((value >> position) & 1);
    }

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
        int resultSize = (permutations.Count + 7) >> 3; 
        List<byte> result = new List<byte>(Enumerable.Repeat((byte)0, resultSize));
        
        for (int i = 0; i < permutations.Count; i++)
        {
            int srcBitPos = permutations[i]; 
            
            if (srcBitPos < 0 || srcBitPos >= mas.Count * 8)
            {
                throw new ArgumentOutOfRangeException(nameof(permutations),
                    $"Invalid bit position: {srcBitPos} at total count: {mas.Count * 8}");
            }


            int srcByteIndex = srcBitPos >> 3;
            int srcBitIndex = srcBitPos % 8;
            byte srcByte = mas[srcByteIndex];
 
            int bitValue = (srcByte >> srcBitIndex) & 1;

            int destByteIndex = i >> 3;
            int destBitIndex = i % 8;

            result[destByteIndex] |= (byte)(bitValue << destBitIndex);
        }
        return result;
    }
    
    public enum IndexingRules : uint{
        FromSmallestToBiggest = 0,
        FromBiggestToSmallest = 1,
    }

    public static List<byte> PermuteBits(List<byte> sourceValue, List<int> rulesOfPermutations,
        int startBitNumber = 0, IndexingRules indexingRules = IndexingRules.FromBiggestToSmallest)
    {
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

    // 0000_1111 -> 0000 left, 1111 right. Returns part shifted to left
    private static byte GetLeftByteHalf(byte value)
    {
        return (byte)(value >> 4);
    }
    
    private static byte GetRightByteHalf(byte value)
    {
        return (byte)(value & 0x0F);
    }

    public static List<byte> BCycleShiftLeft(List<byte> sourceValue, int shiftCount)
    {
        // shiftCount =  shiftCount % (sourceValue.Count << 3); // -o2
        
        if (shiftCount > 7)
        {
            sourceValue = BCycleShiftLeft(sourceValue, shiftCount - 7);
            shiftCount = 7;
        }

        if (shiftCount == 0)
        {
            return [..sourceValue];
        }
        
        List<byte> result = new List<byte>(sourceValue.Count);

        byte mask = (byte) ((1 << shiftCount) - 1);
        for (int i = 0; i < sourceValue.Count - 1; i++)
        {
            result.Add((byte)((sourceValue[i] << shiftCount) | ((sourceValue[i + 1] >> (8 - shiftCount)) & mask)));
        }
        result.Add((byte)((sourceValue[^1] << shiftCount) | ((sourceValue[0] >> (8 - shiftCount)) & mask)));

        return result;
    }
    public static List<byte> CycleShiftLeft(List<byte> data, int shifts)
    {
        if (data.Count != 4)
            throw new ArgumentException("Data must be 4 bytes (32 bits) to represent 28-bit half-key.");

        // Объединяем байты в 32-битное число (старший байт первый)
        uint value = (uint)((data[0] << 24) | (data[1] << 16) | (data[2] << 8) | data[3]);
    
        // Маскируем старшие 4 бита, оставляя только 28 бит
        value &= 0x0FFFFFFF;
    
        // Нормализуем shifts (на случай shifts >= 28)
        shifts %= 28;
    
        // Циклический сдвиг влево
        value = (value << shifts) | (value >> (28 - shifts));
    
        // Снова маскируем, чтобы убрать лишние биты после сдвига
        value &= 0x0FFFFFFF;
    
        // Преобразуем обратно в массив байтов (старший байт первый)
        byte[] result = new byte[4];
        result[0] = (byte)((value >> 24) & 0xFF); // Старший байт
        result[1] = (byte)((value >> 16) & 0xFF);
        result[2] = (byte)((value >> 8)  & 0xFF);
        result[3] = (byte)(value         & 0xFF); // Младший байт
    
        return new List<byte>(result);
    }


    public static List<byte> ShiftRightWithExpand(List<byte> sourceValue, int shiftCount)
    {
        shiftCount %= (sourceValue.Count << 3);
        
        if (shiftCount > 7)
        {
            sourceValue = ShiftRightWithExpand(sourceValue, shiftCount - 7);
            shiftCount = 7;
        }

        if (shiftCount == 0)
        {
            return [..sourceValue];
        }
        
        List<byte> result = new List<byte>(sourceValue.Count);

        result.Add((byte)((sourceValue[0] >> shiftCount)));
        byte mask = (byte)(~(0xFF >> shiftCount));
        
        for (int i = 1; i < sourceValue.Count; i++)
        {
            result.Add((byte)((sourceValue[i] >> shiftCount) | ((sourceValue[i-1] << (8-shiftCount)) & mask)));
        }
        
       // result.Add((byte)((sourceValue[^1] << (8 - shiftCount))));
        
        return result;
    }

    public enum ShiftDirection: int
    {
        Left = 0,
        Right = 1
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
    
    
    public static List<byte> MergeTwoParts(List<byte> left, List<byte> right, int bitCnt)
    {
        if (left.Count != right.Count) throw new ArgumentException("Left and right arrays must have the same length");
        
        List<byte> result = new List<byte>(left.Count + right.Count);
        result.AddRange(left);
        int shift = bitCnt % 8;
        
        if (shift == 0)
        {
            result.AddRange(right);
            return result;
        }

        var shiftedRight = ShiftRightWithExpand(right, shift);
        result[^1] |= shiftedRight[0];
        
        result.AddRange(shiftedRight.Skip(1));

        return result;
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