using Crypota.Symmetric;
using Crypota.Symmetric.Deal;
using static Crypota.SymmetricUtils;
using static Crypota.FileUtility;

namespace UnitTests.Tests.Des;

[TestClass]
public sealed class DesTests
{
    private const string Filepath1 = "C:\\Users\\yashelter\\Desktop\\Crypota\\UnitTests\\Input\\message.txt";
    private const string Filepath2 = "C:\\Users\\yashelter\\Desktop\\Crypota\\UnitTests\\Input\\img.jpeg";
    private const string Filepath3 = "C:\\Users\\yashelter\\Desktop\\Crypota\\UnitTests\\Input\\Twofish.pdf";
    
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

        var result = PermuteBits(val, rules, 0, IndexingRules.FromLeftToRight);

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

        var result = PermuteBits(val, rules, 0, IndexingRules.FromRightToLeft);

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

        var result = PermuteBits(val, rules, 1, IndexingRules.FromRightToLeft);

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
        des.EncryptBlock(message);

        for (int i = 0; i < message.Length; i++)
        {
            Assert.AreEqual(ArrayToHexString(message), expected);
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
        var data = message.ToArray();
        
        des.EncryptBlock(message);
        des.DecryptBlock(message);

        for (int i = 0; i < message.Length; i++)
        {
            Assert.AreEqual(data[i], message[i]);
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
        var data = message.ToArray();
        
        deal128.EncryptBlock(message);
        deal128.DecryptBlock(message);

        for (int i = 0; i < message.Length; i++)
        {
            Assert.AreEqual(message[i], data[i]);
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
            mode: CipherMode.CFB,
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
    [DataRow(Filepath1)]
    [DataRow(Filepath2)]
    public void TestOneModeDesWithFile(string filepath)
    {
        byte[] key = new byte[8];

        byte[] message = GetFileInBytes(filepath);
        byte[] expected = GetFileInBytes(filepath);

        SymmetricCipher cipher = new SymmetricCipher
        (
            key: key,
            mode: CipherMode.ECB,
            padding: PaddingMode.Zeros,
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
            Assert.AreEqual(expected[k], decrypted[k]);
        }
    }



    [DataTestMethod]
    [DataRow(Filepath3)]
    public void TestAllModesDesWithFile(string filepath)
    {
        byte[] key = new byte[8];

        byte[] message = GetFileInBytes(filepath);

        for (int i = 0; i < 4; i++)
        {
            PaddingMode pm = (PaddingMode)i;
            for (int j = 0; j < 7; j++)
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
                    Assert.AreEqual(message[k], decrypted[k], $"Wrong data for cipher mode: {cm.ToString()}, " +
                                                              $"padding mode: {pm.ToString()}");
                }

            }
        }
    }

    [DataTestMethod]
    [DataRow(Filepath1)]
    public void TestAllModesDealWithFile(string filepath)
    {
        byte[] key = new byte[16];

        byte[] message = GetFileInBytes(filepath);

        for (int i = 1; i < 4; i++)
        {
            PaddingMode pm = (PaddingMode)i;
            for (int j = 0; j < 7; j++)
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
                    Assert.AreEqual(message[k], decrypted[k], $"Wrong data for cipher mode: {cm.ToString()}, " +
                                                              $"padding mode: {pm.ToString()}");
                }

            }

        }

    }
}