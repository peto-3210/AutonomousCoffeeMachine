using System.Security.Cryptography;
using CupFetcherController;


class Program
{
    static void Main(string[] args)
    {
        NHduinoController c1 = new("/dev/ttyAMA3");
        Console.WriteLine("Hello, Cup Fetcher Controller!");

        int a = 3;
        string command = "";
        bool result = false; 

        while (a == 3)
        {
            Thread.Sleep(500);

            //Why blocking?
            if (Console.KeyAvailable == true)
            {
                result = true;
                command = Console.ReadLine() ?? "";
                if (command.Length < 1)
                {
                    continue;
                }

                if (command[0] == 'p')
                {
                    c1.PrintStatus();
                    continue;
                }

                if (c1.CheckIfDone() == false)
                {
                    continue;
                }

                switch (command[0])
                {
                    case '0':
                        c1.Calibrate();
                        break;

                    case '1':
                        result = c1.Home();
                        break;

                    case '2':
                        result = c1.FetchCup();
                        break;

                    case '3':
                        result = c1.ExportCoffee();
                        break;
                }
                Console.WriteLine($"Result: {result}");
            }

        }
    }
}