
using System.Text.Json;

using System.IO.Ports;
using NModbus;
using NModbus.Serial;

namespace CoffeMachineController
{
    public class PicoController
    {
        /// <summary>
        /// List of all records necessary for proper functionality.
        /// </summary>
        List<string> recordIDs = ["Acknowledged","Attach_hot_water_spout","Coffee_temp_max","Coffee_temp_med","Coffee_temp_min",
            "Coffee_temp_selection","Default_screen","Default_screen_no_coffee","Drink_americano","Drink_hot_water",
            "Drink_milk_froth","Error_no_water","Icon_acknowledge","Icon_back","Icon_down","Icon_up",
            "Initialization_calibrating","Initialization_heating","Initialization_rinsing","Intensity_1","Intensity_2",
            "Intensity_3","Intensity_4","Intensity_5","Intensity_ground_coffee","Making_americano","Making_cappuccino",
            "Making_coffee","Making_coffee_double","Making_espresso","Making_espresso_double","Making_hot_water",
            "Making_latte","Menu_1_drinks","Menu_1_menu","Menu_2_coffee_temp","Menu_2_quick_clean","Milk_carafe_attach",
            "Milk_carafe_clean_request","Milk_carafe_dispensing_spout_closed","Milk_carafe_dispensing_spout_opened",
            "Milk_carafe_hot_steam","Milk_carafe_put_cup_under","Progress_bar","Start_calc_clean_message",

            "Error_coffee_ground_container_full","Error_coffee_ground_container_spill","Error_no_water",
            "Error_service_door_opened","Error_insert_water_dispenser","Error_no_coffee","Error_brew_group_failure"];

        //Serial line properties
        private const byte DEVICE_ADDRESS = 2;
        private const int BAUD_RATE = 115200;
        private const int PORT_TIMEOUT = 100; //In miliseconds
        private SerialPort port;
        
        //Modbus properties
        private ModbusFactory factory;
        private IModbusMaster conn;
        private PicoRegisters pico;

        //Low-level controll properties (communication, button push, ...)
        private const int READ_BUTTON_DELAY = 50; //In miliseconds, time elapsed between 2 repetitive when pushing button
        private const int READ_STATUS_DELAY = 500; //In miliseconds, time elapsed between 2 repetitive readings
        private const int ATTEMPTS = 20; //Max number of repetitive attempts
        private const long INITIALIZATION_TIME_MS = 25000;
        private const long PROGRESS_BAR_APPEAR_TIMEOUT_MS = 5000;
        private EventLogger myLogger;
        

        //High-level controll properties
        //private System.Timers.Timer phaseTimer = new();
        //private bool timerTicking = false;

        /// <summary>
        /// Most recent display record. This is updated each time the transaction occurs.
        /// </summary>
        private DisplayRecord currentScreen = new();




        //Database properties
        Random idGenerator = new Random();
        public Dictionary<string, DatabaseRecord> StandardRecords { get; private set; } = [];
        public Dictionary<string, DatabaseRecord> ErrorRecords { get; private set; } = [];

        //Methods for handling database records

        /// <summary>
        /// Imports display record from JSON file
        /// </summary>
        /// <param name="path">
        /// Path to file
        /// </param>
        /// <exception cref="ArgumentNullException">If database cannot be read or 
        /// does not contain necessary records.</exception>
        public void ImportDatabase(string path)
        {
            string rawString = File.ReadAllText(path);
            List<DatabaseRecord> Records = JsonSerializer.Deserialize<List<DatabaseRecord>>(rawString) ??
                throw new ArgumentNullException("Screen record database reading failed!");

            foreach (DatabaseRecord rec in Records)
            {
                if (rec.IsError == true)
                {
                    ErrorRecords.Add(rec.Id, rec);
                    recordIDs.Remove(rec.Id);
                }
                else
                {
                    StandardRecords.Add(rec.Id, rec);
                    recordIDs.Remove(rec.Id);
                }
            }
            if (recordIDs.Count > 0)
            {
                throw new ArgumentNullException($"Screen record database does not contain files: {recordIDs}!");
            }
        }

