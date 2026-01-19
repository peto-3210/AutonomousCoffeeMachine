
using System.Device.Gpio;
using System.Reflection;

namespace CoffeMachineController
{
    public class MachineController
    {
        /// <summary>
        /// Possible states of machine
        /// </summary>
        public enum States : ushort
        {
            Off, //Machine is turned off
            AC_disconnected, //Machine has no power
            Running, //Coffee machine is turned on and not in error state
            Idle, //Default screen
            Switching, //Powering on or off
            MachineSetting, //Applying commands according to HMI
            DrinkMaking, //Preparing the drink
            Resetting, //Resetting after exception (or new data)
            Error, //Error state of machine
            FatalError //Machine control cannot be restored
        }
        /// <summary>
        /// Requests that machine can fulfil
        /// </summary>
        public enum Requests : ushort
        {
            None,
            GetState,
            PowerOn,
            PowerOff,
            MakeEspresso,
            MakeCoffee,
            MakeAmericano,
            MakeHotWater,
            SetAroma,
            SetTemperature
        }

        /// <summary>
        /// Pin used to reset Pico
        /// </summary>
        private int RESET_PIN = 25;

        /// <summary>
        /// Pin used by pico to report new data
        /// </summary>
        private int IRQ_PIN = 11;

        /// <summary>
        /// Max time machine can take to get to home screen
        /// </summary>
        private const int RETURN_TO_HOME_MS = 10000;

        /// <summary>
        /// Max amount of time the switching process 
        /// can last (excluding initialization)
        /// </summary>
        private const int MAX_SWITCHING_TIME = 5000;
        //private const int TURN_ON_TIME_MS = 60000;
        //private const int TURN_OFF_TIME_MS = 20000;

        /// <summary>
        /// Max time machine can take to make espresso
        /// </summary>
        private const int ESPRESSO_TIME_MS = 60000;

        /// <summary>
        /// Max time machine can take to make coffee
        /// </summary>
        private const int COFFEE_TIME_MS = 65000;
        //private const int CAPUCCINO_TIME_MS = 90000;
        //private const int LATTE_TIME_MS = 90000;

        /// <summary>
        /// Max time machine can take to make americano
        /// </summary>
        private const int AMERICANO_TIME_MS = 100000;

        /// <summary>
        /// Max time machine can take to make hot water.
        /// Time was calibrated for filling the currently used cups.
        /// </summary>
        private const int HOT_WATER_TIME_MS = 50000;

        /// <summary>
        /// Maximum time the program will wait before firing fatal error
        /// </summary>
        private const int MAX_WAITING_TIME_MS = 120000; 
        //private const int FATAL_ERROR_RECOVERY_TIMEOUT = 300000; //How often will program try to recover from fatal error

        /// <summary>
        /// How long it takes for screen to display new frame
        /// </summary>
        private const int SCREEN_REFRESH_TIMEOUT_MS = 1500;

        /// <summary>
        /// For periodic screen readig
        /// </summary>
        private const int READ_SCREEN_TIME_MS = 500; 

        /// <summary>
        /// How often main loop runs
        /// </summary>
        private const int MAIN_LOOP_REFRESH_TIME_MS = 500;

        /// <summary>
        /// Time until temporary frame disappears. Temporary frames 
        /// are frames which will disappear after some amount of time.
        /// </summary>
        private const int TEMPORARY_SCREEN_TIMEOUT_MS = 5000;

        /// <summary>
        /// Minimal time required after manual button push detection
        /// to initiate the resetting process.
        /// </summary>
        private const int MIN_IDLE_TIME = 5000;

        /// <summary>
        /// Timeout between 2 consecutive button push attempts
        /// </summary>
        private const int BUTTON_PUSH_TIMEOUT_MS = 500; 

        /// <summary>
        /// Max number of attempts to push the button
        /// </summary>
        private const int PUSH_ATTEMPT_NUM = 3;

        /// <summary>
        /// Max number button push to set desired aroma
        /// </summary>
        private const int SET_AROMA_ATTEMPTS_NUM = 5;

        /// <summary>
        /// Max number of button push to set desired temperature
        /// </summary>

        private const int SET_TEMPERATURE_ATTEMPTS_NUM = 2;

        /// <summary>
        /// Object for RPI Pico control
        /// </summary>
        private PicoController pico;

        /// <summary>
        /// Object for RPI4 pins control
        /// </summary>
        private GpioController pins;

        /// <summary>
        /// State of coffee machine
        /// </summary>
        public States State { get; private set; } = States.AC_disconnected;

        /// <summary>
        /// Error message displayed on screen
        /// </summary>
        public string ErrorState { get; private set; } = "";

        /// <summary>
        /// Machine ran out of coffee (it need not be considered an error state)
        /// </summary>
        public bool OutOfCoffee { get; private set; } = false;

        /// <summary>
        /// Event logger
        /// </summary>
        public EventLogger MyLogger { get; private set; }


        //public bool fatalError{ get; private set; } = false;
        //public int fatalErrorRecoveryAttemptTime{ get; private set; } = 0; //No recovery from fatal error

        /// <summary>
        /// Whether the control loop is busy (executing command).
        /// </summary>
        private bool Busy = true;

        /// <summary>
        /// Whether the communication with pico timed out
        /// </summary>
        private bool connectionTimedOut = false;

        /// <summary>
        /// If the carafe is attached
        /// </summary>
        private bool carafeAttached = false;

