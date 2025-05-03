using Crypota.Symmetric.Deal;

namespace UnitTests.Tests.Benchmarks;

[TestClass]
public sealed class DesBenchmarkTests : Benchmark
{
    private const string Filepath1 = "C:\\Users\\yashelter\\Desktop\\Crypota\\UnitTests\\Input\\message.txt";
    private const string Filepath2 = "C:\\Users\\yashelter\\Desktop\\Crypota\\UnitTests\\Input\\img.jpeg";
    private const string Filepath3 = "C:\\Users\\yashelter\\Desktop\\Crypota\\UnitTests\\Input\\Twofish.pdf";

    [DataTestMethod]
    [DataRow(Filepath1)]
    [DataRow(Filepath2)]
    [DataRow(Filepath3)]
    public void BenchmarkAllModesDesWithFile(string filepath)
    {
        var implementation = new Crypota.Symmetric.Des.Des();
        byte[] key = new byte[implementation.KeySize];
        byte[] iv = new byte[implementation.BlockSize];
        string cipherName = "DES";

        RunBenchmark(filepath, implementation, key, iv, cipherName);
    }

    [DataTestMethod]
    [DataRow(Filepath1)]
    public void BenchmarkAllModesDeal128WithFile(string filepath)
    {
        var implementation = new Deal128();
        byte[] key = new byte[implementation.KeySize]; 
        byte[] iv = new byte[implementation.BlockSize];
        string cipherName = "Deal128";

        RunBenchmark(filepath, implementation, key, iv, cipherName);
    }
}