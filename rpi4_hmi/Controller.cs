
using CoffeMachineController;
using CupFetcherController;

namespace rpi4_backend{

    public class Controller
    {
        public enum GlobalPhases
        {
            waiting,
            cupFetching,
            makingCoffee,
            cupExporting,
            error
        }

        public enum Commands : ushort
        {
            empty,
            espresso,
            coffee,
            americano,
            setAroma,
            setTemperature,
            calibrate,
            startMachine
        }

        /*public class Order
        {
            private static ConcurrentQueue<Order> HMIqueue = new();
            public static Order[] CupPositions {get; private set;} = [new(Beverages.empty), new(Beverages.empty), new(Beverages.empty), new(Beverages.empty)];
            public const byte MAX_ORDER_NUM = 4;
            private static Stopwatch positionTimer = new();
            private const long POSITION_DELAY_MS = 1000;
            private static byte tempLastCupPositions = 0;
            private static byte lastCupPositions = 0;
            private static byte cupNum = 0;

            public Beverages orderType {get; private set;}
            //public string orderer {get; private set;} = "";

            private Order(Beverages orderType){
                this.orderType = orderType;
                //this.orderer = orderer;
            }

            public static bool AddOrder(Beverages orderType){
                if (cupNum == MAX_ORDER_NUM){
                    return false;
                }
                else {
                    HMIqueue.Enqueue(new(orderType));
                    cupNum++;
                    return true;
                }
            }

            public static Order? GetFirstOrder(){
                Order? firstOrder;
                if (HMIqueue.TryPeek(out firstOrder)){
                    return firstOrder;
                }
                return null;
            }

            public static void FinishFirstOrder(){ //UNSAFE?
                Order? lastOrder;
                if (HMIqueue.TryDequeue(out lastOrder)){
                    for (int i = 0; i < MAX_ORDER_NUM; ++i){

                        if (CupPositions[i].orderType == Beverages.empty){
                            //Shifts all cups by one position
                            for (int j = i; j > 0; --j){
                                CupPositions[j] = CupPositions[j - 1];
                            }
                            CupPositions[0] = lastOrder;
                        }
                        
                    }
                }
            }

            //Adjusts positions according to data from sensors, returns false in case of error
            public static bool AdjustPositions(byte cupPositions){
                if (cupPositions == lastCupPositions){
                    return true;
                } 
                else if (cupPositions != tempLastCupPositions){
                    positionTimer.Restart();
                    tempLastCupPositions = cupPositions;
                    return true;
                }
                else if (cupPositions == tempLastCupPositions && positionTimer.ElapsedMilliseconds > POSITION_DELAY_MS){
                    if (cupPositions < lastCupPositions){
                        for (int i = 0; i < MAX_ORDER_NUM; ++i){
                            if ((cupPositions & (1 << i)) != (lastCupPositions & (1 << i))){
                                CupPositions[i] = new(Beverages.empty);
                            }
                        }
                    }
                    positionTimer.Reset();
                    return true;
                }
                return false;
            }
        }*/


        public MachineController Machine;
        public NHduinoController CupFetcher;

        //To distinct between command phases
        //private bool phaseRunning = false;
        //To prevent HMI from launching command while another one is running
        public bool CoffeeMachineErrror = false;
        public bool CupFetcherError = false;


        private Thread? commandHandlerThread = null;
        private bool endThread = false;
        //Coffee machine ready
        //private bool coffeeInitialized = false;
        //Assembly ready
        //private bool assemblyCalibrated = false;
        //Used for first time initialization
        //private bool firstResetDone = false;

        public Commands CurrentCommand{ get; private set; } = Commands.empty;

        public Controller()
        {
            Machine = new("/dev/ttyAMA4", "/home/rpi4/programs/autonomous-coffe-machine/Firmware/rpi4_coffee_machine_controller/Database.json", "ErrorLog.txt");
            CupFetcher = new("/dev/ttyAMA3");
            Machine.StartControlLoop();
        }