        /// <summary>
        /// Request (command) from user
        /// </summary>
        private Requests userRequest;

        /// <summary>
        /// Additional parameter (aroma, temp.) of request
        /// </summary>
        private int additionalParameter;

        /// <summary>
        /// Used to end main control thread
        /// </summary>
        private bool endThread = false;

        /// <summary>
        /// Main control thread
        /// </summary>
        private Thread? ControlThread = null;


        /// <summary>
        /// Creates controller object
        /// <param name="portName">
        /// Name of serial port connected to pico
        /// </param>
        /// <param name="database">
        /// Path to screen record database
        /// </param>
        /// <param name="errorOutput">
        /// Path to error log file
        /// </param>
        /// </summary>
        public MachineController(string portName, string database, string errorOutput)
        {
            MyLogger = new(errorOutput);
            pico = new(portName, MyLogger);
            pico.ImportDatabase(database);

            pins = new();
            pins.OpenPin(RESET_PIN, PinMode.Output);
            pins.Write(RESET_PIN, PinValue.Low); //Set pin to low by default
            pins.OpenPin(IRQ_PIN, PinMode.InputPullDown);
        }





        //Private methods for navigation

        /// <summary>
        /// For navigation, requires "Default_screen" mask
        /// </summary>
        /// <exception cref="ButtonPushedManuallyException">If the button was pushed by user.</exception>
        /// <exception cref="PicoErrorException">If Pico reports error.</exception>
        /// <exception cref="UnexpectedStateException">If unexpected state occurs.</exception>
        private void PushEspresso()
        {
            for (int attempts = 0; attempts < PUSH_ATTEMPT_NUM; ++attempts)
            {
                if (pico.PushButton(PicoRegisters.Buttons.Espresso, pico.StandardRecords["Default_screen"]) == false)
                {
                    pico.Panic("Espresso push failed!");
                }

                Thread.Sleep(BUTTON_PUSH_TIMEOUT_MS);
                if (pico.CheckScreenUpdated() == true)
                {
                    return;
                }

            }
            pico.Panic("Espresso push timeout!");
        }

        /// <summary>
        /// For navigation, requires "Default_screen" mask
        /// </summary>
        /// <exception cref="ButtonPushedManuallyException">If the button was pushed by user.</exception>
        /// <exception cref="PicoErrorException">If Pico reports error.</exception>
        /// <exception cref="UnexpectedStateException">If unexpected state occurs.</exception>
        private void PushCoffee()
        {
            for (int attempts = 0; attempts < PUSH_ATTEMPT_NUM; ++attempts)
            {
                if (pico.PushButton(PicoRegisters.Buttons.Coffee, pico.StandardRecords["Default_screen"]) == false)
                {
                    pico.Panic("Coffee push failed!");
                }

                Thread.Sleep(BUTTON_PUSH_TIMEOUT_MS);
                if (pico.CheckScreenUpdated() == true)
                {
                    return;
                }

            }
            pico.Panic("Coffee push timeout!");
        }

        /// <summary>
        /// For navigation, requires "Default_screen" mask
        /// </summary>
        /// <exception cref="ButtonPushedManuallyException">If the button was pushed by user.</exception>
        /// <exception cref="PicoErrorException">If Pico reports error.</exception>
        /// <exception cref="UnexpectedStateException">If unexpected state occurs.</exception>
        private void PushAroma()
        {
            for (int attempts = 0; attempts < PUSH_ATTEMPT_NUM; ++attempts)
            {
                if (pico.PushButton(PicoRegisters.Buttons.Aroma, pico.StandardRecords["Default_screen"]) == false)
                {
                    pico.Panic("Aroma push failed!");
                }

                Thread.Sleep(BUTTON_PUSH_TIMEOUT_MS);
                if (pico.CheckScreenUpdated() == true)
                {
                    return;
                }

            }
            pico.Panic("Aroma push timeout!");
        }

        /// <summary>
        /// For navigation, requires "Default_screen" mask
        /// </summary>
        /// <exception cref="ButtonPushedManuallyException">If the button was pushed by user.</exception>
        /// <exception cref="PicoErrorException">If Pico reports error.</exception>
        /// <exception cref="UnexpectedStateException">If unexpected state occurs.</exception>
        private void PushCappuccino()
        {
            for (int attempts = 0; attempts < PUSH_ATTEMPT_NUM; ++attempts)
            {
                if (pico.PushButton(PicoRegisters.Buttons.Capuccino, pico.StandardRecords["Default_screen"]) == false)
                {
                    pico.Panic("Cappuccino push failed!");
                }

                Thread.Sleep(BUTTON_PUSH_TIMEOUT_MS);
                if (pico.CheckScreenUpdated() == true)
                {
                    return;
                }

            }
            pico.Panic("Cappuccino push timeout!");
        }

        /// <summary>
        /// For navigation, requires "Default_screen" mask
        /// </summary>
        /// <exception cref="ButtonPushedManuallyException">If the button was pushed by user.</exception>
        /// <exception cref="PicoErrorException">If Pico reports error.</exception>
        /// <exception cref="UnexpectedStateException">If unexpected state occurs.</exception>
        private void PushLatte()
        {
            for (int attempts = 0; attempts < PUSH_ATTEMPT_NUM; ++attempts)
            {
                if (pico.PushButton(PicoRegisters.Buttons.Latte, pico.StandardRecords["Default_screen"]) == false)
                {
                    pico.Panic("Latte push failed!");
                }

                Thread.Sleep(BUTTON_PUSH_TIMEOUT_MS);
                if (pico.CheckScreenUpdated() == true)
                {
                    return;
                }

            }
            pico.Panic("Latte push timeout!");
        }