        /// <summary>
        /// Exports display record data to JSON file
        /// </summary>
        /// <param name="path">
        /// Path to file
        /// </param>
        public void ExportDatabase(string path)
        {
            List<DatabaseRecord> r = [.. StandardRecords.Values];
            r.Sort((el1, el2) =>
                {
                        return el1.Id.CompareTo(el2.Id);
                });

            List<DatabaseRecord> l = [.. r, .. ErrorRecords.Values];

            string rawString = JsonSerializer.Serialize(l, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, rawString);
        }

        /// <summary>
        /// Reads screen data from pico and creates new record if it's not in database.
        /// If the record is unique, adds it to the corresponding list.
        /// </summary>
        public void AddRecord()
        {
            ReadMachineStatus();
            ReadScreenOutput();

            if (pico.InputRegister.IsError() == true)
            {
                string newId = "";
                do
                {
                    newId = idGenerator.Next(ushort.MaxValue).ToString();
                } while (ErrorRecords.ContainsKey(newId));

                DatabaseRecord newRec = new(currentScreen, pico.InputRegister.IsError(), newId);
                foreach (DatabaseRecord rec in ErrorRecords.Values)
                {
                    if (rec.CompareScreen(newRec) == true)
                    {
                        return;
                    }
                }
                ErrorRecords.Add(newId, newRec);
            }

            else
            {
                string newId = "";
                do
                {
                    newId = idGenerator.Next(ushort.MaxValue).ToString();
                } while (StandardRecords.ContainsKey(newId));

                DatabaseRecord newRec = new(currentScreen, pico.InputRegister.IsError(), newId);
                foreach (DatabaseRecord rec in StandardRecords.Values)
                {
                    if (rec.CompareScreen(newRec) == true)
                    {
                        return;
                    }
                }
                StandardRecords.Add(newId, newRec);
            }
        }





        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="portName">Serial port address</param>
        /// <param name="myLogger">Logger object</param>
        public PicoController(string portName, EventLogger myLogger)
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
            conn.Transport.ReadTimeout = PORT_TIMEOUT;
            conn.Transport.WriteTimeout = PORT_TIMEOUT;

            pico = new();
            this.myLogger = myLogger;
        }




        
        //Private methods for controlling pico

        /// <summary>
        /// Reads input register from Pico
        /// </summary>
        /// <exception cref="ButtonPushedManuallyException">If the button was pushed by user.</exception>
        /// <exception cref="PicoErrorException">If Pico reports error.</exception>
        private void ReadMachineStatus()
        {
            pico.InputRegister.UpdateValue(conn.ReadInputRegisters(DEVICE_ADDRESS, PicoRegisters.INPUT_REGISTER_ADDRESS, 1)[0]);
            if (pico.InputRegister.IsActive() && pico.InputRegister.ButtonPushedMaually() == true)
            {
                throw new ButtonPushedManuallyException();
            }
            if (pico.InputRegister.IsActive() && pico.InputRegister.ButtonPushFailed() == true)
            {
                myLogger.LogEvent(new PicoErrorException(pico, "Button push failed!"));
            }
        }

        /// <summary>
        /// Reads Input and 5 SPI screen registers from Pico
        /// </summary>
        /// <exception cref="PicoErrorException">If SPI or REG reading fails.</exception>
        private void ReadScreenOutput()
        {
            ReadMachineStatus();
            if (pico.InputRegister.SpiRunning() == false && pico.InputRegister.IsRunning() == true) 
            {
                throw new PicoErrorException(pico, "Spi reading failed!");
            }
            if (pico.InputRegister.RegRunning() == false && pico.InputRegister.IsRunning() == true)
            {
                throw new PicoErrorException(pico, "Register reading failed!"); 
            }

            ushort[][] RxBuffer = new ushort[PicoRegisters.TRANSACTION_NUM][];
            for (int i = 0; i < PicoRegisters.TRANSACTION_NUM; ++i)
            {
                RxBuffer[i] = conn.ReadInputRegisters(DEVICE_ADDRESS, pico.REGISTER_GROUPS[i], PicoRegisters.REGISTER_NUM);
            }
            pico.SpiBuffer.ParseReceivedData(RxBuffer);
            currentScreen.UpdateRecord(pico.SpiBuffer.GetScreenData());
        }

