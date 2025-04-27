using Crypota.Symmetric;
using Crypota.Symmetric.Deal;
using static Crypota.SymmetricMath;
using static Crypota.FileUtility;

namespace UnitTests.Des;

[TestClass]
public sealed class DesTests
{
    [DataTestMethod]
    [DataRow(new byte[] { }, new byte[] { }, new byte[] { })]
    [DataRow(new byte[] { 0xff, 0x00 }, new byte[] { 0xff }, new byte[] { 0x00 })]
    public void TestSplitFunction(byte[] val, byte[] left, byte[] right)
    {
        var (l, r) = SplitToTwoParts(val);

        for (int i = 0; i < left.Length; i++)
        {
            Assert.AreEqual(l[i], left[i]);
            Assert.AreEqual(r[i], right[i]);
        }
    }


    [DataTestMethod]
    [DataRow(new byte[] { }, new int[] { }, new byte[] { })]
    [DataRow(new byte[] { 0xf0 }, new int[] { 0, 1, 2, 3 }, new byte[] { 0xf0 })]
    [DataRow(new byte[] { 0x0f }, new int[] { 0, 1, 2, 3 }, new byte[] { 0x00 })]
    [DataRow(new byte[] { 0x0f }, new int[] { 0, 7, 0, 7, 0, 7, 0, 7 }, new byte[] { 85 })]
    public void TestPermuteBitsFromBiggestToSmallest(byte[] val, int[] rules, byte[] exp)
    {

        var result = PermuteBits(val, rules, 0, IndexingRules.FromBiggestToSmallest);

        for (int i = 0; i < result.Length; i++)
        {
            Assert.AreEqual(result[i], exp[i]);
        }
    }

    [DataTestMethod]
    [DataRow(new byte[] { }, new int[] { }, new byte[] { })]
    [DataRow(new byte[] { 0xf0 }, new int[] { 0, 1, 2, 3 }, new byte[] { 0x00 })]
    [DataRow(new byte[] { 0x0f }, new int[] { 7, 7, 7, 7, 0, 1, 2, 3 }, new byte[] { 0x0f })]
    public void TestPermuteBitsFromSmallestToBiggest(byte[] val, int[] rules, byte[] exp)
    {

        var result = PermuteBits(val, rules, 0, IndexingRules.FromSmallestToBiggest);

        for (int i = 0; i < result.Length; i++)
        {
            Assert.AreEqual(result[i], exp[i]);
        }
    }

    [DataTestMethod]
    [DataRow(new byte[] { }, new int[] { }, new byte[] { })]
    [DataRow(new byte[] { 0xf0 }, new int[] { 1, 2, 3, 4 }, new byte[] { 0x00 })]
    [DataRow(new byte[] { 0x0f }, new int[] { 1, 2, 3, 4 }, new byte[] { 0xf0 })]
    public void TestStartBitNumberPermuteBits(byte[] val, int[] rules, byte[] exp)
    {

        var result = PermuteBits(val, rules, 1, IndexingRules.FromSmallestToBiggest);

        for (int i = 0; i < result.Length; i++)
        {
            Assert.AreEqual(result[i], exp[i]);
        }
    }

    [DataTestMethod]
    [DataRow(new byte[] { 0b10000000 }, 1, 8, new byte[] { 0b00000001 })]
    [DataRow(new byte[] { 0b10000000 }, 9, 8, new byte[] { 0b00000001 })]
    [DataRow(new byte[] { 0b11000000 }, 1, 7, new byte[] { 0b10000010 })]
    public void TestCycleLeftShift(byte[] val, int shift, int bits, byte[] exp)
    {

        var result = CycleLeftShift(ref val, shift, bits);

        for (int i = 0; i < result.Length; i++)
        {
            Assert.AreEqual(result[i], exp[i]);
        }
    }

