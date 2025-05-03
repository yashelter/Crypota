using Crypota.Symmetric.Rc6;

namespace UnitTests.Tests.Rc6;

[TestClass]
public class Rc6Tests
{
    private static readonly Random _random = new Random();

    [DataTestMethod]
    [DataRow(128, DisplayName = "Encrypt/Decrypt 128 Random")]
    [DataRow(192, DisplayName = "Encrypt/Decrypt 192 Random")]
    [DataRow(256, DisplayName = "Encrypt/Decrypt 256 Random")]
    public void Rc6_EncryptDecrypt_RandomData(int keySizeBits, int blockSizeBits=128)
    {
        int keySizeBytes = keySizeBits / 8;
        int blockSizeBytes = blockSizeBits / 8;

        byte[] key = new byte[keySizeBytes];
        byte[] originalMessage = new byte[blockSizeBytes];

        _random.NextBytes(key);
        _random.NextBytes(originalMessage);

        byte[] messageToProcess = (byte[])originalMessage.Clone();

        Crypota.Symmetric.Rc6.Rc6 alg = new Crypota.Symmetric.Rc6.Rc6
        {
            Key = key
        };

        
        alg.EncryptBlock(messageToProcess);
        alg.DecryptBlock(messageToProcess);

        CollectionAssert.AreEqual(originalMessage, messageToProcess,
            $"Decryption failed for KeySize={keySizeBits}, BlockSize={blockSizeBits}. Decrypted data does not match original.");
    }
}