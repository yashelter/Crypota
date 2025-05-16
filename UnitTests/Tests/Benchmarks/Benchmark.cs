using System.Diagnostics;
using System.Runtime.CompilerServices;
using Crypota.Interfaces;
using Crypota.Symmetric;
using static Crypota.FileUtility;

namespace UnitTests.Tests.Benchmarks;

[TestClass]
public abstract class Benchmark
{
    public TestContext TestContext { get; set; }

    protected void RunBenchmark(string filepath, ISymmetricCipher implementation, byte[] key, byte[] iv, string cipherName)
    {
        byte[] message = GetFileInBytes(filepath);
        long fileSize = message.Length;
        double fileSizeMB = (double)fileSize / (1024 * 1024); 

        TestContext.WriteLine($"--- Starting Benchmark for {cipherName} on file: {Path.GetFileName(filepath)} ({fileSizeMB:F2} MB) ---");

        Stopwatch stopwatch = new Stopwatch();

        // Итерируем по всем режимам дополнения
        foreach (PaddingMode pm in Enum.GetValues(typeof(PaddingMode)))
        {
            // Итерируем по всем режимам шифрования
            foreach (CipherMode cm in Enum.GetValues(typeof(CipherMode)))
            {
                TestContext.WriteLine($"Testing Mode: {cm}, Padding: {pm}");
                
                SymmetricCipherWrapper cipherWrapper = new SymmetricCipherWrapper(key, cm, pm, implementation, iv)
                {
                    Key = key
                };

                byte[] encrypted = null;
                byte[] decrypted = null;
                TimeSpan encryptTime = TimeSpan.Zero;
                TimeSpan decryptTime = TimeSpan.Zero;
                bool success = false;

                try
                {
                    stopwatch.Restart();

                    encrypted = cipherWrapper.EncryptMessageAsync(message).GetAwaiter().GetResult();
                    stopwatch.Stop();
                    encryptTime = stopwatch.Elapsed;

                    stopwatch.Restart();
                    decrypted = cipherWrapper.DecryptMessageAsync(encrypted).GetAwaiter().GetResult();
                    stopwatch.Stop();
                    decryptTime = stopwatch.Elapsed;

                    Assert.AreEqual(message.Length, decrypted.Length, $"Length mismatch for {cm}/{pm}");
                    for (int k = 0; k < message.Length; k++)
                    {
                        if (message[k] != decrypted[k])
                        {
                            TestContext.WriteLine($"ERROR: Data mismatch at index {k} for {cm}/{pm}. Expected: {message[k]}, Got: {decrypted[k]}");
                            Assert.AreEqual(message[k], decrypted[k], $"Data mismatch at index {k} for {cm}/{pm}");
                        }
                    }
                    success = true;
                }
                catch (Exception ex)
                {
                    TestContext.WriteLine($"  ERROR during {cm}/{pm}: {ex.Message}");
                    success = false;
                }

                if (success)
                {
                    double encryptSpeed = encryptTime.TotalSeconds > 0 ? fileSizeMB / encryptTime.TotalSeconds : double.PositiveInfinity;
                    double decryptSpeed = decryptTime.TotalSeconds > 0 ? fileSizeMB / decryptTime.TotalSeconds : double.PositiveInfinity;

                    TestContext.WriteLine($"  Encrypt Time: {encryptTime.TotalMilliseconds:F2} ms, Speed: {encryptSpeed:F2} MB/s");
                    TestContext.WriteLine($"  Decrypt Time: {decryptTime.TotalMilliseconds:F2} ms, Speed: {decryptSpeed:F2} MB/s");
                }
                TestContext.WriteLine("------------------------------------");
            }
        }
        TestContext.WriteLine($"--- Benchmark for {cipherName} on file: {Path.GetFileName(filepath)} finished ---");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]

    protected void RunBenchmarkMaxSpeed(string filepath, ISymmetricCipher implementation, byte[] key, byte[] iv,
        string cipherName)
    {
        byte[] message = GetFileInBytes(filepath);
        long fileSize = message.Length;
        double fileSizeMB = (double)fileSize / (1024 * 1024);

        TestContext.WriteLine(
            $"--- Starting Benchmark for {cipherName} on file: {Path.GetFileName(filepath)} ({fileSizeMB:F2} MB) ---");

        Stopwatch stopwatch = new Stopwatch();

        var cm = CipherMode.RD;
        var pm = PaddingMode.PKCS7;
        
        TestContext.WriteLine($"Testing Mode: {cm}, Padding: {pm}");

        SymmetricCipherWrapper cipherWrapper = new SymmetricCipherWrapper(key, cm, pm, implementation, iv, 
            new SymmetricCipherWrapper.RandomDeltaParameters()
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

            encrypted = cipherWrapper.EncryptMessageAsync(message).GetAwaiter().GetResult();
            stopwatch.Stop();
            encryptTime = stopwatch.Elapsed;

            stopwatch.Restart();
            decrypted = cipherWrapper.DecryptMessageAsync(encrypted).GetAwaiter().GetResult();
            stopwatch.Stop();
            decryptTime = stopwatch.Elapsed;

            Assert.AreEqual(message.Length, decrypted.Length, $"Length mismatch for {cm}/{pm}");
            for (int k = 0; k < message.Length; k++)
            {
                if (message[k] != decrypted[k])
                {
                    TestContext.WriteLine(
                        $"ERROR: Data mismatch at index {k} for {cm}/{pm}. Expected: {message[k]}, Got: {decrypted[k]}");
                    Assert.AreEqual(message[k], decrypted[k], $"Data mismatch at index {k} for {cm}/{pm}");
                }
            }

            success = true;
        }
        catch (Exception ex)
        {
            TestContext.WriteLine($"  ERROR during {cm}/{pm}: {ex.Message}");
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

            TestContext.WriteLine(
                $"  Encrypt Time: {encryptTime.TotalMilliseconds:F2} ms, Speed: {encryptSpeed:F2} MB/s");
            TestContext.WriteLine(
                $"  Decrypt Time: {decryptTime.TotalMilliseconds:F2} ms, Speed: {decryptSpeed:F2} MB/s");
        }

        TestContext.WriteLine("------------------------------------");

        TestContext.WriteLine($"--- Benchmark for {cipherName} on file: {Path.GetFileName(filepath)} finished ---");
    }
}