        /// <summary>
        /// Sets function on Pico.
        /// </summary>
        /// <param name="function">Function to set</param>
        private void SetFunction(PicoRegisters.Functions function)
        {
            pico.CommandRegister.SetFunction(function);
            conn.WriteSingleRegister(DEVICE_ADDRESS, PicoRegisters.HOLDING_REGISTER_ADDRESS, pico.CommandRegister.Value);
            pico.CommandRegister.ResetFunction(function);
        }

        /// <summary>
        /// Resets function on Pico.
        /// </summary>
        /// <param name="function">Function to reset</param>
        private void ResetFunction(PicoRegisters.Functions function)
        {
            pico.CommandRegister.ResetFunction(function);
            conn.WriteSingleRegister(DEVICE_ADDRESS, PicoRegisters.HOLDING_REGISTER_ADDRESS, pico.CommandRegister.Value);
        }

        /// <summary>
        /// Sends "Push button" command to Pico
        /// </summary>
        /// <param name="button">Button to push</param>
        /// <exception cref="ButtonPushedManuallyException">If the button was pushed by user.</exception>
        /// <exception cref="PicoErrorException">If Pico reports error.</exception>
        private void SendPushCommand(PicoRegisters.Buttons button)
        {
            pico.CommandRegister.PushButton(button);
            conn.WriteSingleRegister(DEVICE_ADDRESS, PicoRegisters.HOLDING_REGISTER_ADDRESS, pico.CommandRegister.Value);
            pico.CommandRegister.ReleaseButton(button);

            for (int i = 0; i < ATTEMPTS; i++)
            {
                ReadMachineStatus();
                if (pico.InputRegister.IsPushed(button) == true)
                {
                    break;
                }
                Thread.Sleep(READ_BUTTON_DELAY);
            }

            for (int i = 0; i < ATTEMPTS; i++)
            {
                ReadMachineStatus();
                if (pico.InputRegister.IsPushed(button) == false)
                {
                    return;
                }
                Thread.Sleep(READ_BUTTON_DELAY);
            }

            throw new PicoErrorException(pico, "Button push operation timed out!");
        }





