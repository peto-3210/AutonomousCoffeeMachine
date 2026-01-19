
using System.IO.Ports;
using NModbus;
using NModbus.Serial;

namespace CupFetcherController{

    public class NHduinoController{

        /// <summary>
        /// All procedures supported by cup fetcher
        /// </summary>
        public enum Procedures : byte
        {
            home = 0,
            fetchCup = 1,
            exportCoffee = 2,
            calibrate = 3,
            stop = 4
        };

        /// <summary>
        /// List of phases from all procedures
        /// </summary>
        public enum Phases : byte
        {
            homing = 0,

            waiting = 1,
            cupPick = 2,
            solenoidUnlock = 3,
            cupUnload = 4,
            solenoidLock = 5,
            liftDescend = 6,
            cupToMachine = 7,

            coffeeToLift = 8,
            liftAscend = 9,
            manipulatorDodge = 10,
            coffeeToHeliport = 11,
            coffeeToPlatform = 12,
            platformFall = 13,
            coffeeToConveyor = 14,
            platformRise = 15,

            calibrating = 16
        };


        /*public enum BatchControlPhases: ushort{
            idle, 
            running, 
            pausing, //Skipped
            paused, 
            holding, 
            held, 
            restarting, //Skipped
            stopped, 
            stopping, 
            aborted, 
            aborting, //Skipped
            complete
        };

        public enum BatchControlCommands: ushort{
            startCommand, //Initiated by user
            pauseCommand, //Initiated by user, stops immediately
            resumeCommand, //Initiated by user, resumes
            holdCommand, //Initiated by user and machine, finishes current step and stops
            restartCommand, //Initiated by user, starts next step
            stopCommand, //Initiated by user, finishes current step and stops procedure
            abortCommand, //Initiated by user and machine, stops procedure immediately
            resetCommand //Initiated by user, goes to idle state
        };*/




        /// <summary>
        /// MODBUS address of device
        /// </summary>
        private const byte DEVICE_ADDRESS = 1;

        /// <summary>
        /// Address of MODBUS holding (command) register
        /// </summary>
        private const ushort COMMAND_REGISTER = 0;

        /// <summary>
        /// Address of first MODBUS input register
        /// </summary>
        private const ushort INPUT_REGISTER1 = 0;

        /// <summary>
        /// Number of input registers. First one is for
        /// input data from the sensors, second is for
        /// errors and warnings.
        /// </summary>
        private const ushort NUMBER_OF_INPUT_REGS = 2;


        /*private enum Registers: ushort{
            inputRegister, stateMachineRegister, logsRegister, diagnosticDataRegister,
            xAxisPosition1, xAxisPosition2, yAxisPosition1, yAxisPosition2, zAxisPosition1, zAxisPosition2
        };*/





        /// <summary>
        /// Structure for handling command register
        /// </summary>
        public class CommandRegister(){
            /// <summary>
            /// Raw command data
            /// </summary>
            public ushort Command { get; private set; } = 0;

            /*public void SetBatchCommand(BatchControlCommands command){
                Command |= (ushort)command;
            }*/
            /// <summary>
            /// Sets the procedure for cup fetcher
            /// </summary>
            /// <param name="procedure">Procedure number</param>
            public void SetProcedure(Procedures procedure)
            {
                Command = (ushort)procedure;
            }
        }





        /// <summary>
        /// Represents inputs from all sensors
        /// </summary>
        public class Inputs()
        {
            public enum Sensors : ushort
            {
                xAxisRight,
                xAxisCupPresent,
                yAxisBottom,
                yAxisCupPresent,
                xAxisMachine,
                yAxisTop,                
                switch6,
                switch7,
                cupSensor1,
                cupSensor2,
                cupSensor3,
                cupSensor4,
                switch12,
                switch13,
                switch14,
                switch15,
            };
            /// <summary>
            /// Raw value
            /// </summary>
            public ushort Value { private get; set; }

            /// <summary>
            /// Returns the input from specified sensor (end stops)
            /// </summary>
            /// <param name="sensor">Sensor number</param>
            /// <returns></returns>
            public bool GetSensorInput(Sensors sensor)
            {
                return (Value & (1 << (ushort)sensor)) > 0;
            }

