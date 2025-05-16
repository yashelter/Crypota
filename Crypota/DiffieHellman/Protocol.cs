using System.Numerics;
using System.Runtime.CompilerServices;
using Crypota.RSA;
using static Crypota.CryptoMath.CryptoMath;

namespace Crypota.DiffieHellman;

public static class Protocol
{
    private static double probability = 0.99;
    private const int Bitlen = 4096;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static (BigInteger p, BigInteger g) GeneratePairParallel(int bitlen = Bitlen)
    {
        var cts = new CancellationTokenSource();
        var token = cts.Token;
        
        Task<(BigInteger p, BigInteger g)> MakeCandidateAsync() => Task.Run(() =>
        {
            var gen = new KeyGenForDh(
                RsaService.PrimaryTestOption.MillerRabinTest,
                probability, bitlen);

            while (!token.IsCancellationRequested)
            {
                var pCand = gen.GeneratePrimaryNumber();
                var phi = pCand - 1;
                var q = phi / 2;

                for (BigInteger gCand = 2; gCand < phi; gCand++)
                {
                    if (BinaryPowerByMod(gCand, phi / q, pCand) != 1 &&
                        BinaryPowerByMod(gCand, phi / 2, pCand) != 1)
                    {
                        return (pCand, gCand);
                    }
                }
            }
            throw new OperationCanceledException(token);
        }, token);

        int degree = Environment.ProcessorCount;
        var tasks = Enumerable.Range(0, degree)
            .Select(_ => MakeCandidateAsync())
            .ToList();

        var first = Task.WhenAny(tasks).Result;
        var result = first.Result;

        cts.Cancel();
        return result;
    }
    
    
    
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static (BigInteger p, BigInteger q) GeneratePair(int bitlen = Bitlen)
    {
        KeyGenForDh genForDh = new KeyGenForDh(RsaService.PrimaryTestOption.MillerRabinTest, probability, bitlen);
        BigInteger g = 1, p;
        bool great = false;
        int i = 0;
        do
        {
            Console.Clear();

            p = genForDh.GeneratePrimaryNumber();
            var phi = p - 1;
            Console.WriteLine(p);

            var q = (phi / 2);
            for (BigInteger probG = 2; probG < phi; probG++)
            {
                if (BinaryPowerByMod(probG, (phi / q), p) != 1 &&
                    BinaryPowerByMod(probG, (phi / 2), p) != 1)
                {
                    g = probG;
                    great = true;
                    break;
                }
            }
        } while (!great);

        Console.Clear();
        return (g, p);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static BigInteger GeneratePart(BigInteger g, BigInteger secretPrimal, BigInteger p)
    {
        return BinaryPowerByMod(g, secretPrimal, p);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static BigInteger GenerateSecret(BigInteger a, BigInteger b, BigInteger p)
    {
        return BinaryPowerByMod(a, b, p);
    }
}