        //Public methods    
        /// <summary>
        /// Checks whether the machine is on. 
        /// WARNING:
        /// After the machine has been turned on, there is 
        /// a small delay until white or red screen appears, so be carefull 
        /// with this function.
        /// </summary>
        /// <returns>True if machine is on, false otherwise.</returns>
        /// <exception cref="ButtonPushedManuallyException">If the button was pushed by user.</exception>
        /// <exception cref="PicoErrorException">If Pico reports error.</exception>
        public bool IsOn()
        {
            ReadMachineStatus();
            return pico.InputRegister.IsOn();
        }
        /// <summary>
        /// Checks whether the machine is in standby mode.
        /// </summary>
        /// <returns>True if machine is in stand-by mode, false otherwise.</returns>
        /// <exception cref="ButtonPushedManuallyException">If the button was pushed by user.</exception>
        /// <exception cref="PicoErrorException">If Pico reports error.</exception>
        public bool IsStandby()
        {
            ReadMachineStatus();
            return pico.InputRegister.IsStandby();
        }
        /// <summary>
        /// Checks whether the screen is lighting (machine is active).
        /// </summary>
        /// <returns>True if screen is lighting, false otherwise.</returns>
        /// 
        /// <exception cref="ButtonPushedManuallyException">If the button was pushed by user.</exception>
        /// <exception cref="PicoErrorException">If Pico reports error.</exception>
        public bool IsActive()
        {
            ReadMachineStatus();
            return pico.InputRegister.IsActive();
        }
        /// <summary>
        /// Checks whether the machine is in error state 
        /// (red screen lighting.)
        /// </summary>
        /// <returns>True if error occured, false otherwise.</returns>
        /// <exception cref="ButtonPushedManuallyException">If the button was pushed by user.</exception>
        /// <exception cref="PicoErrorException">If Pico reports error.</exception>
        public bool IsError()
        {
            ReadMachineStatus();
            return pico.InputRegister.IsError();
        }
        /// <summary>
        /// Checks whether the machine is in normal state 
        /// (white screen lighting.)
        /// </summary>
        /// <returns>True if device is running, false otherwise.</returns>
        /// <exception cref="ButtonPushedManuallyException">If the button was pushed by user.</exception>
        /// <exception cref="PicoErrorException">If Pico reports error.</exception>
        public bool IsRunning()
        {
            ReadMachineStatus();
            return pico.InputRegister.IsRunning();
        }
        /// <summary>
        /// Prints the diagnostic data in text form.
        /// </summary>
        /// <exception cref="ButtonPushedManuallyException">If the button was pushed by user.</exception>
        /// <exception cref="PicoErrorException">If Pico reports error.</exception>
        public void PrintStatus()
        {
            ReadMachineStatus();
            pico.InputRegister.PrintStatus();
        }

        /// <exception cref="ButtonPushedManuallyException">If the button was pushed by user.</exception>
        /// <exception cref="PicoErrorException">If Pico reports error.</exception>
        public void PrintScreenData()
        {
            ReadScreenOutput();
            //TODO
        }

        /*public string GetCurrentScreenLabel()
        {
            ReadMachineStatus();
            return currentScreen.Id;
        }*/

        /// <summary>
        /// Reads screen data and checks them against the mask
        /// </summary>
        /// <param name="screenMask">Mask</param>
        /// <returns>True if current screen matches the mask, false otherwise.</returns>
        /// <exception cref="ButtonPushedManuallyException">If the button was pushed by user.</exception>
        /// <exception cref="PicoErrorException">If Pico reports error.</exception>
        public bool CheckScreen(DatabaseRecord screenMask)
        {
            ReadScreenOutput();
            return screenMask.Fits(currentScreen);
        }