            /// <summary>
            /// Returns the input from cup sensors
            /// </summary>
            /// <returns>Cup sensors input in binary form</returns>
            public byte CupSensorValues()
            {
                return (byte)((Value & 0x0f00) >> 8);
            }

            /// <summary>
            /// Prints outputs from all sensors
            /// </summary>
            public void PrintStatus()
            {
                Console.WriteLine($"xAxisRight: {GetSensorInput(Sensors.xAxisRight)}");
                Console.WriteLine($"xAxisCupPresent: {GetSensorInput(Sensors.xAxisCupPresent)}");
                Console.WriteLine($"yAxisBottom: {GetSensorInput(Sensors.yAxisBottom)}");
                Console.WriteLine($"yAxisCupPresent: {GetSensorInput(Sensors.yAxisCupPresent)}");

                Console.WriteLine($"cupSensor1: {GetSensorInput(Sensors.cupSensor1)}");
                Console.WriteLine($"cupSensor2: {GetSensorInput(Sensors.cupSensor2)}");
                Console.WriteLine($"cupSensor3: {GetSensorInput(Sensors.cupSensor3)}");
                Console.WriteLine($"cupSensor4: {GetSensorInput(Sensors.cupSensor4)}");
            }
        }

        /// <summary>
        /// Represents all errors, warnings and diagnostic outputs
        /// Also stores current phase of procedure
        /// </summary>
        public class DiagData
        {
            /// <summary>
            /// List of messages from assembly
            /// </summary>
            private enum Messages : byte
            {
                phaseRunning,
                calibrated,
                home,
                firstPhaseDone,
                errCup,
                errStepperLost,
                errFullConveyor,
                errInvalidCommand
            }

            /// <summary>
            /// Current phase of procedure
            /// </summary>
            public Phases Phase { get; private set; }
            /// <summary>
            /// Raw message data
            /// </summary>
            private byte Message;

            /// <summary>
            /// Checks whether the procedure is currently running
            /// </summary>
            /// <returns></returns>
            public bool IsRunning()
            {
                return (Message & 0b00000001) > 0;
            }

            /// <summary>
            /// Checks whether the device is calibrated
            /// </summary>
            /// <returns></returns>
            public bool IsCalibrated()
            {
                return (Message & 0b00000010) > 0;
            }

            /// <summary>
            /// Checks whether the device is in home position
            /// </summary>
            /// <returns></returns>
            public bool IsHome()
            {
                return (Message & 0b00000100) > 0;
            }

            /// <summary>
            /// Checks whether the first phase (fetch cup) is done
            /// </summary>
            /// <returns></returns>
            public bool FirstPhaseDone()
            {
                return (Message & 0b00001000) > 0;
            }

            /// <summary>
            /// Checks whether there is any error
            /// </summary>
            /// <returns></returns>
            public bool IsError()
            {
                return (Message & 0b11110000) > 0;
            }

            /// <summary>
            /// Checks if stepper got lost (did not hit end stop)
            /// </summary>
            /// <returns></returns>
            public bool StepperLost()
            {
                return (Message & 0b00010000) > 0;
            }

            /// <summary>
            /// Checks whether there is no cup when there should be one
            /// </summary>
            /// <returns></returns>
            public bool CupMissing()
            {
                return (Message & 0b00100000) > 0;
            }

            /// <summary>
            /// Checks whether there is a cup present where there should be none
            /// </summary>
            /// <returns></returns>
            public bool CupPresent()
            {
                return (Message & 0b01000000) > 0;
            }

            /// <summary>
            /// Checks whether the conveyor is full
            /// </summary>
            /// <returns></returns>
            public bool FullConveyor()
            {
                return (Message & 0b10000000) > 0;
            }

