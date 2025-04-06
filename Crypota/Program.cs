using System.Diagnostics;
using System.Numerics;
using Crypota.Classes;
using Crypota.PrimalityTests;
using static Crypota.Utilities;

namespace Crypota;


class Program
{
    static async Task Main(string[] args)
    {
        string n = "561";
        
        var test = new FermatTest();
        double prob = 0.9;

        var result = test.PrimaryTest(BigInteger.Parse(n), prob);
        Console.WriteLine(result);
    }
}