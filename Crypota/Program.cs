using System.Diagnostics;
using Crypota.Classes;
using static Crypota.Tests.DesTests;

namespace Crypota;


class Program
{
    static async Task Main(string[] args)
    {
        
        string[] paths = ["C:\\Users\\yashelter\\Desktop\\Crypota\\Crypota\\Crypota\\Tests\\Input\\message.txt",
            // "C:\\Users\\yashelter\\Desktop\\Crypota\\Crypota\\Crypota\\Tests\\Input\\goddes.png",
           // "C:\\Users\\yashelter\\Desktop\\Crypota\\Crypota\\Crypota\\Tests\\Input\\happy.mp4",
            //  "C:\\Users\\yashelter\\Desktop\\Crypota\\Crypota\\Crypota\\Tests\\Input\\suzuha.gif",
        ];

        
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();
        
        padding = 0;
        mode = 0;

        for (int i = 0; i < 7; i++)
        {
            padding = 0;
            for (int j = 0; j < 4; j++)
            {
                foreach (string path in paths)
                {
                    Console.WriteLine($"Testing path with mode: {mode}, padding {padding} :" + path);
                    try
                    {
                       // await TestDes(path);
                        await TestDeal(path);
                    } 
                    catch (Exception e) { Console.WriteLine(e.Message); }
                    
                    
                }
                
                padding += 1;
            }

            mode += 1;
        }
        
        
        stopwatch.Stop();
        //Console.WriteLine($"Time: {stopwatch.ElapsedMilliseconds} ms");

    }
}