            /// <summary>
            /// Prints all diagnostic data
            /// </summary>
            public void PrintStatus()
            {
                Console.WriteLine($"Running: {IsRunning()}");
                Console.WriteLine($"Calibrated: {IsCalibrated()}");
                Console.WriteLine($"Home: {IsHome()}");
                Console.WriteLine($"First Phase Done: {FirstPhaseDone()}");
                Console.WriteLine($"Cup Missing: {CupMissing()}");
                Console.WriteLine($"Cup Present: {CupPresent()}");
                Console.WriteLine($"Full Conveyor: {FullConveyor()}");
                Console.WriteLine($"Stepper Lost: {StepperLost()}");

                Console.WriteLine($"Error: {IsError()}");
                Console.WriteLine($"Phase: {Phase}");
            }

            /// <summary>
            /// Parses raw data from register into properties
            /// </summary>
            /// <param name="rawData"></param>
            public void ParseData(ushort rawData)
            {
                Phase = (Phases)(rawData & 0xff);
                Message = (byte)((rawData & 0xff00) >> 8);
            }
        }





        //Reading batch state and process state of assembly
        /*private struct StateMachine(ushort rawData){
            public ushort RawData {private get; set;} = rawData;
            public BatchControlPhases GetBatchPhase(){
                return (BatchControlPhases)(RawData & 0xff);
            }

            public Phases GetProcessPhase(){
                return (Phases)((RawData & 0xff00) >> 8);
            }
        }*/





        //Errors and warnings from assembly
        /*private struct Logs(ushort rawData){
            public enum Errors: ushort {
                errUnexpectedEnd, //End sensor activated too soon
                errDeviceLost, //End sensor did not activate at the position
                errNoCup, //No cup detected
                errCupPresent, //Cup detected when there should be none
                errWrongProc, //Request for procedure without fulfilling prereqs
            }

            public enum Warnings: ushort {
                warnEmptyTower, //No cup for another coffee
                warnFullConveyor, //No space for another coffee
                //warnWrongBatch, //Requested batch command cannot be fulfilled
            }
        
            public ushort RawData {set; private get;} = rawData;

            public bool IsError(Errors number){
                return (RawData & (1 << (ushort)number)) > 0;
            }

            public bool IsWarning(Warnings number){
                return (RawData & (1 << ((ushort)number + 8))) > 0;
            }

            public bool IsError(){
                return (RawData & 0xff) > 0;
            }

            public bool IsWarning(){
                return (RawData & RawData & 0xff00) > 0;
            }
        }*/







        //Global properties
        /// <summary>
        /// Baud rate for serial communication
        /// </summary>
        private const int BAUD_RATE = 115200;

        /// <summary>
        /// Serial port for communication
        /// </summary>
        private SerialPort port;

        /// <summary>
        /// Factory for creating MODBUS connection
        /// </summary>
        private ModbusFactory factory;

        /// <summary>
        /// MODBUS connection
        /// </summary>
        private IModbusMaster conn;


        /// <summary>
        /// Command register
        /// </summary>
        public CommandRegister commands = new();

        /// <summary>
        /// Inputs from all sensors
        /// </summary>
        private Inputs inputs = new();

        /// <summary>
        /// Diagnostics from assembly
        /// </summary>
        public DiagData diagnostics = new();

        /*private StateMachine states = new(0);
        private Logs logging = new(0);*/




        //Constructor
        /// <summary>
        /// Creates a new controller for cup fetcher assembly
        /// </summary>
        /// <param name="portName"></param>
        public NHduinoController(string portName)
        {
            port = new()
            {
                PortName = portName,
                BaudRate = BAUD_RATE,
                DataBits = 8,
                Parity = Parity.Even,
                StopBits = StopBits.One
            };
            port.Open();

            factory = new();
            conn = factory.CreateRtuMaster(port);
        }





        //Private methods
        /// <summary>
        /// Reads data from input registers and updates properties
        /// </summary>
        private void GetData()
        {
            ushort[] rxData = conn.ReadInputRegisters(DEVICE_ADDRESS, INPUT_REGISTER1, NUMBER_OF_INPUT_REGS);
            inputs.Value = rxData[0];
            diagnostics.ParseData(rxData[1]);
        }