        /*public bool Initialize(){
            commandRunning = true;
            if (initialized == true){
                return true;
            }

            phase = GlobalPhases.preparing;
            Machine.PowerOn();
            cupFetcher.Calibrate();

            do {
                Thread.Sleep(500);
                if (Machine.IsError() || cupFetcher.IsError()){
                    return false;
                }
            } while (Machine.CheckIfDone() == false || cupFetcher.CheckIfDone() == false);
            phase = GlobalPhases.waiting;
            initialized = true;
            
            return true;
        }*/

        //Executs command in a loop
        /*public void MakeCommand(){
            if (currentCommand == Commands.empty){
                return;
            }



            //Runs only once at the beginning
            //During initialization, errors on Machine are ignored
            else if(currentCommand == Commands.startMachine){
                if (phaseRunning == false && coffeeInitialized == false){
                    
                    if (Machine.State == MachineController.States.Idle){
                        coffeeInitialized = true;
                        currentCommand = Commands.reset;
                    }
                    else {
                        Machine.PowerOn();
                        commandRunning = true;
                        phaseRunning = true;
                    }
                }

                else if(coffeeInitialized == false && phaseRunning == true && Machine.CheckIfDone() == true){
                    phaseRunning = false;
                    
                    coffeeInitialized = true;
                    currentCommand = Commands.reset;
                }
            }



            //Reset and recalibration of assembly
            else if (currentCommand == Commands.reset){
                if (firstResetDone == false){
                    currentCommand = Commands.startMachine;
                    firstResetDone = true;
                    Globals.UpdateState("Starting");
                }
                else if (phaseRunning == false){
                    assemblyCalibrated = false;
                    phaseRunning = true;
                    cupFetcher.Stop();
                    cupFetcher.Calibrate();
                    Globals.UpdateState("Calibrating");
                }
                else if (phaseRunning == true){
                    if (cupFetcher.IsError() == true){
                        phaseRunning = false;
                        phase = GlobalPhases.error;
                        currentCommand = Commands.empty;
                        Globals.UpdateState("ESP Error");
                    }
                    else if (cupFetcher.CheckIfDone() == true){
                        assemblyCalibrated = true;
                        phaseRunning = false;
                        phase = GlobalPhases.waiting;
                        currentCommand = Commands.empty;
                        Globals.UpdateState("Ready");
                    }
                }
            }



            //Normal execution of command
            else if (cupFetcher.IsError() == true){
                phaseRunning = false;
                
                phase = GlobalPhases.error;
                currentCommand = Commands.empty;
                Globals.UpdateState("ESP Error");
            }

            else if (coffeeInitialized == true && assemblyCalibrated == true && 
                currentCommand != Commands.empty && currentCommand != Commands.reset){
                if (phaseRunning == true && cupFetcher.CheckIfDone() == true && Machine.CheckIfDone() == true){
                    phaseRunning = false;
                }
                
                if (phaseRunning == false){
                    if (phase == GlobalPhases.waiting && Machine.IsError() == false){
                        phase = GlobalPhases.cupFetching;
                        cupFetcher.FetchCup(); //Check for error
                        phaseRunning = true;
                        commandRunning = true;
                        Globals.UpdateState("Fetching cup");
                    }

                    else if (phase == GlobalPhases.cupFetching){
                        if (Machine.IsError() == true){
                            phaseRunning = false;
                            
                            phase = GlobalPhases.error;
                            currentCommand = Commands.empty;
                            Globals.UpdateState("Machine Error");
                        }
                        else {
                            phase = GlobalPhases.makingCoffee;
                            switch(currentCommand){
                                case Commands.espresso:
                                    Machine.MakeEspresso();
                                    break;
                                case Commands.coffee:
                                    Machine.MakeCoffee();
                                    break;
                                case Commands.setAroma:
                                    Machine.d();
                                    break;
                                case Commands.setTemperature:
                                    Machine.MakeLatte();
                                    break;
                            }
                        }
                        phaseRunning = true;
                        Globals.UpdateState("Making coffee");
                    }

                    else if (phase == GlobalPhases.makingCoffee){
                        phase = GlobalPhases.cupExporting;
                        cupFetcher.ExportCoffee();
                        phaseRunning = true;
                        Globals.UpdateState("Exporting Coffee");
                    }

                    else if (phase == GlobalPhases.cupExporting){
                        phase = GlobalPhases.waiting;
                        
                        currentCommand = Commands.empty;
                        Globals.UpdateState("Done");
                    }
                }
            }
  
        }*/

