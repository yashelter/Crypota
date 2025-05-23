using System.Text;

namespace Crypota.CryptoMath;

public static class SymmetricUtils
{
    public static void Swap<T>(ref T a, ref T b)
    {
        (a, b) = (b, a);
    }



    public enum IndexingRules : uint
    {
        FromRightToLeft = 0,
        FromLeftToRight = 1,
    }


    public static byte GetBitOnPositon(Span<byte> array, int position)
    {
        int byteIndex = position / 8;
        int bitIndex = 7 - (position % 8);

        return (byte)((array[byteIndex] & (1 << bitIndex)) >> bitIndex);
    }
    
    public static void XorInPlace(Span<byte> target, ReadOnlySpan<byte> source)
    {
        if (target.Length != source.Length)
            throw new ArgumentException("Spans must have the same length for XOR operation.");
        for (int i = 0; i < target.Length; i++)
        {
            target[i] ^= source[i];
        }
    }
    

    public static byte[] PermuteBits(Span<byte> sourceValue, int[] rulesOfPermutations,
        int startBitNumber = 0, IndexingRules indexingRules = IndexingRules.FromLeftToRight)
    {
        int size = rulesOfPermutations.Length / 8 + (rulesOfPermutations.Length % 8 == 0 ? 0 : 1);
        byte[] result = new byte[size];

        int byteIndex = 0;
        int bitIndex = 0;

        foreach (int position in rulesOfPermutations)
        {
            if (indexingRules == IndexingRules.FromRightToLeft)
            {
                int invesion = sourceValue.Length * 8 - 1 - position;
                result[byteIndex] |= (byte)(GetBitOnPositon(sourceValue, invesion + startBitNumber) << (7 - bitIndex));

            }
            else
            {
                result[byteIndex] |= (byte)(GetBitOnPositon(sourceValue, position - startBitNumber) << (7 - bitIndex));
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
    /// Вставка происходит либо в левые 4 бита, либо в правые
    /// </summary>
    /// <param name="array"></param>
    /// <param name="value">Значение берётся из последних 4 битов</param>
    /// <param name="bitStartPosition">Позиция начального бита в array, должна быть кратна 4</param>
    /// <returns></returns>
    public static byte[] SetNextFourBits(ref byte[] array, byte value, int bitStartPosition)
    {
        value &= 0x0F;
        int byteN = bitStartPosition >> 3;
        int bitN = bitStartPosition % 8;

        if (bitN == 4)
        {
            array[byteN] = (byte)(array[byteN] | value);
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


    /// <summary>
    /// 
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns>ref on array a</returns>
    /// <exception cref="ArgumentException"></exception>
    public static Span<byte> XorTwoParts(Span<byte> a, Span<byte> b)
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
    
    public static byte[] XorTwoPartsCopy(byte[] a, byte[] b)
    {
        if (a.Length != b.Length)
        {
            throw new ArgumentException("The two arrays must have the same length.");
        }
        var result = new byte[a.Length];

        for (int i = 0; i < a.Length; i++)
        {
            result[i] = (byte) (a[i] ^ b[i]);
        }

        return result;
    }


    public static byte[] CycleLeftShift(ref byte[] a, int shift, int bits)
    {
        if (a == null)
        {
            throw new ArgumentNullException(nameof(a));
        }

        if (bits < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bits), "Number of bits cannot be negative.");
        }

        if (shift < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(shift), "Shift amount cannot be negative.");
        }

        if (bits > a.Length * 8)
        {
            throw new ArgumentOutOfRangeException(nameof(bits),
                "Number of bits cannot exceed the array's total bit capacity.");
        }

        if (bits == 0 || a.Length == 0)
        {
            return a;
        }

        int effectiveShift = shift % bits;

        if (effectiveShift == 0)
        {
            return a;
        }

        int byteCount = (bits + 7) / 8;
        byte[] temp = new byte[byteCount];
        Array.Copy(a, 0, temp, 0, byteCount);


        int bitsInLastByte = bits % 8;
        byte lastByteMask = 0xFF;
        if (bitsInLastByte != 0)
        {
            lastByteMask = (byte)(0xFF << (8 - bitsInLastByte));
            temp[byteCount - 1] &= lastByteMask;
        }

        for (int i = 0; i < byteCount - 1; i++)
        {
            a[i] = 0;
        }

        if (byteCount > 0)
        {
            if (bitsInLastByte == 0)
            {
                a[byteCount - 1] = 0;
            }
            else
            {
                byte clearMask = (byte)~lastByteMask;
                a[byteCount - 1] &= clearMask;
            }
        }

        for (int destBitIndex = 0; destBitIndex < bits; destBitIndex++)
        {
            int srcBitIndex = (destBitIndex + effectiveShift) % bits;

            int srcByteIndex = srcBitIndex / 8;
            int srcBitWithinByte = srcBitIndex % 8;
            byte srcMask = (byte)(1 << (7 - srcBitWithinByte));

            bool bitIsSet = (temp[srcByteIndex] & srcMask) != 0;

            if (bitIsSet)
            {
                int destByteIndex = destBitIndex / 8;
                int destBitWithinByte = destBitIndex % 8;
                byte destMask = (byte)(1 << (7 - destBitWithinByte));
                a[destByteIndex] |= destMask;
            }
        }

        return a;
    }

    public static string ArrayToHexString(byte[] array)
    { 
        StringBuilder builder = new StringBuilder();
        array.ToList().ForEach(b => builder.Append(b.ToString("X2")));
        return builder.ToString();
    }
    
    public static void XorBlocks(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b, Span<byte> destination)
    {
        if (a.Length != b.Length || a.Length != destination.Length)
        {
            throw new ArgumentException("All spans must have the same length for XOR operation.");
        }

        for (int i = 0; i < destination.Length; i++)
        {
            destination[i] = (byte)(a[i] ^ b[i]);
        }
    }

}