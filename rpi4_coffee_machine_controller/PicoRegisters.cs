
namespace CoffeMachineController{
    public class PicoRegisters{
        //Adresses and constants
        public const ushort INPUT_REGISTER_ADDRESS = 0;
        public const ushort HOLDING_REGISTER_ADDRESS = 0;
        public const ushort TRANSACTION_NUM = 5;
        public const ushort REGISTER_NUM = 107;
        public readonly ushort[] REGISTER_GROUPS = [1000, 2000, 3000, 4000, 5000];

        /// <summary>
        /// Buttons on machine control panel
        /// </summary>
        public enum Buttons : ushort
        {
            Espresso = 0,
            Latte = 1,
            Capuccino = 2,
            Menu = 3,
            Aroma = 6,
            Coffee = 7,
            Power = 8
        }

        /// <summary>
        /// Diagnostic data reported by pico
        /// </summary>
        public enum Events : ushort
        {
            poweredOn = 10,
            standbyOn = 11,
            redScreen = 12,
            whiteScreen = 13,
            spiRunning = 14,
            regRunning = 15
        }

        /// <summary>
        /// Settings of pico
        /// </summary>
        public enum Functions : ushort
        {
            buttonClearDisable = 10,
        }

        /// <summary>
        /// Input register
        /// </summary>
        public class Inputs()
        {
            /// <summary>
            /// Raw value
            /// </summary>
            private ushort Value = 0;

            //Private methods
            /// <summary>
            /// Checks whether the event occured.
            /// </summary>
            /// <param name="evnt">Event number</param>
            /// <returns>True if event occured, false otherwise.</returns>
            private bool CheckEvent(Events evnt)
            {
                return (Value & (1 << (ushort)evnt)) > 0;
            }

            //Public methods
            /// <summary>
            /// Updates register value
            /// </summary>
            /// <param name="newValue">New value</param>
            public void UpdateValue(ushort newValue)
            {
                Value = newValue;
            }

            /// <summary>
            /// Checks whether the specific button is pushed
            /// </summary>
            /// <param name="button">Button number</param>
            /// <returns>True if button is pushed, false otherwise.</returns>
            public bool IsPushed(Buttons button)
            {
                return (Value & (1 << (ushort)button)) > 0;
            }
            /// <summary>
            /// Checks whether the button was pushed manually.
            /// </summary>
            /// <returns>True if button was pushed, false otherwise.</returns>
            public bool ButtonPushedMaually()
            {
                return (Value & 0b00010000) > 0;
            }
            /// <summary>
            /// Checks whether the button push failed. 
            /// WARNING:
            /// Pico may sometimes report false button push failures,
            /// so it is recommended to check for button push manually 
            /// in program.
            /// </summary>
            /// <returns>True if button push failed, false otherwise.</returns>
            public bool ButtonPushFailed()
            {
                return (Value & 0b00100000) > 0;
            }
            /// <summary>
            /// Checks whether the machine is on. 
            /// WARNING:
            /// After the machine has been turned on, there is 
            /// a small delay until white or red screen appears, so be carefull 
            /// with this function.
            /// </summary>
            /// <returns>True if machine is on, false otherwise.</returns>
            public bool IsOn()
            {
                return CheckEvent(Events.poweredOn);
            }
            /// <summary>
            /// Checks whether the machine is in standby mode.
            /// </summary>
            /// <returns>True if machine is in stand-by mode, false otherwise.</returns>
            public bool IsStandby()
            {
                return CheckEvent(Events.standbyOn);
            }
            /// <summary>
            /// Checks whether the screen is lighting (machine is active).
            /// </summary>
            /// <returns>True if screen is lighting, false otherwise.</returns>
            public bool IsActive()
            {
                return CheckEvent(Events.redScreen) || CheckEvent(Events.whiteScreen);
            }
            /// <summary>
            /// Checks whether the machine is in error state 
            /// (red screen lighting.)
            /// </summary>
            /// <returns>True if error occured, false otherwise.</returns>
            public bool IsError()
            {
                return CheckEvent(Events.redScreen);
            }
            /// <summary>
            /// Checks whether the machine is in normal state 
            /// (white screen lighting.)
            /// </summary>
            /// <returns>True if device is running, false otherwise.</returns>
            public bool IsRunning()
            {
                return CheckEvent(Events.whiteScreen);
            }
            /// <summary>
            /// Checks whether the SPI capture is active.
            /// </summary>
            /// <returns>True if SPI capture is running, false otherwise.</returns>
            public bool SpiRunning()
            {
                return CheckEvent(Events.spiRunning);
            }
            /// <summary>
            /// Checks whether the shift register handling is active.
            /// </summary>
            /// <returns>True if register handling is active, false otherwise.</returns>
            public bool RegRunning()
            {
                return CheckEvent(Events.regRunning);
            }
            /// <summary>
            /// Prints the diagnostic data in text form.
            /// </summary>
            public void PrintStatus()
            {
                Console.WriteLine("Buttons:");
                Console.WriteLine($"espresso: {IsPushed(Buttons.Espresso)}");
                Console.WriteLine($"latte: {IsPushed(Buttons.Latte)}");
                Console.WriteLine($"capuccino: {IsPushed(Buttons.Capuccino)}");
                Console.WriteLine($"menu: {IsPushed(Buttons.Menu)}");
                Console.WriteLine($"manual push: {ButtonPushedMaually()}");
                Console.WriteLine($"push failed: {ButtonPushFailed()}");
                Console.WriteLine($"aroma: {IsPushed(Buttons.Aroma)}");
                Console.WriteLine($"coffee: {IsPushed(Buttons.Coffee)}");

                Console.WriteLine($"InputRegister:");
                Console.WriteLine($"power button: {IsPushed(Buttons.Power)}");
                Console.WriteLine($"dummy1: {false}");
                Console.WriteLine($"powered_on: {IsOn()}");
                Console.WriteLine($"standby_on: {IsStandby()}");
                Console.WriteLine($"red_screen: {IsError()}");
                Console.WriteLine($"green_screen: {IsRunning()}");
                Console.WriteLine($"spi recv running: {SpiRunning()}");
                Console.WriteLine($"reg handler running: {RegRunning()}");
                Console.WriteLine();
            }
            /// <summary>
            /// Used to stringify this object
            /// </summary>
            /// <returns>Raw value in string format.</returns>
            public override string ToString()
            {
                return Value.ToString();
            }
        }

