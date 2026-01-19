
using System.Text.Json.Serialization;

namespace CoffeMachineController{
    public class DatabaseRecord : DisplayRecord, IJsonOnDeserialized, IJsonOnSerializing{
        const ushort PIXELS_IN_ROW = 128;
        const ushort PIXELS_IN_COL_BYTE = 8; //Every byte in row carries information about 8 pixels (in column)
        const ushort TOTAL_ROWS = 8;

        /// <value>
        /// Id of record in database, cannot be null.
        /// </value>
        public string Id{ get; private set; }

        /// <value>
        /// True if record is displayed in case of error.
        /// </value>
        public bool IsError { get; private set; }

        /// <value>
        /// True if record is a mask.
        /// </value>
        public bool IsMask{get; private set;}

        /// <value>
        /// Used to store screen record in graphic form.
        /// </value>
        public List<string> ScreenRecordGraphicArray{get; private set;} = [];

        /// <summary>
        /// Constructor for JSON deserializer
        /// </summary>
        [JsonConstructor]
        public DatabaseRecord(string Id, bool IsError, bool IsMask, List<string> ScreenRecordGraphicArray){
            this.Id = Id;
            this.IsError = IsError;
            this.IsMask = IsMask;
            this.ScreenRecordGraphicArray = ScreenRecordGraphicArray;
        }

        /// <summary>
        /// Creates record from current screen data
        /// </summary>
        ///<param name="currentScreen">
        /// Current screen data
        /// </param>
        /// <param name="isError">
        /// True if coffee machine is in error state
        /// </param>
        /// <param name="Id">
        /// Randomly generated Id
        /// </param>
        
        public DatabaseRecord(DisplayRecord currentScreen, bool isError, string Id) : base(currentScreen.ScreenRecord){
            IsError = isError;
            this.Id = Id;
            }

        /// <summary>
        /// Used to hide original inherited method
        /// </summary>
        /// <param name="rawData"></param>
        /// <exception cref="MethodAccessException"></exception>
        public new static void UpdateRecord(byte[] rawData){
            throw new MethodAccessException("Illegal method on this instance!");
            }

        /// <summary>
        /// Exports screen record to graphic form
        /// </summary>
        private void ExportToString()
        {
            ScreenRecordGraphicArray = [];
            string oneLine = "";
            for (ushort row = 0; row < TOTAL_ROWS; ++row)
            {
                for (ushort mask = 1; mask <= 0b10000000; mask <<= 1)
                {
                    for (ushort col = 0; col < PIXELS_IN_ROW; ++col)
                    {

                        if ((ScreenRecord[row * PIXELS_IN_ROW + col] & mask) > 0)
                        {
                            oneLine += '0';
                            Console.Write('0');
                        }
                        else
                        {
                            oneLine += '.';
                            Console.Write('.');
                        }
                    }
                    ScreenRecordGraphicArray.Add(oneLine);
                    oneLine = "";
                    Console.Write('\n');
                }
            }
            Console.Write('\n');
            Console.Write('\n');
        }

        /// <summary>
        /// Imports record from JSON file.
        /// </summary>
        private void ImportFromString(){
            for (ushort row = 0; row < TOTAL_ROWS; ++row){
                ushort iterator = 0;
                for (ushort mask = 1; mask <= 0b10000000; mask <<= 1){
                    for (ushort col = 0; col < PIXELS_IN_ROW; ++col){
                        if (ScreenRecordGraphicArray[row * PIXELS_IN_COL_BYTE + iterator][col] == '0'){
                            ScreenRecord[row*PIXELS_IN_ROW + col] |= (byte)mask;
                            Console.Write('0');
                        }
                        else if (ScreenRecordGraphicArray[row * PIXELS_IN_COL_BYTE + iterator][col] == '.'){
                            //ScreenRecord[row*PIXELS_IN_ROW + col] |= 0;
                            Console.Write('.');
                        }
                        else {
                            throw new InvalidDataException($"Invalid character at position: {row * PIXELS_IN_COL_BYTE + iterator}, {col}!");
                        }
                    }
                    ++iterator;
                    Console.Write('\n');
                }
            }
            Console.Write('\n');
            Console.Write('\n');
            ScreenRecordGraphicArray = [];
        }

        /// <summary>
        /// Checks if current screen fits the mask.
        /// All non-zero bit sequences of mask must be present in record.
        /// </summary>
        /// <param name="currentRecord">Current screen record</param>
        /// <returns></returns>
        public bool Fits(DisplayRecord currentRecord){
            if (IsMask == false)
            {
                if (CompareScreen(currentRecord) == true)
                {
                    return true;
                }
                return false;
            }

            for (int i = 0; i < ScreenRecord.Length; ++i)
            {
                if ((ScreenRecord[i] & currentRecord.ScreenRecord[i]) != ScreenRecord[i])
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Callback which executes after deserialization occurs.
        /// </summary>
        public void OnDeserialized()
        {
            ImportFromString();
        }

        /// <summary>
        /// Callback which executes before serialization occurs.
        /// </summary>
        public void OnSerializing()
        {
            ExportToString();
        }
    }
}