    [DataTestMethod]
    [DataRow(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 }, new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 }, "8CA64DE9C1B123A7")]
    [DataRow(new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 }, new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 }, "166B40B44ABA4BD6")]
    [DataRow(new byte[] { 0, 0, 0, 0, 0, 0, 0, 2 }, new byte[] { 0, 0, 0, 0, 0, 0, 0, 2 }, "050A9DC3FC0189AC")]
    [DataRow(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 }, new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 }, "0D9F279BA5D87260")]
    public void TestDesAlgorithm(byte[] key, byte[] message, string expected)
    {
        Crypota.Symmetric.Des.Des des = new Crypota.Symmetric.Des.Des() { Key = key };
        var encrypted = des.EncryptBlock(message);

        for (int i = 0; i < message.Length; i++)
        {
            Assert.AreEqual(ArrayToHexString(encrypted), expected);
        }
    }


    [DataTestMethod]
    [DataRow(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 }, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 })]
    [DataRow(new byte[] { 0, 45, 0, 0, 0, 0, 0, 0 }, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 })]
    [DataRow(new byte[] { 0, 0, 43, 0, 0, 0, 0, 0 }, new byte[] { 255, 255, 255, 255, 255, 255, 255, 255 })]
    [DataRow(new byte[] { 0, 1, 0, 24, 0, 1, 0, 12 }, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 })]
    public void TestDesSingle(byte[] key, byte[] message)
    {
        Crypota.Symmetric.Des.Des des = new Crypota.Symmetric.Des.Des() { Key = key };
        var encrypted = des.EncryptBlock(message);
        var decrypted = des.DecryptBlock(encrypted);

        for (int i = 0; i < message.Length; i++)
        {
            Assert.AreEqual(message[i], decrypted[i]);
        }
    }

    [DataTestMethod]
    [DataRow(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
        new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 })]
    [DataRow(new byte[] { 0, 0, 1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 2, 0 },
        new byte[] { 0, 0, 0, 0, 0, 0, 4, 0, 0, 0, 0, 0, 6, 0, 0, 0 })]
    [DataRow(new byte[] { 0, 0, 23, 1, 0, 0, 1, 0, 1, 0, 0, 0, 0, 0, 0, 0 },
        new byte[] { 0, 3, 0, 0, 0, 01, 0, 01, 0, 0, 0, 0, 6, 0, 0, 0 })]
    [DataRow(new byte[] { 0, 23, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 23, 0, 0, 0 },
        new byte[] { 0, 4, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1 })]
    public void TestDealSingle(byte[] key, byte[] message)
    {
        Deal128 deal128 = new Deal128() { Key = key };
        var encrypted = deal128.EncryptBlock(message);
        var decrypted = deal128.DecryptBlock(encrypted);

        for (int i = 0; i < message.Length; i++)
        {
            Assert.AreEqual(message[i], decrypted[i]);
        }
    }

    [DataTestMethod]
    [DataRow(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 }, new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 })]
    [DataRow(new byte[] { 0, 45, 0, 0, 0, 0, 0, 0 }, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 })]
    [DataRow(new byte[] { 0, 0, 43, 0, 0, 0, 0, 0 }, new byte[] { 255, 255, 255, 255, 255, 255, 255, 255 })]
    [DataRow(new byte[] { 0, 1, 0, 24, 0, 1, 0, 12 }, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 })]
    public void TestDesAsyncTest(byte[] key, byte[] message)
    {
        SymmetricCipher cipher = new SymmetricCipher
        (
            key: key,
            mode: CipherMode.PCBC,
            padding: 0,
            implementation: new Crypota.Symmetric.Des.Des(),
            iv: [0, 0, 0, 0, 0, 0, 0, 0],
            additionalParams: new SymmetricCipher.RandomDeltaParameters() { Delta = 3 }
        )
        {
            Key = key,
        };

        var encrypted = cipher.EncryptMessageAsync(message).GetAwaiter().GetResult();
        var decrypted = cipher.DecryptMessageAsync(encrypted).GetAwaiter().GetResult();

        for (int i = 0; i < message.Length; i++)
        {
            Assert.AreEqual(message[i], decrypted[i]);
        }
    }

    [DataTestMethod]
    [DataRow(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 })]
    [DataRow(new byte[] { 0, 45, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, new byte[] { 1, 2, 3, 4, 5, 6, 7 })]
    [DataRow(new byte[] { 0, 0, 43, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
        new byte[] { 255, 255, 255, 255, 255, 255, 255, 255 })]
    [DataRow(new byte[] { 0, 1, 0, 24, 0, 1, 0, 12, 0, 0, 0, 0, 0, 0, 0, 0 }, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 })]
    public void TestDealAsync(byte[] key, byte[] message)
    {
        SymmetricCipher cipher = new SymmetricCipher
        (
            key: key,
            mode: CipherMode.OFB,
            padding: PaddingMode.ANSIX923,
            implementation: new Deal128(),
            iv: [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
            additionalParams: new SymmetricCipher.RandomDeltaParameters() { Delta = 3 }
        )
        {
            Key = key,
        };

        var encrypted = cipher.EncryptMessageAsync(message).GetAwaiter().GetResult();
        var decrypted = cipher.DecryptMessageAsync(encrypted).GetAwaiter().GetResult();

        for (int i = 0; i < message.Length; i++)
        {
            Assert.AreEqual(message[i], decrypted[i]);
        }
    }

    [DataTestMethod]
    [DataRow(@"C:\Users\yashelter\Desktop\Crypota\UnitTests\Tests\Input\img.jpeg")]
    [DataRow(@"C:\Users\yashelter\Desktop\Crypota\UnitTests\Tests\Input\Twofish.pdf")]
    public void TestOneModeDesWithFile(string filepath)
    {
        byte[] key = new byte[8];

        byte[] message = GetFileInBytes(filepath);

        SymmetricCipher cipher = new SymmetricCipher
        (
            key: key,
            mode: CipherMode.ECB,
            padding: PaddingMode.ANSIX923,
            implementation: new Crypota.Symmetric.Des.Des(),
            iv: [0, 0, 0, 0, 0, 0, 0, 0],
            additionalParams: new SymmetricCipher.RandomDeltaParameters() { Delta = 3 }
        )
        {
            Key = key,
        };

        var encrypted = cipher.EncryptMessageAsync(message).GetAwaiter().GetResult();
        var decrypted = cipher.DecryptMessageAsync(encrypted).GetAwaiter().GetResult();

        for (int k = 0; k < message.Length; k++)
        {
            Assert.AreEqual(message[k], decrypted[k]);
        }
    }



    [DataTestMethod]
    [DataRow(@"C:\Users\yashelter\Desktop\Crypota\UnitTests\Tests\Input\Twofish.pdf")]
    public void TestAllModesDesWithFile(string filepath)
    {
        byte[] key = new byte[8];

        byte[] message = GetFileInBytes(filepath);

        for (int i = 0; i < 4; i++)
        {
            PaddingMode pm = (PaddingMode)i;
            for (int j = 0; j < 6; j++)
            {
                CipherMode cm = (CipherMode)j;
                SymmetricCipher cipher = new SymmetricCipher
                (
                    key: key,
                    mode: cm,
                    padding: pm,
                    implementation: new Crypota.Symmetric.Des.Des(),
                    iv: [0, 0, 0, 0, 0, 0, 0, 0],
                    additionalParams: new SymmetricCipher.RandomDeltaParameters() { Delta = 3 }
                )
                {
                    Key = key,
                };

                var encrypted = cipher.EncryptMessageAsync(message).GetAwaiter().GetResult();
                var decrypted = cipher.DecryptMessageAsync(encrypted).GetAwaiter().GetResult();

                for (int k = 0; k < message.Length; k++)
                {
                    Assert.AreEqual(message[k], decrypted[k]);
                }

            }
        }
    }

    [DataTestMethod]
    [DataRow(@"C:\Users\yashelter\Desktop\Crypota\UnitTests\Tests\Input\message.txt")]
    public void TestAllModesDealWithFile(string filepath)
    {
        byte[] key = new byte[16];

        byte[] message = GetFileInBytes(filepath);

        for (int i = 0; i < 4; i++)
        {
            PaddingMode pm = (PaddingMode)i;
            for (int j = 0; j < 6; j++)
            {
                CipherMode cm = (CipherMode)j;
                SymmetricCipher cipher = new SymmetricCipher
                (
                    key: key,
                    mode: cm,
                    padding: pm,
                    implementation: new Deal128(),
                    iv: [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
                    additionalParams: new SymmetricCipher.RandomDeltaParameters() { Delta = 3 }
                )
                {
                    Key = key,
                };

                var encrypted = cipher.EncryptMessageAsync(message).GetAwaiter().GetResult();
                var decrypted = cipher.DecryptMessageAsync(encrypted).GetAwaiter().GetResult();

                for (int k = 0; k < message.Length; k++)
                {
                    Assert.AreEqual(message[k], decrypted[k]);
                }

            }

        }

    }
}