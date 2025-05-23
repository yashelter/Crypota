﻿using System.Runtime.CompilerServices;

namespace UnitTests.Tests.Benchmarks;

[TestClass]
public class RijndaelBenchmarkTests: Benchmark
{
    private const string Filepath1 = "C:\\Users\\yashelter\\Desktop\\Crypota\\UnitTests\\Input\\message.txt";
    private const string Filepath2 = "C:\\Users\\yashelter\\Desktop\\Crypota\\UnitTests\\Input\\img.jpeg";
    private const string Filepath3 = "C:\\Users\\yashelter\\Desktop\\Crypota\\UnitTests\\Input\\Twofish.pdf";
    private const string Filepath4 = "C:\\Users\\yashelter\\Desktop\\Crypota\\UnitTests\\Input\\1.gif";


    [DataTestMethod]
    [DataRow(Filepath1)]
    [DataRow(Filepath2)]
    [DataRow(Filepath3)]
    public void BenchmarkAllModesRijndaelWithFile(string filepath)
    {
        var implementation = new Crypota.Symmetric.Rijndael.Rijndael()
        {
            BlockSizeBits = 128,
            KeySizeBits = 128,
            IrreduciblePolynom = 0x1B
        };
        
        byte[] key = new byte[implementation.KeySize];
        byte[] iv = new byte[implementation.BlockSize];
        string cipherName = "Rijndael";

        RunBenchmark(filepath, implementation, key, iv, cipherName);
    }
    
    [DataTestMethod]
    [DataRow(Filepath1)]
    [DataRow(Filepath2)]
    [DataRow(Filepath3)]
    [DataRow(Filepath4)]

    public void SpeedBenchmarkRijndaelWithFile(string filepath)
    {
        var implementation = new Crypota.Symmetric.Rijndael.Rijndael()
        {
            BlockSizeBits = 128,
            KeySizeBits = 128,
            IrreduciblePolynom = 0x1B
        };
        
        byte[] key = new byte[implementation.KeySize];
        byte[] iv = new byte[implementation.BlockSize];
        string cipherName = "Rijndael";

        RunBenchmarkMaxSpeed(filepath, implementation, key, iv, cipherName);
    }

}