        /// <summary>
        /// For navigation, requires "Default_screen" mask
        /// </summary>
        /// <exception cref="ButtonPushedManuallyException">If the button was pushed by user.</exception>
        /// <exception cref="PicoErrorException">If Pico reports error.</exception>
        /// <exception cref="UnexpectedStateException">If unexpected state occurs.</exception>
        private void PushMenu()
        {
            for (int attempts = 0; attempts < PUSH_ATTEMPT_NUM; ++attempts)
            {
                if (pico.PushButton(PicoRegisters.Buttons.Menu, pico.StandardRecords["Default_screen"]) == false)
                {
                    pico.Panic("Menu push failed!");
                }

                Thread.Sleep(BUTTON_PUSH_TIMEOUT_MS);
                if (pico.CheckScreenUpdated() == true)
                {
                    return;
                }

            }
            pico.Panic("Menu push timeout!");
        }

        /// <summary>
        /// For navigation, requires "Acknowledge_icon" mask
        /// </summary>
        /// <exception cref="ButtonPushedManuallyException">If the button was pushed by user.</exception>
        /// <exception cref="PicoErrorException">If Pico reports error.</exception>
        /// <exception cref="UnexpectedStateException">If unexpected state occurs.</exception>
        private void Acknowledge()
        {
            for (int attempts = 0; attempts < PUSH_ATTEMPT_NUM; ++attempts)
            {
                if (pico.PushButton(PicoRegisters.Buttons.Aroma, pico.StandardRecords["Icon_acknowledge"]) == false)
                {
                    pico.Panic("Acknowledge push failed!");
                }

                Thread.Sleep(BUTTON_PUSH_TIMEOUT_MS);
                if (pico.CheckScreenUpdated() == true)
                {
                    return;
                }

            }
            pico.Panic("Acknowledge push timeout!");
        }

        /// <summary>
        /// For navigation, requires "Back_icon" mask
        /// </summary>
        /// <exception cref="ButtonPushedManuallyException">If the button was pushed by user.</exception>
        /// <exception cref="PicoErrorException">If Pico reports error.</exception>
        /// <exception cref="UnexpectedStateException">If unexpected state occurs.</exception>
        private void GoBack()
        {
            for (int attempts = 0; attempts < PUSH_ATTEMPT_NUM; ++attempts)
            {
                if (pico.PushButton(PicoRegisters.Buttons.Espresso, pico.StandardRecords["Icon_back"]) == false)
                {
                    pico.Panic("Go_back push failed!");
                }

                Thread.Sleep(BUTTON_PUSH_TIMEOUT_MS);
                if (pico.CheckScreenUpdated() == true)
                {
                    return;
                }

            }
            pico.Panic("Go_back push timeout!");
        }

        /// <summary>
        /// For navigation, requires "Up_icon" mask
        /// </summary>
        /// <exception cref="ButtonPushedManuallyException">If the button was pushed by user.</exception>
        /// <exception cref="PicoErrorException">If Pico reports error.</exception>
        /// <exception cref="UnexpectedStateException">If unexpected state occurs.</exception>
        private void GoUp()
        {
            for (int attempts = 0; attempts < PUSH_ATTEMPT_NUM; ++attempts)
            {
                if (pico.PushButton(PicoRegisters.Buttons.Capuccino, pico.StandardRecords["Icon_up"]) == false)
                {
                    pico.Panic("Go_up push failed!");
                }

                Thread.Sleep(BUTTON_PUSH_TIMEOUT_MS);
                if (pico.CheckScreenUpdated() == true)
                {
                    return;
                }

            }
            pico.Panic("Go_up push timeout!");
        }

        /// <summary>
        /// For navigation, requires "Down_icon" mask
        /// </summary>
        /// <exception cref="ButtonPushedManuallyException">If the button was pushed by user.</exception>
        /// <exception cref="PicoErrorException">If Pico reports error.</exception>
        /// <exception cref="UnexpectedStateException">If unexpected state occurs.</exception>
        private void GoDown()
        {
            for (int attempts = 0; attempts < PUSH_ATTEMPT_NUM; ++attempts)
            {
                if (pico.PushButton(PicoRegisters.Buttons.Menu, pico.StandardRecords["Icon_down"]) == false)
                {
                    pico.Panic("Go_down push failed!");
                }

                Thread.Sleep(BUTTON_PUSH_TIMEOUT_MS);
                if (pico.CheckScreenUpdated() == true)
                {
                    return;
                }

            }
            pico.Panic("Go_down push timeout!");
        }





        //Private methods for user requests

