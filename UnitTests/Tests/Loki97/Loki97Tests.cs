namespace UnitTests.Tests.Loki97;

[TestClass]
public sealed class Loki97Tests
{
    private static readonly Random _random = new Random();

    [DataTestMethod]
    [DataRow(192, DisplayName = "Encrypt/Decrypt 192 Random 1")]
    [DataRow(192, DisplayName = "Encrypt/Decrypt 192 Random 2")]
    [DataRow(192, DisplayName = "Encrypt/Decrypt 192 Random 3")]
    public void Loki97_EncryptDecrypt_RandomData(int keySizeBits, int blockSizeBits=128)
    {
        int keySizeBytes = keySizeBits / 8;
        int blockSizeBytes = blockSizeBits / 8;

        byte[] key = new byte[keySizeBytes];
        byte[] originalMessage = new byte[blockSizeBytes];

        _random.NextBytes(key);
        _random.NextBytes(originalMessage);

        byte[] messageToProcess = (byte[])originalMessage.Clone();

        Crypota.Symmetric.Loki97.Loki97 alg = new Crypota.Symmetric.Loki97.Loki97
        {
            Key = key
        };

        
        alg.EncryptBlock(messageToProcess);
        alg.DecryptBlock(messageToProcess);

        CollectionAssert.AreEqual(originalMessage, messageToProcess,
            $"Decryption failed for KeySize={keySizeBits}, BlockSize={blockSizeBits}. Decrypted data does not match original.");
    }
}