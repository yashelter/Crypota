using Crypota.Classes;
using Crypota.Classes.DES;

namespace Crypota.Tests;

public static class DesTests
{
    public static CipherMode mode;
    public static PaddingMode padding;
    
    public static async Task TestDeal(string inputPath)
    {
        List<byte> key = [0xff, 0x00, 0x12, 0x11, 0x12, 0x0f, 0x11, 0xaa, 0xff, 0xff, 0x12, 0x00, 0x12, 0xff, 0x11, 0xaa];
        
        var cipher = new SymmetricCipher
        (
            key, 
            mode, 
            padding, 
            new Deal128(),
            [0x00, 0xff, 0x00, 0x00, 0x00, 0x0f, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0x00, 0x00, 0x00],
            new SymmetricCipher.RandomDeltaParameters() { Delta = 13});

        cipher.BlockSize = 16;
        cipher.KeySize = 16;
        
        var data = FileUtility.GetFileInBytes(inputPath);
        
        var encrypted   = await cipher.EncryptMessageAsync(new List<byte>(data));
        var decrypted = await cipher.DecryptMessageAsync(encrypted);
        
        var outputPath = FileUtility.AddPrefixBeforeExtension(inputPath, $"_deal_{padding}_{mode}_")?.Replace("Input", "Output");
        
        FileUtility.WriteBytesToFile(outputPath, decrypted.ToArray());
    }
    
    public static async Task TestDes(string inputPath)
    {
        List<byte> key = [0xff, 0x21, 0x12, 0xff, 0x00, 0x00, 0x0f, 0xff];
        
        var cipher = new SymmetricCipher
        (
            key,
            mode, 
            padding, 
            new Des(), 
            [0x00, 0x00, 0xff, 0x00, 0x00, 0x00, 0x00, 0x00],
            new SymmetricCipher.RandomDeltaParameters() { Delta = 13});

        var data = FileUtility.GetFileInBytes(inputPath);
        
        var encrypted   = await cipher.EncryptMessageAsync([..data]);
        var decrypted = await cipher.DecryptMessageAsync(encrypted);
        
        var outputPath = FileUtility.AddPrefixBeforeExtension(inputPath, $"_des_{padding}_{mode}_")?.Replace("Input", "Output");
        
        FileUtility.WriteBytesToFile(outputPath, decrypted.ToArray());
    }
    
}