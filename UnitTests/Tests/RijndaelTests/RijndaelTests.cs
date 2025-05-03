using RijndaelCipher = Crypota.Symmetric.Rijndael.Rijndael;
namespace UnitTests.Tests.RijndaelTests;

[TestClass]
public sealed class RijndaelTests
{
    private static readonly Random _random = new Random();

    [DataTestMethod]
    [DataRow(128, 128, DisplayName = "Encrypt/Decrypt 128/128 Random")]
    [DataRow(192, 192, DisplayName = "Encrypt/Decrypt 192/192 Random")]
    [DataRow(256, 256, DisplayName = "Encrypt/Decrypt 256/256 Random")]
    [DataRow(128, 192, DisplayName = "Encrypt/Decrypt 128/192 Random")]
    [DataRow(128, 256, DisplayName = "Encrypt/Decrypt 128/256 Random")]
    [DataRow(192, 128, DisplayName = "Encrypt/Decrypt 192/128 Random")]
    [DataRow(192, 256, DisplayName = "Encrypt/Decrypt 192/256 Random")]
    [DataRow(256, 128, DisplayName = "Encrypt/Decrypt 256/128 Random")]
    [DataRow(256, 192, DisplayName = "Encrypt/Decrypt 256/192 Random")]
    public void TestRijndael_EncryptDecrypt_RandomData(int keySizeBits, int blockSizeBits)
    {
        int keySizeBytes = keySizeBits / 8;
        int blockSizeBytes = blockSizeBits / 8;

        byte[] key = new byte[keySizeBytes];
        byte[] originalMessage = new byte[blockSizeBytes];

        _random.NextBytes(key);
        _random.NextBytes(originalMessage);

        byte[] messageToProcess = (byte[])originalMessage.Clone();

        RijndaelCipher rijndael = new RijndaelCipher
        {
            IrreduciblePolynom = 0x1B,
            BlockSizeBits = blockSizeBits,
            KeySizeBits = keySizeBits,
            Key = key
        };

        
        rijndael.EncryptBlock(messageToProcess);
        rijndael.DecryptBlock(messageToProcess);

        CollectionAssert.AreEqual(originalMessage, messageToProcess,
            $"Decryption failed for KeySize={keySizeBits}, BlockSize={blockSizeBits}. Decrypted data does not match original.");
    }
}