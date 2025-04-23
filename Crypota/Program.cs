using System.Diagnostics;
using System.Numerics;
using Crypota.Classes;
using Crypota.Classes.DES;
using Crypota.PrimalityTests;
using static Crypota.Utilities;
using static Crypota.FileUtility;

namespace Crypota;


class Program
{
    static async Task Main(string[] args)
    {
        var filepath = @"C:\Users\yashelter\Desktop\Crypota\Crypota\UnitTests\Tests\Input\img.jpeg";
        byte[] key = new byte[8];

        byte[] message = GetFileInBytes(filepath);

        SymmetricCipher cipher = new SymmetricCipher
        (
            key: key,
            mode: CipherMode.ECB,
            padding: PaddingMode.ANSIX923,
            implementation: new Des(),
            iv: [0, 0, 0, 0, 0, 0, 0, 0],
            additionalParams: new SymmetricCipher.RandomDeltaParameters() { Delta = 3 }
        )
        {
            Key = key,
            BlockSize = 8,
            KeySize = 8,
        };

        var encrypted = cipher.EncryptMessageAsync(message).GetAwaiter().GetResult();
        var decrypted = cipher.DecryptMessageAsync(encrypted).GetAwaiter().GetResult();
    }
}