        /// <summary>
        /// Sends command to command register
        /// </summary>
        private void SendCommand()
        {
            conn.WriteSingleRegister(DEVICE_ADDRESS, COMMAND_REGISTER, commands.Command);
        }





        //Public methods
        /// <summary>
        /// Checks whether the cup fetcher is done with current procedure
        /// </summary>
        /// <returns></returns>
        public bool CheckIfDone(){
            GetData();
            return diagnostics.IsRunning() == false;
        }

        /// <summary>
        /// Brings all steppers to home position
        /// </summary>
        /// <returns></returns>
        public bool Home()
        {
            GetData();
            if (diagnostics.IsError() || diagnostics.IsRunning())
            {
                return false;
            }
            else if (diagnostics.IsCalibrated() == false)
            {
                return false;
            }

            commands.SetProcedure(Procedures.home);
            //commands.SetBatchCommand(BatchControlCommands.startCommand);

            SendCommand();
            return true;
        }

        /// <summary>
        /// Fetches a cup from the tower
        /// </summary>
        /// <returns>Whether the operation started successfully</returns>
        public bool FetchCup(){
            GetData();
            if (diagnostics.IsError() || diagnostics.IsRunning()){
                return false;
            }
            else if(diagnostics.IsHome() == false){
                return false;
            }

            commands.SetProcedure(Procedures.fetchCup);
            //commands.SetBatchCommand(BatchControlCommands.startCommand);
            SendCommand();
            return true;
        }

        /// <summary>
        /// Exports coffee to conveyor
        /// </summary>
        /// <returns>Whether the operation started successfully</returns>
        public bool ExportCoffee()
        {
            GetData();
            if (diagnostics.IsError() || diagnostics.IsRunning())
            {
                return false;
            }
            if (diagnostics.FirstPhaseDone() == false)
            {
                return false;
            }

            commands.SetProcedure(Procedures.exportCoffee);
            //commands.SetBatchCommand(BatchControlCommands.startCommand);
            SendCommand();
            return true;
        }

        /// <summary>
        /// Calibrates all steppers
        /// </summary>
        public void Calibrate()
        {
            GetData();
            /*if (diagnostics.IsError() || diagnostics.IsRunning()){
                return false;
            }*/

            commands.SetProcedure(Procedures.calibrate);
            //commands.SetBatchCommand(BatchControlCommands.startCommand);
            SendCommand();
        }

        /// <summary>
        /// Returns cup sensor data in bits
        /// </summary>
        /// <returns></returns>
        public byte CupOccupancy()
        {
            return inputs.CupSensorValues();
        }

        /// <summary>
        /// Prints status of all inputs and diagnostics 
        /// </summary>
        public void PrintStatus()
        {
            GetData();
            inputs.PrintStatus();
            diagnostics.PrintStatus();
        }

        //Pauses currently running process
        /*public bool PauseProcess(){
            GetData();
            if (logging.IsError() || states.GetBatchPhase() != BatchControlPhases.running){
                return false;
            }

            commands.SetBatchCommand(BatchControlCommands.pauseCommand);
            SendCommand();
            return true;
        }

        //Resumes process
        public bool ResumeProcess(){
            GetData();
            if (logging.IsError() || states.GetBatchPhase() != BatchControlPhases.running){
                return false;
            }

            commands.SetBatchCommand(BatchControlCommands.resumeCommand);
            SendCommand();
            return true;
        }

        //Restarts held process
        public bool RestartProcess(){
            GetData();
            if (logging.IsError() || states.GetBatchPhase() != BatchControlPhases.running){
                return false;
            }

            commands.SetBatchCommand(BatchControlCommands.resumeCommand);
            SendCommand();
            return true;
        }

        //Aborts currently running process
        public bool StopProcess(){
            GetData();
            if (logging.IsError() || states.GetBatchPhase() != BatchControlPhases.running){
                return false;
            }

            commands.SetBatchCommand(BatchControlCommands.abortCommand);
            SendCommand();
            return true;
        }*/

        /// <summary>
        /// Immediately stops all operations and resets device
        /// </summary>
        public void Stop()
        {
            commands.SetProcedure(Procedures.stop);
            SendCommand();
        }

    }
}