        /// <summary>
        /// Turns on the device, waits until Default screen appears
        /// </summary>
        /// <returns>True if machine is turned on, false if unknown state occured.</returns>
        /// <exception cref="ButtonPushedManuallyException">If the button was pushed by user.</exception>
        /// <exception cref="PicoErrorException">If Pico reports error.</exception>
        private bool PowerOn()
        {
            //Machine is running
            if (pico.IsActive() == true)
            {
                if (pico.IsError() == true)
                {
                    State = States.Error;
                    ErrorState = pico.GetErrorState();
                    return true;
                }

                //Machine cannot go to default state
                if (ReturnToHomeScreen() == false)
                {
                    return false;
                }
                State = States.Idle;
                return true;
            }

            //Machine is not plugged in
            if (pico.PowerOn() == false)
            {
                State = States.AC_disconnected;
                return true;
            }

            //Waiting for machine to turn on
            for (int i = 0; i < MAX_SWITCHING_TIME; i += READ_SCREEN_TIME_MS)
            {
                Thread.Sleep(READ_SCREEN_TIME_MS);

                //Machine is in error state
                if (pico.IsError() == true)
                {
                    State = States.Error;
                    ErrorState = pico.GetErrorState();
                    return true;
                }

                //Machine is running
                else if (pico.IsRunning() == true)
                {
                    Thread.Sleep(1000);
                    try
                    {
                        //Clears error message //TODO: report it
                        if (pico.CheckScreen(pico.StandardRecords["Icon_back"]) == true)
                        {
                            GoBack();
                        }

                        //Waits for initialization if required
                        while (pico.WaitForInitialization() == true);
                        if (pico.CheckScreen(pico.StandardRecords["Default_screen"]) == true)
                        {
                            State = States.Idle;
                            return true;
                        }
                    }
                    catch (UnexpectedStateException e)
                    {
                        MyLogger.LogEvent(e, MethodBase.GetCurrentMethod()?.Name);
                        throw new FatalErrorException("Unknown state during powering on the device!");
                    }
                }
            }
            throw new FatalErrorException("Unknown state during powering on the device!");
        }

        /// <summary>
        /// Turns off the device
        /// </summary>
        /// <exception cref="ButtonPushedManuallyException">If the button was pushed by user.</exception>
        /// <exception cref="PicoErrorException">If Pico reports error.</exception>
        private void PowerOff()
        {
            if (pico.IsOn() == false)
            {
                return;
            }

            if (pico.IsRunning() == true)
            {
                ReturnToHomeScreen();
            }
            pico.PowerOff();

            for (int i = 0; i < MAX_SWITCHING_TIME; i += READ_SCREEN_TIME_MS)
            {
                Thread.Sleep(READ_SCREEN_TIME_MS);
                
                //Waits for initialization if required
                while (pico.WaitForInitialization() == true) ;
                if (pico.IsOn() == false)
                {
                    State = States.Off;
                    return;
                }
            }
            throw new FatalErrorException("Unknown state during powering off the device!");
        }





        //Private methods for drink making

        /// <summary>
        /// Prepares espresso
        /// </summary>
        /// <returns>False in case of error, true otherwise</returns>
        /// <exception cref="ButtonPushedManuallyException">If the button was pushed by user.</exception>
        /// <exception cref="PicoErrorException">If Pico reports error.</exception>
        private bool MakeEspresso()
        {
            if (OutOfCoffee == true)
            {
                return false;
            }

            try
            {
                PushEspresso();
                pico.WaitForMask(pico.StandardRecords["Making_espresso"], SCREEN_REFRESH_TIMEOUT_MS);

                pico.WaitUntilDone(ESPRESSO_TIME_MS);
                pico.WaitForMask(pico.StandardRecords["Default_screen"], SCREEN_REFRESH_TIMEOUT_MS);
            }
            catch (UnexpectedStateException e)
            {
                MyLogger.LogEvent(e, MethodBase.GetCurrentMethod()?.Name);
                return false;
            }
            return IsHomeScreen();
        }

        /// <summary>
        /// Prepares coffee
        /// </summary>
        /// <returns>False in case of error, true otherwise</returns>
        /// <exception cref="ButtonPushedManuallyException">If the button was pushed by user.</exception>
        /// <exception cref="PicoErrorException">If Pico reports error.</exception>
        private bool MakeCoffee()
        {
            if (OutOfCoffee == true)
            {
                return false;
            }

            try
            {
                PushCoffee();
                pico.WaitForMask(pico.StandardRecords["Making_coffee"], SCREEN_REFRESH_TIMEOUT_MS);

                pico.WaitUntilDone(COFFEE_TIME_MS);
                pico.WaitForMask(pico.StandardRecords["Default_screen"], SCREEN_REFRESH_TIMEOUT_MS);
            }
            catch (UnexpectedStateException e)
            {
                MyLogger.LogEvent(e, MethodBase.GetCurrentMethod()?.Name);
                return false;
            }
            return IsHomeScreen();
        }

        /// <summary>
        /// Prepares Americano
        /// </summary>
        /// <returns>False in case of error, true otherwise</returns>
        /// <exception cref="ButtonPushedManuallyException">If the button was pushed by user.</exception>
        /// <exception cref="PicoErrorException">If Pico reports error.</exception>
        private bool MakeAmericano()
        {
            if (OutOfCoffee == true)
            {
                return false;
            }

            try
            {
                PushMenu();
                pico.WaitForMask(pico.StandardRecords["Menu_1_menu"], SCREEN_REFRESH_TIMEOUT_MS);

                GoDown();
                pico.WaitForMask(pico.StandardRecords["Menu_1_drinks"], SCREEN_REFRESH_TIMEOUT_MS);

                Acknowledge();
                pico.WaitForMask(pico.StandardRecords["Drink_americano"], SCREEN_REFRESH_TIMEOUT_MS);

                Acknowledge();
                pico.WaitForMask(pico.StandardRecords["Making_americano"], SCREEN_REFRESH_TIMEOUT_MS);

                pico.WaitUntilDone(AMERICANO_TIME_MS);
                pico.WaitForMask(pico.StandardRecords["Default_screen"], SCREEN_REFRESH_TIMEOUT_MS);
            }
            catch (UnexpectedStateException e)
            {
                MyLogger.LogEvent(e, MethodBase.GetCurrentMethod()?.Name);
                return false;
            }
            return IsHomeScreen();
        }

