
using CoffeMachineController;

/// <summary>
/// Main method for testing the coffee machine controller using command line.
/// </summary>
class Program
{
    static void Main(string[] args)
    {
        
        Console.WriteLine("Hello, World!");

        MachineController d1 = new("/dev/ttyAMA4", "Database.json", "Events.txt");
        //Pinout: 21 = Rx, 24 = Tx, 22 = reset, 23 = irq

        File.WriteAllText("Events.txt", ""); //Clear the log file
        File.AppendAllLines("Events.txt", ["d"]);


        d1.StartControlLoop();
        string msg;
        string? command;
        bool retVal = false;
        int param = 0;

        int a = 3;

        while (a == 3)
        {
            Thread.Sleep(500);
            while ((msg = d1.MyLogger.ReadNextMessage()) != "")
            {
                Console.WriteLine(msg);
            }

            //Why blocking?
            if (Console.KeyAvailable == true)
            {
                command = Console.ReadLine() ?? "";
                if (command.Length < 1)
                {
                    continue;
                }

                switch (command[0])
                {

                    case 'd':
                        retVal = d1.SetRequest(MachineController.Requests.GetState);
                        break;
                    case '1':
                        retVal = d1.SetRequest(MachineController.Requests.PowerOn);
                        break;
                    case '0':
                        retVal = d1.SetRequest(MachineController.Requests.PowerOff);
                        break;

                    case 'e':
                        retVal = d1.SetRequest(MachineController.Requests.MakeEspresso);
                        break;
                    case 'c':
                        retVal = d1.SetRequest(MachineController.Requests.MakeCoffee);
                        break;
                    case 'a':
                        retVal = d1.SetRequest(MachineController.Requests.MakeAmericano);
                        break;
                    case 'h':
                        retVal = d1.SetRequest(MachineController.Requests.MakeHotWater);
                        break;
                    case 't':
                        param = command[1] - 48;
                        retVal = d1.SetRequest(MachineController.Requests.SetTemperature, param);
                        break;
                    case 'i':
                        param = command[1] - 48;
                        retVal = d1.SetRequest(MachineController.Requests.SetAroma, param);
                        break;
                    case 's':
                        retVal = true;
                        break;

                }
                Console.WriteLine(" Return value: " + retVal + "; State: " + d1.State.ToString() + "; Error: " + d1.ErrorState);
            }

        }

        d1.StopControlLoop();
        a = 3;

    }
}