        /// <summary>
        /// Reads screen data and checks them against the array of masks
        /// </summary>
        /// <param name="screenMask">Masks</param>
        /// <returns>True if current screen matches at least one mask, false otherwise.</returns>
        /// <exception cref="ButtonPushedManuallyException">If the button was pushed by user.</exception>
        /// <exception cref="PicoErrorException">If Pico reports error.</exception>
        public bool CheckScreen(DatabaseRecord[] screenMask)
        {
            ReadScreenOutput();
            foreach (DatabaseRecord mask in screenMask)
            {
                if (mask.Fits(currentScreen) == true)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Reads display, checks whether the screen has changed since last read
        /// </summary>
        /// <returns>True if screen changed, false otherwise.</returns>
        /// <exception cref="ButtonPushedManuallyException">If the button was pushed by user.</exception>
        /// <exception cref="PicoErrorException">If Pico reports error.</exception>
        public bool CheckScreenUpdated()
        {
            DisplayRecord oldScreen = currentScreen;
            ReadScreenOutput();
            return oldScreen.CompareScreen(currentScreen);
        }

        /// <summary>
        /// If initialization is required, waits until it is done.
        /// Initialization means calibrating, heating or rinsing.
        /// NOTE: This function must be called for each initialization phase
        /// separately (see the first part of function), otherwise the operation
        /// may time out.
        /// </summary>
        /// <returns>True if initialization was required, false otherwise.</returns>
        /// <exception cref="ButtonPushedManuallyException">If the button was pushed by user.</exception>
        /// <exception cref="PicoErrorException">If Pico reports error.</exception>
        /// <exception cref="UnexpectedStateException">If unexpected state occurs.</exception>
        public bool WaitForInitialization()
        {
            //Determines what kind of initialization is in progress
            ReadScreenOutput();
            DatabaseRecord initType;
            string initTypeStr = "";
            if (StandardRecords["Initialization_calibrating"].Fits(currentScreen) == true)
            {
                initType = StandardRecords["Initialization_calibrating"];
                initTypeStr = "calibrating";
            }
            else if (StandardRecords["Initialization_heating"].Fits(currentScreen) == true)
            {
                initType = StandardRecords["Initialization_heating"];
                initTypeStr = "heating";
            }
            else if (StandardRecords["Initialization_rinsing"].Fits(currentScreen) == true)
            {
                initType = StandardRecords["Initialization_rinsing"];
                initTypeStr = "rinsing";
            }
            else
            {
                return false;
            }

            //Waits until initialization is done
            for (long t = 0; t < INITIALIZATION_TIME_MS; t += READ_STATUS_DELAY)
            {
                if (CheckScreen(initType) != true)
                {
                    return t > 0;
                }
                if (pico.InputRegister.IsError() == true)
                {
                    Panic($"Error occured during {initTypeStr}!");
                    return false;
                }
                Thread.Sleep(READ_STATUS_DELAY);
            }
            Panic($"Operation {initTypeStr} timed out!");
            return false;
            }


        /// <summary>
        /// Waits until the fitting mask appears on screen. In case of heating, rinsing or calibrating,
        /// calls WaitUntilDone() which will wait until the initialization proces finishes.
        /// </summary>
        /// <param name="requiredMask">Fitting mask</param>
        /// <param name="timeout">Max time allowed for mask to appear (in ms, minimal value is 500)</param>
        /// <exception cref="ButtonPushedManuallyException">If the button was pushed by user.</exception>
        /// <exception cref="PicoErrorException">If Pico reports error.</exception>
        /// <exception cref="UnexpectedStateException">If unexpected state occurs.</exception>
        public void WaitForMask(DatabaseRecord requiredMask, long timeout)
        {
            for (long t = 0; t < timeout; t += READ_STATUS_DELAY)
            {
                if (CheckScreen(requiredMask) == true)
                {
                    return;
                }

                WaitForInitialization();

                if (pico.InputRegister.IsError() == true)
                {
                    Panic($"Error occured during waiting for mask {requiredMask.Id}!");
                }
                Thread.Sleep(READ_STATUS_DELAY);
            }
            Panic($"Waiting for mask {requiredMask.Id} timeout!");
        }

        /// <summary>
        /// Waits until the procedure begins (progress bar appears), then waits
        /// until it ends (bar disappears).
        /// 
        /// </summary>
        /// <param name="progressTimeout">Max time allowed for waiting phase</param>
        /// <exception cref="ButtonPushedManuallyException">If the button was pushed by user.</exception>
        /// <exception cref="PicoErrorException">If Pico reports error.</exception>
        /// <exception cref="UnexpectedStateException">If unexpected state occurs.</exception>
        public void WaitUntilDone(long progressTimeout)
        {
            try
            {
                WaitForMask(StandardRecords["Progress_bar"], PROGRESS_BAR_APPEAR_TIMEOUT_MS);
            }
            catch (UnexpectedStateException e)
            {
                myLogger.LogEvent(e, "WaitUntilDone");
                Panic("Waiting for progress bar timed out!");
            }

            for (long t = 0; t < progressTimeout; t += READ_STATUS_DELAY)
            {
                if (CheckScreen(StandardRecords["Progress_bar"]) != true)
                {
                    return;
                }
                if (pico.InputRegister.IsError() == true)
                {
                    Panic("Error occured during loading bar!");
                }
                Thread.Sleep(READ_STATUS_DELAY);
            }
            Panic("Loading bar loading timeout!");
        }

        /// <summary>
        /// Determines error state of machine. If no error occured,
        /// returns empty string
        /// </summary>
        /// <returns>Error state of machine</returns>
        /// <exception cref="ButtonPushedManuallyException">If the button was pushed by user.</exception>
        /// <exception cref="PicoErrorException">If Pico reports error.</exception>
        public string GetErrorState()
        {
            ReadScreenOutput();
            if (pico.InputRegister.IsError() == false)
            {
                return "";
            }

            foreach (DatabaseRecord e in ErrorRecords.Values)
            {
                if (e.Fits(currentScreen) == true)
                {
                    return e.Id;
                }
            }
            return "Unknown!";
        }

        /// <summary>
        /// Pushed the button on machine, verifies if curent screen allows button pushing.
        /// </summary>
        /// <param name="button">Regular (i.e. not Power) button to push</param>
        /// <param name="requiredMask">Some buttons may only be pushed in certain states. This mask
        /// ensures that machine is in one of those.</param>
        /// <param name="newMask">If provided, method also checks whether the screen fits the 
        /// new mask after button was pushed</param>
        /// <returns>False if condition has not been met, true otherwise</returns>
        /// <exception cref="ButtonPushedManuallyException">If the button was pushed by user.</exception>
        /// <exception cref="PicoErrorException">If Pico reports error.</exception>
        public bool PushButton(PicoRegisters.Buttons button, DatabaseRecord requiredMask)
        {
            ReadScreenOutput();
            if (button == PicoRegisters.Buttons.Power || requiredMask.Fits(currentScreen) == false)
            {
                return false;
            }
            SendPushCommand(button);

            return true;
        }


        /// <summary>
        /// Turn on the machine.
        /// </summary>
        /// <returns>True when machine is powered on, false if AC power is disconnected.</returns>
        public bool PowerOn()
        {
            ReadMachineStatus();
            if (pico.InputRegister.IsRunning() == true || pico.InputRegister.IsError() == true)
            {
                return true;
            }
            if (pico.InputRegister.IsStandby() == false)
            {
                return false;
            }

            SendPushCommand(PicoRegisters.Buttons.Power);
            return true;
        }

        /// <summary>
        /// Turn off the machine.
        /// </summary>
        public void PowerOff()
        {
            ReadMachineStatus();
            if (pico.InputRegister.IsOn() == false)
            {
                return;
            }

            SendPushCommand(PicoRegisters.Buttons.Power);
        }

        /// <summary>
        /// Used when upper control layer hits unknown state, throws exception
        /// </summary>
        /// <param name="msg">If provided, method also checks whether the screen fits the 
        /// <exception cref="UnexpectedStateException"></exception>
        public void Panic(string msg)
        {
            throw new UnexpectedStateException(pico, msg);
        }
        
    }


    /// <summary>
    /// The button on coffee machine control panel was pushed manually
    /// </summary>
    public class ButtonPushedManuallyException : Exception
    {
        public ButtonPushedManuallyException() { }
    }

    /// <summary>
    /// Pico reported error
    /// </summary>
    /// <param name="input">Input register of pico, in time of failure.</param>
    /// <param name="message">Error message</param>
    public class PicoErrorException(PicoRegisters pico, string message) : Exception(message)
    {
        public PicoRegisters.Inputs inputs { get; private set; } = pico.InputRegister;
        public PicoRegisters.Commands commands { get; private set; } = pico.CommandRegister;
    }

    /// <summary>
    /// Unexpected state occured
    /// </summary>
    /// <param name="pico">Input register of pico, in time of failure.</param>
    /// <param name="message">Error message</param>
    public class UnexpectedStateException(PicoRegisters pico, string message) : Exception(message)
    {
        public PicoRegisters.Inputs inputs = pico.InputRegister;
        public PicoRegisters.Commands commands = pico.CommandRegister;
    }
}