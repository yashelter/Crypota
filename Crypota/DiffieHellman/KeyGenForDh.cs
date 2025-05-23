using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Crypota.PrimalityTests;
using Crypota.RSA;
namespace Crypota.DiffieHellman;

public class KeyGenForDh
{
    private readonly RandomNumberGenerator _rng = RandomNumberGenerator.Create();
    private readonly IPrimaryTest _primaryTest;
    private readonly double _probability;
    private readonly int _bitLength;   
    
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KeyGenForDh(RsaService.PrimaryTestOption primaryTestOption, double probability, int bitLength)
    {
            
        if (probability < 0.5 || probability >= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(probability),"Probability is not reachable");
        }

        if (bitLength < 3)
        {
            throw new ArgumentOutOfRangeException(nameof(bitLength),"BitLength is too small");
        }
            
        _probability = probability;
        _bitLength = bitLength;
            
        switch (primaryTestOption)
        {
            case RsaService.PrimaryTestOption.FermatTest:
                _primaryTest = new FermatTest();
                break;
            case RsaService.PrimaryTestOption.SolovayStrassenTest:
                _primaryTest = new SolovayStrassenTest();
                break;
            case RsaService.PrimaryTestOption.MillerRabinTest:
                _primaryTest = new MillerRabinTest();
                break;
            default:
                throw new ArgumentException("Unknown primary test");
        }
    }
    
    
    public BigInteger GeneratePrimaryNumberDh()
    {
        BigInteger candidate;
        Probability state;
        int remBits = _bitLength & 7;
        int size = (_bitLength + 7) >> 3;

        var buffer = ArrayPool<byte>.Shared.Rent(size);
        try
        {
            do
            {
                _rng.GetBytes(buffer, 0, size);
                if (remBits == 0) remBits = 8;
                buffer[size - 1] |= (byte)(1 << (remBits - 1));

                candidate = new BigInteger(buffer, isUnsigned: true, isBigEndian: false);
                state = _primaryTest.PrimaryTest(candidate, _probability);
                if (state == Probability.PossiblePrimal)
                {
                    candidate = candidate * 2 + 1;
                    state = _primaryTest.PrimaryTest(candidate, _probability);
                }
            }
            while (state == Probability.Composite);
            return candidate;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
    
    private byte[] GetRandomBytes(int bitCount)
    {
        int byteCount = (bitCount + 7) >> 3;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(byteCount);
        try
        {
            _rng.GetBytes(buffer.AsSpan(0, byteCount));

            int excessBits = (byteCount << 3) - bitCount;
            if (excessBits > 0)
            {
                byte mask = (byte)(0xFF >> excessBits);
                buffer[byteCount - 1] &= mask;
            }

            var result = new byte[byteCount];
            Array.Copy(buffer, result, byteCount);
            return result;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, false);
        }
    }

    public BigInteger GenerateCandidate()
    {
        var bytes = GetRandomBytes(_bitLength);
        int topBitIndex = (_bitLength - 1) & 7;
        bytes[^1] |= (byte)(1 << topBitIndex);
        bytes[0] |= 1;
        return new BigInteger(bytes, isUnsigned: true, isBigEndian: false);
    }

    
    public async Task<BigInteger> GeneratePrimeAsync(CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<BigInteger>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        int workerCount = Environment.ProcessorCount;
        Task[] workers = Enumerable.Range(0, workerCount).Select(_ => Task.Run(() =>
        {
            while (!cts.IsCancellationRequested)
            {
                var candidate = GenerateCandidate();
                if (_primaryTest.PrimaryTest(candidate, _probability) != Probability.Composite)
                {
                    if (tcs.TrySetResult(candidate))
                        cts.Cancel();
                    break;
                }
            }
        }, cts.Token)).ToArray();

        var result = await tcs.Task.ConfigureAwait(false);
        try { await Task.WhenAll(workers).ConfigureAwait(false); } catch { /* ignore */ }
        return result;
    }
}