        /// <summary>
        /// Prepares hot water
        /// </summary>
        /// <returns>False in case of error or if milk carafe is attached, true otherwise</returns>
        /// <exception cref="ButtonPushedManuallyException">If the button was pushed by user.</exception>
        /// <exception cref="PicoErrorException">If Pico reports error.</exception>
        private bool MakeHotWater()
        {
            if (carafeAttached == true)
            {
                return false;
            }
            try
            {
                PushMenu();
                pico.WaitForMask(pico.StandardRecords["Menu_1_menu"], SCREEN_REFRESH_TIMEOUT_MS);

                GoDown();
                pico.WaitForMask(pico.StandardRecords["Menu_1_drinks"], SCREEN_REFRESH_TIMEOUT_MS);

                Acknowledge();
                pico.WaitForMask(pico.StandardRecords["Drink_americano"], SCREEN_REFRESH_TIMEOUT_MS);

                GoDown();
                pico.WaitForMask(pico.StandardRecords["Drink_hot_water"], SCREEN_REFRESH_TIMEOUT_MS);

                Acknowledge();
                pico.WaitForMask(pico.StandardRecords["Attach_hot_water_spout"], SCREEN_REFRESH_TIMEOUT_MS);

                Acknowledge();
                pico.WaitForMask(pico.StandardRecords["Making_hot_water"], SCREEN_REFRESH_TIMEOUT_MS);

                for (int t = 0; t < HOT_WATER_TIME_MS; t += READ_SCREEN_TIME_MS)
                {
                    if (pico.CheckScreen(pico.StandardRecords["Making_hot_water"]) == false)
                    {
                        pico.Panic("Unknown state occured during hot water making!");
                    }
                    Thread.Sleep(SCREEN_REFRESH_TIMEOUT_MS);
                }

                Acknowledge();
            }

            catch (UnexpectedStateException e)
            {
                MyLogger.LogEvent(e, MethodBase.GetCurrentMethod()?.Name);
                return false;
            }
            Thread.Sleep(TEMPORARY_SCREEN_TIMEOUT_MS);
            return IsHomeScreen();
        }

        //Private method for machine configuration

        /// <summary>
        /// Sets intensity of coffee
        /// </summary>
        /// <param name="degree">Intensity degree (0 for ground coffee)</param>
        /// <returns>False in case of error, true otherwise</returns>
        /// <exception cref="ButtonPushedManuallyException">If the button was pushed by user.</exception>
        /// <exception cref="PicoErrorException">If Pico reports error.</exception>
        private bool SetAroma(int degree)
        {
            DatabaseRecord mask;
            switch (degree)
            {
                case 0:
                    mask = pico.StandardRecords["Intensity_ground_coffee"];
                    break;
                case 1:
                    mask = pico.StandardRecords["Intensity_1"];
                    break;
                case 2:
                    mask = pico.StandardRecords["Intensity_2"];
                    break;
                case 3:
                    mask = pico.StandardRecords["Intensity_3"];
                    break;
                case 4:
                    mask = pico.StandardRecords["Intensity_4"];
                    break;
                case 5:
                    mask = pico.StandardRecords["Intensity_5"];
                    break;
                default:
                    return false;
            }

            int counter = 0;
            while (pico.CheckScreen(mask) != true)
            {
                ++counter;
                if (counter > SET_AROMA_ATTEMPTS_NUM)
                {
                    return false;
                }
                try
                {
                    PushAroma();
                }
                catch (UnexpectedStateException e)
                {
                    MyLogger.LogEvent(e, MethodBase.GetCurrentMethod()?.Name);
                    return false;
                }
            }
            return IsHomeScreen();
        }

        /// <summary>
        /// Sets temperature of coffee - DO NOT USE!!!
        /// </summary>
        /// <param name="temperature">0 for low, 1 for medium, 2 for high</param>
        /// <returns>False in case of error, true otherwise</returns>
        /// <exception cref="ButtonPushedManuallyException">If the button was pushed by user.</exception>
        /// <exception cref="PicoErrorException">If Pico reports error.</exception>
        private bool SetTemperature(int temperature)
        {
            DatabaseRecord mask;
            switch (temperature)
            {
                case 0:
                    mask = pico.StandardRecords["Coffee_temp_min"];
                    break;
                case 1:
                    mask = pico.StandardRecords["Coffee_temp_med"];
                    break;
                case 2:
                    mask = pico.StandardRecords["Coffee_temp_max"];
                    break;
                default:
                    return false;
            }

            try
            {
                PushMenu();
                pico.WaitForMask(pico.StandardRecords["Menu_1_menu"], SCREEN_REFRESH_TIMEOUT_MS);

                Acknowledge();
                pico.WaitForMask(pico.StandardRecords["Menu_2_quick_clean"], SCREEN_REFRESH_TIMEOUT_MS);

                GoDown();
                pico.WaitForMask(pico.StandardRecords["Menu_2_coffee_temp"], SCREEN_REFRESH_TIMEOUT_MS);

                Thread.Sleep(BUTTON_PUSH_TIMEOUT_MS);
                Acknowledge();
                pico.WaitForMask(pico.StandardRecords["Coffee_temp_selection"], SCREEN_REFRESH_TIMEOUT_MS);

                int counter = 0;
                while (pico.CheckScreen(mask) != true)
                {
                    ++counter;
                    if (counter > SET_TEMPERATURE_ATTEMPTS_NUM)
                    {
                        return false;
                    }

                    GoDown();
                }

                Acknowledge();
                pico.WaitForMask(pico.StandardRecords["Acknowledged"], SCREEN_REFRESH_TIMEOUT_MS);
                pico.WaitForMask(pico.StandardRecords["Icon_back"], TEMPORARY_SCREEN_TIMEOUT_MS);

            }
            catch (UnexpectedStateException e)
            {
                MyLogger.LogEvent(e, MethodBase.GetCurrentMethod()?.Name);
                return false;
            }
            return ReturnToHomeScreen();
        }