        private void HandleCommand()
        {
            switch (CurrentCommand)
            {
                case Commands.startMachine:
                    if (Machine.SetRequest(MachineController.Requests.PowerOn) == false)
                    {
                        CoffeeMachineErrror = true;
                        
                        CurrentCommand = Commands.empty;
                        Globals.UpdateState("Machine switching start failed");
                        return;
                    }

                    //Waiting for machine to power on
                    Thread.Sleep(500);
                    while (Machine.State == MachineController.States.Switching)
                    {
                        Thread.Sleep(500); //Wait for machine to power on
                    }
                    
                    Thread.Sleep(2000);

                    //Power on operation finished
                    if (Machine.State != MachineController.States.Idle)
                    {
                        CoffeeMachineErrror = true;

                        CurrentCommand = Commands.empty;
                        Globals.UpdateState("Machine switching failed");
                        return;
                    }
                    
                    CurrentCommand = Commands.empty;
                    Globals.UpdateState($"Machine switching done");
                    break;



                case Commands.calibrate:
                    CupFetcher.Calibrate();
                    Thread.Sleep(500);
                    while (CupFetcher.CheckIfDone() == false)
                    {
                        Thread.Sleep(500); //Wait for cup fetcher to calibrate
                        if (CupFetcher.diagnostics.IsError() == true)
                        {
                            CupFetcherError = true;

                            CurrentCommand = Commands.empty;
                            Globals.UpdateState("Fetcher calibration failed");
                            return;
                        }
                    }
                    //Thread.Sleep(10000);
                    
                    CurrentCommand = Commands.empty;
                    Globals.UpdateState("Fetcher calibration done");
                    break;



                case Commands.setTemperature:
                    if (Machine.SetRequest(MachineController.Requests.SetTemperature, 1) == false)
                    {
                        CoffeeMachineErrror = true;
                        
                        CurrentCommand = Commands.empty;
                        Globals.UpdateState("Machine setting start failed");
                        return;
                    }

                    //Waiting for machine to set temperature
                    Thread.Sleep(500);
                    while (Machine.State == MachineController.States.MachineSetting)
                    {
                        Thread.Sleep(500); //Wait for machine to finish
                    }

                    //Temperature setting operation finished
                    
                    CurrentCommand = Commands.empty;
                    Globals.UpdateState("Temperature set done");
                    break;



                case Commands.setAroma:
                    if (Machine.SetRequest(MachineController.Requests.SetAroma, 1) == false)
                    {
                        CoffeeMachineErrror = true;
                        
                        CurrentCommand = Commands.empty;
                        Globals.UpdateState("Machine setting start failed");
                        return;
                    }

                    //Waiting for machine to set aroma
                    Thread.Sleep(500);
                    while (Machine.State == MachineController.States.MachineSetting)
                    {
                        Thread.Sleep(500); //Wait for machine to power on
                    }

                    //Aroma setting operation finished
                    
                    CurrentCommand = Commands.empty;
                    Globals.UpdateState("Aroma set done");
                    break;



                case Commands.espresso:
                case Commands.coffee:
                case Commands.americano:
                    // First phase start
                    if (CupFetcher.FetchCup() == false)
                    {
                        CupFetcherError = true;
                        
                        CurrentCommand = Commands.empty;
                        Globals.UpdateState("Fetching start failed");
                        return;
                    }
                    Thread.Sleep(500);

                    //Waiting for first phase to finish
                    while (CupFetcher.CheckIfDone() == false)
                    {
                        Thread.Sleep(500); //Wait for cup fetcher to calibrate
                        if (CupFetcher.diagnostics.IsError() == true)
                        {
                            CupFetcherError = true;

                            CurrentCommand = Commands.empty;
                            Globals.UpdateState("Fetching failed");
                            return;
                        }
                    }
                    Globals.UpdateState("Fetching done");


                    // Second phase start
                    MachineController.Requests request = CurrentCommand switch
                    {
                        Commands.espresso => MachineController.Requests.MakeEspresso,
                        Commands.coffee => MachineController.Requests.MakeCoffee,
                        Commands.americano => MachineController.Requests.MakeAmericano,
                        _ => MachineController.Requests.None
                    };
                    if (Machine.SetRequest(request) == false)
                    {
                        CoffeeMachineErrror = true;
                        
                        CurrentCommand = Commands.empty;
                        Globals.UpdateState("Drink making failed");
                        return;
                    }

                    //Waiting for second phase to finish
                    Thread.Sleep(500);
                    while (Machine.State == MachineController.States.DrinkMaking)
                    {
                        Thread.Sleep(500); //Wait for machine to power on
                    }
                    if (Machine.State != MachineController.States.Idle)
                    {
                        CoffeeMachineErrror = true;
                        
                        CurrentCommand = Commands.empty;
                        Globals.UpdateState("Drink making failed");
                        return;
                    }
                    Globals.UpdateState($"Drink done");


                    // Third phase start
                    if (CupFetcher.ExportCoffee() == false)
                    {
                        CupFetcherError = true;
                        
                        CurrentCommand = Commands.empty;
                        Globals.UpdateState("Exporting start failed");
                        return;
                    }
                    Thread.Sleep(500);

                    //Waiting for third phase to finish
                    while (CupFetcher.CheckIfDone() == false)
                    {
                        Thread.Sleep(500); //Wait for cup fetcher to calibrate
                        if (CupFetcher.diagnostics.IsError() == true)
                        {
                            CupFetcherError = true;

                            CurrentCommand = Commands.empty;
                            Globals.UpdateState("Exporting failed");
                            return;
                        }
                    }
                    Globals.UpdateState("Coffee export done");
                    
                    CurrentCommand = Commands.empty;
                    break;



                default:
                    return;
            }
        }