        /// <summary>
        /// Command register
        /// </summary>
        public class Commands()
        {
            /// <summary>
            /// Raw value of register
            /// </summary>
            public ushort Value { get; private set; }

            //Public methods
            /// <summary>
            /// Applies button push command
            /// </summary>
            /// <param name="button">Number of button to be pushed.</param>
            public void PushButton(Buttons button)
            {
                Value |= (ushort)(1 << (ushort)button);
            }
            /// <summary>
            /// Releases the pushed button
            /// </summary>
            /// <param name="button">Number of button to be released.</param>
            public void ReleaseButton(Buttons button)
            {
                Value &= (ushort)~(1 << (ushort)button);
            }
            /// <summary>
            /// Releases all buttons.
            /// </summary>
            public void ResetButtons()
            {
                Value = (ushort)(Value & 0b1111111000000000);
            }
            /// <summary>
            /// Applies specified setting to pico
            /// </summary>
            /// <param name="func">Number of setting</param>
            public void SetFunction(Functions func)
            {
                Value |= (ushort)(1 << (ushort)func);
            }
            /// <summary>
            /// Resets specified setting to pico
            /// </summary>
            /// <param name="func">Number of setting</param>
            public void ResetFunction(Functions func)
            {
                Value &= (ushort)~(1 << (ushort)func);
            }

            /// <summary>
            /// Used to stringify the object
            /// </summary>
            /// <returns>Raw value in string format</returns>
            public override string ToString()
            {
                return Value.ToString();
            }

        }

        /// <summary>
        /// SPI registers
        /// </summary>
        public class SpiData()
        {
            public const ushort TOTAL_LEN = 1063;
            public const ushort TOTAL_PARSED_LEN = 1024;
            const ushort GLOBAL_HEADER_LEN = 15;
            const ushort ROW_HEADER_LEN = 3;
            const ushort PIXELS_IN_ROW = 128;
            const ushort TOTAL_ROWS = 8; //Every byte in row carries information about 8 pixels (in column)

            /// <summary>
            ///  Buffer with raw spi bytes
            /// </summary>
            private byte[] SpiBuffer = new byte[TOTAL_LEN + 1];

            /// <summary>
            /// Parses received data into byte buffer
            /// </summary>
            /// <param name="receivedData">Data received via ModbusRtu</param>
            public void ParseReceivedData(ushort[][] receivedData)
            {
                int bufferIterator = 0;
                foreach (ushort[] buffer in receivedData)
                {
                    foreach (ushort value in buffer)
                    {
                        SpiBuffer[bufferIterator++] = (byte)(value & 0xff);
                        SpiBuffer[bufferIterator++] = (byte)((value & 0xff00) >> 8);
                        if (bufferIterator >= TOTAL_LEN)
                        {
                            goto Exit;
                        }
                    }
                }
            Exit:;
            }

            /// <summary>
            /// Parses raw SPI data into screen record data
            /// </summary>
            /// <returns>Parsed screen data</returns>
            public byte[] GetScreenData()
            {
                byte[] ScreenData = new byte[TOTAL_PARSED_LEN];
                ushort iterator = GLOBAL_HEADER_LEN;

                for (ushort i = 0; i < TOTAL_ROWS; ++i)
                {
                    iterator += ROW_HEADER_LEN;
                    Array.Copy(SpiBuffer, iterator, ScreenData, i * PIXELS_IN_ROW, PIXELS_IN_ROW);
                    iterator += PIXELS_IN_ROW;
                }
                return ScreenData;
            }
            /// <summary>
            /// Used to stringify this object
            /// </summary>
            /// <returns>Character 0, because screen record is too large to be dumped.</returns>
            public override string ToString()
            {
                return "0";
            }
        }

        /// <summary>
        /// Input register of pico
        /// </summary>
        public Inputs InputRegister { get; private set; } = new();
        /// <summary>
        /// Command (holding) register of pico
        /// </summary>
        public Commands CommandRegister { get; private set; } = new();
        /// <summary>
        /// Registers used to store SPI data
        /// </summary>
        public SpiData SpiBuffer { get; private set; } = new();

    }
}