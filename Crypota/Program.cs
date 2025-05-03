using System.Diagnostics;
using System.Numerics;
using Crypota.PrimalityTests;
using Crypota.RSA.Examples;
using Crypota.RSA.HackingTheGate;
using static Crypota.CryptoMath.CryptoMath;
using static Crypota.RSA.HackingTheGate.ChainedFraction;
using static Crypota.FileUtility;

namespace Crypota;


class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Starting Rijndael implementation benchmark...");

        var implementation = new Crypota.Symmetric.Rijndael.Rijndael()
        {
            BlockSizeBits = 128,
            KeySizeBits = 128,
            IrreduciblePolynom = 0x1B
        };

        byte[] key = new byte[implementation.KeySize];
        byte[] dataBlockBytes = new byte[implementation.BlockSize];
        byte[] originalDataBlockBytes = new byte[implementation.BlockSize];
        
        Array.Fill<byte>(key, 0x01);
        Array.Fill<byte>(dataBlockBytes, 0x02);

        dataBlockBytes.CopyTo(originalDataBlockBytes, 0);

        implementation.Key = key;

        Span<byte> dataBlockSpan = dataBlockBytes;

        Console.WriteLine("Performing initial encrypt/decrypt check...");
        try
        {
            implementation.EncryptBlock(dataBlockSpan);
            implementation.DecryptBlock(dataBlockSpan);

            bool ok = true;
            for (int i = 0; i < implementation.BlockSize; i++)
            {
                if (dataBlockBytes[i] != originalDataBlockBytes[i])
                {
                    ok = false;
                    break;
                }
            }
            Console.WriteLine($"Initial check result: {(ok ? "OK" : "FAILED!")}");
            if (!ok)
            {
                Console.WriteLine("Benchmark aborted due to failed initial check.");
                return;
            }
            originalDataBlockBytes.CopyTo(dataBlockBytes, 0);

        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during initial check: {ex.Message}");
            Console.WriteLine("Benchmark aborted.");
            return;
        }

        
        int iterations = 1_000_00;
        long totalBytesProcessed = (long)iterations * implementation.BlockSize;
        double totalMegabytesProcessed = (double)totalBytesProcessed / (1024 * 1024);

        Console.WriteLine($"Configuration: BlockSize={implementation.BlockSize * 8} bits, KeySize={implementation.KeySize * 8} bits");
        Console.WriteLine($"Benchmarking with {iterations:N0} iterations...");
        Console.WriteLine($"Total data to process: {totalMegabytesProcessed:F2} MB");
        Console.WriteLine("Warming up...");

        for (int i = 0; i < 1000; i++)
        {
             implementation.EncryptBlock(dataBlockSpan);
        }
        originalDataBlockBytes.CopyTo(dataBlockBytes, 0);

        // 5. Замер шифрования
        Console.WriteLine("Benchmarking EncryptBlock...");
        Stopwatch stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            implementation.EncryptBlock(dataBlockSpan);
        }
        stopwatch.Stop();
        TimeSpan encryptElapsed = stopwatch.Elapsed;
        double encryptSpeed = encryptElapsed.TotalSeconds > 0 ? totalMegabytesProcessed / encryptElapsed.TotalSeconds : 0;

        Console.WriteLine($"EncryptBlock Time: {encryptElapsed.TotalMilliseconds:F2} ms");
        Console.WriteLine($"EncryptBlock Speed: {encryptSpeed:F2} MB/s");


        Console.WriteLine("Benchmarking DecryptBlock...");
        stopwatch.Restart();
        for (int i = 0; i < iterations; i++)
        {
            implementation.DecryptBlock(dataBlockSpan);
        }
        stopwatch.Stop();
        TimeSpan decryptElapsed = stopwatch.Elapsed;
        double decryptSpeed = decryptElapsed.TotalSeconds > 0 ? totalMegabytesProcessed / decryptElapsed.TotalSeconds : 0;

        Console.WriteLine($"DecryptBlock Time: {decryptElapsed.TotalMilliseconds:F2} ms");
        Console.WriteLine($"DecryptBlock Speed: {decryptSpeed:F2} MB/s");

        Console.WriteLine("\nBenchmark finished.");
    }
}