        public bool SetCommand(Commands command)
        {
            if (CurrentCommand != Commands.empty)
            {

                return false; //Command already running
            }

            switch (command)
            {
                case Commands.setAroma:
                case Commands.setTemperature:
                    if (Machine.State != MachineController.States.Idle)
                    {
                        return false; //Machine not ready
                    }
                    break;

                case Commands.espresso:
                case Commands.coffee:
                case Commands.americano:
                
                    if (Machine.State != MachineController.States.Idle ||
                        CupFetcher.diagnostics.IsError() == true || CupFetcher.diagnostics.IsHome() == false)
                    {
                        return false;
                    }
                    break;
            }

            CurrentCommand = command;
            return true; //Command accepted
        }

        private void MainLoop(){
            while (endThread == false)
            {
                if (CurrentCommand != Commands.empty)
                {
                    HandleCommand();
                }
                Thread.Sleep(100); // Prevent busy waiting
            }
        }

        public Thread StartCommandHandler()
        {
            endThread = false;
            commandHandlerThread = new Thread(MainLoop);
            commandHandlerThread.IsBackground = true;
            commandHandlerThread.Start();
            return commandHandlerThread;
        }
        
        public void StopCommandHandler()
        {
            endThread = true;
            if (commandHandlerThread != null && commandHandlerThread.IsAlive)
            {
                commandHandlerThread.Join();
            }
            commandHandlerThread = null;
            
            CurrentCommand = Commands.empty;
        }
    }
}
