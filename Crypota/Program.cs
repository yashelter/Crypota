using System.Diagnostics;
using System.Numerics;
using Crypota.PrimalityTests;
using Crypota.RSA.Examples;
using Crypota.RSA.HackingTheGate;
using Crypota.Symmetric;
using Crypota.Symmetric.Rijndael;
using static Crypota.CryptoMath.CryptoMath;
using static Crypota.RSA.HackingTheGate.ChainedFraction;
using static Crypota.FileUtility;

namespace Crypota;


class Program
{
    static void Main(string[] args)
    {
        var implementation = new Crypota.Symmetric.Rijndael.Rijndael()
        {
            BlockSizeBits = 128,
            KeySizeBits = 128,
            IrreduciblePolynom = 0x1B
        };
        
        byte[] key = new byte[implementation.KeySize];
        byte[] iv = new byte[implementation.BlockSize];
        
        string filepath = "C:\\Users\\yashelter\\Desktop\\Crypota\\UnitTests\\Input\\1.gif";
        byte[] message = GetFileInBytes(filepath);
        long fileSize = message.Length;
        double fileSizeMB = (double)fileSize / (1024 * 1024);

        Console.WriteLine(
            $"--- Starting Benchmark for Rijndael on file: {Path.GetFileName(filepath)} ({fileSizeMB:F2} MB) ---");

        Stopwatch stopwatch = new Stopwatch();

        var cm = CipherMode.RD;
        var pm = PaddingMode.PKCS7;
        
        Console.WriteLine($"Testing Mode: {cm}, Padding: {pm}");

        SymmetricCipher cipher = new SymmetricCipher(key, cm, pm, implementation, iv, 
            new SymmetricCipher.RandomDeltaParameters()
        {
            Delta = 3
        })
        {
            Key = key,
        };

        byte[] encrypted = null;
        byte[] decrypted = null;
        TimeSpan encryptTime = TimeSpan.Zero;
        TimeSpan decryptTime = TimeSpan.Zero;
        bool success = false;

        try
        {
            stopwatch.Restart();

            encrypted = cipher.EncryptMessageAsync(message).GetAwaiter().GetResult();
            stopwatch.Stop();
            encryptTime = stopwatch.Elapsed;

            stopwatch.Restart();
            decrypted = cipher.DecryptMessageAsync(encrypted).GetAwaiter().GetResult();
            stopwatch.Stop();
            decryptTime = stopwatch.Elapsed;

            for (int k = 0; k < message.Length; k++)
            {
                if (message[k] != decrypted[k])
                {
                    Console.WriteLine(
                        $"ERROR: Data mismatch at index {k} for {cm}/{pm}. Expected: {message[k]}, Got: {decrypted[k]}");
                }
            }

            success = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ERROR during {cm}/{pm}: {ex.Message}");
            success = false;
        }

        if (success)
        {
            double encryptSpeed = encryptTime.TotalSeconds > 0
                ? fileSizeMB / encryptTime.TotalSeconds
                : double.PositiveInfinity;
            double decryptSpeed = decryptTime.TotalSeconds > 0
                ? fileSizeMB / decryptTime.TotalSeconds
                : double.PositiveInfinity;

            Console.WriteLine(
                $"  Encrypt Time: {encryptTime.TotalMilliseconds:F2} ms, Speed: {encryptSpeed:F2} MB/s");
            Console.WriteLine(
                $"  Decrypt Time: {decryptTime.TotalMilliseconds:F2} ms, Speed: {decryptSpeed:F2} MB/s");
        }

        Console.WriteLine("------------------------------------");

        Console.WriteLine($"--- Benchmark for Rijndael on file: {Path.GetFileName(filepath)} finished ---");
    }
}