        //Private methods for maintenance

        /// <summary>
        /// Method checks whether the machine is at home screen. If yes,
        /// sets State to Idle
        /// Also checks whether the machine has coffee.
        /// </summary>
        /// <returns>Whether the machine is at home screen.</returns> 
        /// <exception cref="ButtonPushedManuallyException">If the button was pushed by user.</exception>
        /// <exception cref="PicoErrorException">If Pico reports error.</exception>
        private bool IsHomeScreen()
        {
            if (pico.CheckScreen(pico.StandardRecords["Default_screen"]) == true)
            {
                OutOfCoffee = false;
                return true;
            }

            else if (pico.CheckScreen(pico.StandardRecords["Default_screen_no_coffee"]) == true)
            {
                OutOfCoffee = true;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Method attempts to return to default screen by continuously pressing BACK button.
        /// </summary>
        /// <returns>True if operation ended at home screen, false otherwise</returns> 
        /// <exception cref="ButtonPushedManuallyException">If the button was pushed by user.</exception>
        /// <exception cref="PicoErrorException">If Pico reports error.</exception>
        private bool ReturnToHomeScreen()
        {
            for (int t = 0; t < RETURN_TO_HOME_MS; t += BUTTON_PUSH_TIMEOUT_MS)
            {
                if (IsHomeScreen() == true)
                {
                    return true;
                }

                pico.WaitForInitialization();

                try
                {
                    GoBack();
                }
                catch (UnexpectedStateException e)
                {
                    MyLogger.LogEvent(e, MethodBase.GetCurrentMethod()?.Name);
                    return false;
                }
            }
            return false;
        }

        /// <summary>
        /// Checks whether the procedure requires milk carafe. If it is
        /// not present, immediately cancels pending operation. Should be called
        /// in a loop.
        /// </summary>
        ///<returns>False if operation failed, true otherwise</returns>
        /// <exception cref="ButtonPushedManuallyException">If the button was pushed by user.</exception>
        /// <exception cref="PicoErrorException">If Pico reports error.</exception>
        private bool GuardCarafe()
        {
            if (carafeAttached == true)
            {
                return true;
            }
            try
            {
                if (pico.CheckScreen([pico.StandardRecords["Milk_carafe_attach"], pico.StandardRecords["Milk_carafe_dispensing_spout_closed"],
                    pico.StandardRecords["Milk_carafe_dispensing_spout_opened"], pico.StandardRecords["Milk_carafe_clean_request"],
                    pico.StandardRecords["Milk_carafe_put_cup_under"], pico.StandardRecords["Milk_carafe_hot_steam"]]) == true)
                {
                    GoBack();
                }
            }
            catch (UnexpectedStateException e)
            {
                MyLogger.LogEvent(e, MethodBase.GetCurrentMethod()?.Name);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Diagnoses current state of machine
        /// </summary>
        /// <exception cref="ButtonPushedManuallyException">If the button was pushed by user.</exception>
        /// <exception cref="PicoErrorException">If Pico reports error.</exception>
        private void DiagnoseState()
        {
            //Machine is on
            if (pico.IsActive() == true)
            {
                //Machine is in error state
                if (pico.IsError() == true)
                {
                    State = States.Error;
                    ErrorState = pico.GetErrorState();
                }

                //Machine is running
                else if (pico.IsRunning() == true)
                {
                    ErrorState = "";
                    State = States.Running;
                }

                //Unknown standard state
                else
                {
                    throw new FatalErrorException("Unknown standard state!");
                }
            }

            //No AC power
            else if (pico.IsStandby() == false)
            {
                ErrorState = "";
                State = States.AC_disconnected;
            }

            //Machine is off
            else
            {
                ErrorState = "";
                State = States.Off;
            }
        }

        /// <summary>
        /// Function Resettings the microcontroller
        /// </summary>
        private void ResetPico()
        {
            pins.Write(RESET_PIN, PinValue.Low);
            Thread.Sleep(500);
            pins.Write(RESET_PIN, PinValue.High);
            Thread.Sleep(1500);
        }

        /// <summary>
        /// Fulfills user request
        /// </summary>
        /// <returns>Whether the request was fulfilled successfully and machine
        ///  is at home screen. All requests should end there, except of
        /// PowerOff and DiagnoseState(unless the machine is in Running state)</returns>
        /// <exception cref="ButtonPushedManuallyException">If the button was pushed by user.</exception>
        /// <exception cref="PicoErrorException">If Pico reports error.</exception>
        private bool FulfillRequest()
        {
            bool result = true;

            if (userRequest != Requests.None)
            {
                MyLogger.LogEvent($"Request {userRequest} started with additional parameter: {additionalParameter}");
            }

            switch (userRequest)
            {
                case Requests.None:
                    break;
                case Requests.GetState:
                    DiagnoseState();
                    if (State == States.Running)
                    {
                        if (IsHomeScreen() == true)
                        {
                            State = States.Idle;
                            result = true;
                        }
                        else
                        {
                            result = false;
                        }
                    }
                    break;
                case Requests.PowerOn:
                    State = States.Switching;
                    result = PowerOn();
                    break;
                case Requests.PowerOff:
                    State = States.Switching;
                    PowerOff();
                    break;
                case Requests.MakeEspresso:
                    State = States.DrinkMaking;
                    result = MakeEspresso();
                    break;
                case Requests.MakeCoffee:
                    State = States.DrinkMaking;
                    result = MakeCoffee();
                    break;
                case Requests.MakeAmericano:
                    State = States.DrinkMaking;
                    result = MakeAmericano();
                    break;
                case Requests.MakeHotWater:
                    State = States.DrinkMaking;
                    result = MakeHotWater();
                    break;
                case Requests.SetAroma:
                    State = States.MachineSetting;
                    result = SetAroma(additionalParameter);
                    break;
                case Requests.SetTemperature:
                    State = States.MachineSetting;
                    result = SetTemperature(additionalParameter);
                    break;
                default:
                    break;
            }
            MyLogger.LogEvent($"Request {userRequest} fulfilled with result: {result}");
            return result;
        }

        /// <summary>
        /// Main controll loop, should run in separate thread
        /// </summary>
        public void MainControlLoop()
        {
            ResetPico();
            userRequest = Requests.None;
            additionalParameter = 0;
            State = States.Resetting;
            while (endThread == false)
            {
                if (State == States.FatalError)
                {
                    Thread.Sleep(1000);
                }

                //Attempt to recover from fatal error
                /*else if (fatalError == true)
                {
                    fatalErrorRecoveryAttemptTime -= MAIN_LOOP_REFRESH_TIME_MS;
                    if (fatalErrorRecoveryAttemptTime == 0)
                    {
                        fatalError = false;
                        State = States.Resetting;
                    }
                }*/

                else
                {
                    try
                    {
                        //If Reset is needed
                        if (State == States.Resetting)
                        {
                            MyLogger.LogEvent("Resetting started!");
                            DiagnoseState();
                            if (State == States.Running)
                            {
                                //Waits for initialization if required
                                while (pico.WaitForInitialization() == true);
                                Thread.Sleep(SCREEN_REFRESH_TIMEOUT_MS);



                                DiagnoseState();
                                if (State == States.Running)
                                {
                                    //If procedure is running, TODO: hot water
                                    if (pico.CheckScreen([pico.StandardRecords["Progress_bar"], pico.StandardRecords["Making_hot_water"]]) == true)
                                    {
                                        pico.WaitUntilDone(MAX_WAITING_TIME_MS);
                                        Thread.Sleep(SCREEN_REFRESH_TIMEOUT_MS);
                                    }


                                    DiagnoseState();
                                    if (State == States.Running)
                                    {
                                        //Tries to return home
                                        if (ReturnToHomeScreen() == false)
                                        {
                                            //Throw if unable to Reset
                                            throw new FatalErrorException("Resetting failed, machine state cannot be determined!");
                                        }
                                        State = States.Idle;
                                    }
                                }
                            }
                            MyLogger.LogEvent("Resetting done!");
                            Busy = false;
                            connectionTimedOut = false;
                        }

                        /*DiagnoseState();
                        if (State == States.Running && IsHomeScreen() == true)
                        {
                            State = States.Idle;
                        }*/

                        if (userRequest != Requests.None)
                        {
                            //If user request failed
                            if (FulfillRequest() == false)
                            {
                                userRequest = Requests.None;
                                additionalParameter = 0;
                                State = States.Resetting;
                            }

                            else
                            {
                                if (State == States.DrinkMaking || State == States.MachineSetting)
                                {
                                    State = States.Idle;
                                }
                                userRequest = Requests.None;
                                additionalParameter = 0;
                                Busy = false;
                            }

                        }
                    }

                    //Resetting pico in case of error
                    catch (PicoErrorException e)
                    {
                        Busy = true;
                        State = States.Resetting;
                        MyLogger.LogEvent(e);
                        ResetPico();
                    }

                    //Manual push detected
                    catch (ButtonPushedManuallyException)
                    {
                        Busy = true;
                        MyLogger.LogEvent("Manual button push detected!");
                        for (int i = 0; i < MIN_IDLE_TIME; i += READ_SCREEN_TIME_MS)
                        {
                            State = States.Resetting;
                            try
                            {
                                //In case someone tries to screw thing up
                                GuardCarafe();
                                Thread.Sleep(READ_SCREEN_TIME_MS);
                            }

                            //Resetting pico in case of error
                            catch (PicoErrorException e)
                            {
                                MyLogger.LogEvent(e);
                                ResetPico();
                                i = 0;
                            }

                            //In case of multiple pushes
                            catch (ButtonPushedManuallyException)
                            {
                                i = 0;
                            }
                        }
                    }

                    //Timeout in communication
                    catch (TimeoutException e)
                    {
                        Busy = true;
                        State = States.Resetting;
                        MyLogger.LogEvent($"TimeoutException: {e.Message}");
                        if (connectionTimedOut == true)
                        {
                            MyLogger.LogEvent("Multiple communication timeouts occured!");
                            ErrorState = "Communication timeout!";
                            State = States.FatalError;
                        }
                        else
                        {
                            ResetPico();
                            connectionTimedOut = true;
                        }
                    }

                    //Fatal error
                    catch (FatalErrorException e)
                    {
                        Busy = true;
                        State = States.FatalError;
                        MyLogger.LogEvent(e);
                        //fatalError = true;
                        //fatalErrorRecoveryAttemptTime = FATAL_ERROR_RECOVERY_TIMEOUT;
                        ErrorState = "Fatal error!";
                    }

                    //General exception
                    catch (Exception e)
                    {
                        Busy = true;
                        State = States.FatalError;
                        MyLogger.LogEvent($"Unknown exception: {e.Message}");
                        ErrorState = "Unknown exception!";
                    }
                }

                //New data detected
                if (pins.Read(IRQ_PIN) == PinValue.High && Busy == false)
                {
                    Busy = true;
                    userRequest = Requests.GetState;
                    MyLogger.LogEvent("New data detected!");
                }
                Thread.Sleep(MAIN_LOOP_REFRESH_TIME_MS);
                //break;
            }
        }





        //Public method for interaction with user

        /// <summary>
        /// Sets action request from user
        /// </summary>
        /// <param name="request">Requested action</param>
        /// <param name="additionalParameter">Additional parameters, if action has some</param>
        /// <returns>True if request can be set (machine is in idel state), false otherwise</returns>
        public bool SetRequest(Requests request, int additionalParameter = 0)
        {
            if (State == States.FatalError || Busy == true)
            {
                return false;
            }

            switch (request)
            {
                case Requests.GetState:
                case Requests.PowerOn:
                case Requests.PowerOff:
                    break;
                default:
                    if (State != States.Idle)
                    {
                        return false;
                    }
                    break;

            }
            Busy = true;
            userRequest = request;
            this.additionalParameter = additionalParameter;
            return true;
        }

        /// <summary>
        /// Starts the control loop in separated thread
        /// </summary>
        /// <returns>New Thread object</returns>
        public Thread StartControlLoop()
        {
            endThread = false;
            Busy = true;
            ControlThread = new Thread(MainControlLoop);
            ControlThread.IsBackground = true;
            ControlThread.Start();
            return ControlThread;
        }

        /// <summary>
        /// Terminates controll loop
        /// </summary>
        public void StopControlLoop()
        {
            endThread = true;
            if (ControlThread != null && ControlThread.IsAlive)
            {
                ControlThread.Join();
            }
            ControlThread = null;
            Busy = true;
        }
    }
    
    

    /// <summary>
    /// Used to log warning and dump registers in case of error.
    /// </summary>
    public class EventLogger
    {
        public List<string> events { get; private set; } = [];
        private int readMessages = 0;
        private string errorOutput = "";

        /// <summary>
        /// Reads first unread message from logger.
        /// </summary>
        /// <returns>
        /// First ubnread message, or empty string if all messages has been read.
        /// </returns>
        public string ReadNextMessage()
        {
            if (readMessages >= events.Count)
            {
                return "";
            }
            return events[readMessages++];
        }

        /// <summary>
        /// Used to log generic event
        /// </summary>
        /// <param name="msg">Error message</param>
        public void LogEvent(string msg)
        {
            msg = DateTime.Now.ToString() + ": " + msg;

            events.Add(msg);
            File.AppendAllLines(errorOutput, [msg]);
        }

        /// <summary>
        /// Used to log error reported by pico
        /// </summary>
        /// <param name="e">PicoErrorException object</param>
        public void LogEvent(PicoErrorException e)
        {
            string msg = DateTime.Now.ToString() + ": " +
                e.Message + "; " +
                $"Input register: {e.inputs}; " +
                $"Command register: {e.inputs}; ";

            events.Add(msg);
            File.AppendAllLines(errorOutput, [msg]);
        }

        /// <summary>
        /// Used to log fatal error
        /// </summary>
        /// <param name="e">FatalErrorException object</param>
        public void LogEvent(FatalErrorException e)
        {
            string msg = DateTime.Now.ToString() + ": " +
                "Fatal error: " + e.Message + "; ";

            events.Add(msg);
            File.AppendAllLines(errorOutput, [msg]);
        }

        /// <summary>
        /// Used to log unexpected state
        /// </summary>
        /// <param name="e">UnexpectedStateException object</param>
        public void LogEvent(UnexpectedStateException e, string? catchedIn)
        {
            string msg = DateTime.Now.ToString() + ": " +
                $"{catchedIn ?? ""}: {e.Message}; " +
                $"Input register: {e.inputs}; " +
                $"Command register: {e.commands}; ";

            events.Add(msg);
            File.AppendAllLines(errorOutput, [msg]);
        }

        /// <summary>
        /// Constructor for logger
        /// </summary>
        /// <param name="path">Path to log file.</param>
        public EventLogger(string path)
        {
            errorOutput = path;
            File.WriteAllText(path, ""); //Clear file
            LogEvent("Logger object created!");
        }
    }

    /// <summary>
    /// Fatal error occured, recovery is not possible.
    /// </summary>
    public class FatalErrorException : Exception
    {
        public FatalErrorException(string msg) : base(msg) { }
    }
}
        