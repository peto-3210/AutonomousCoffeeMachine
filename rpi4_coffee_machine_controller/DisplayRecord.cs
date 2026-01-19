
using System.Text.Json.Serialization;

namespace CoffeMachineController{
    public class DisplayRecord
    {

        /// <value>
        /// Array of bytes representing the screen record.
        /// </value>
        [JsonIgnore]
        public byte[] ScreenRecord { get; private set; } = new byte[PicoRegisters.SpiData.TOTAL_PARSED_LEN];

        public DisplayRecord() { }

        /// <summary>
        /// Creates record from current screen data
        /// </summary>
        ///<param name="screenData">
        /// Data from pico
        /// </param>
        public DisplayRecord(byte[] screenData)
        {
            ScreenRecord = screenData;
        }

        /// <summary>
        /// Compares 2 screen records
        /// </summary>
        /// <param name="r2"></param>
        /// <returns>True if records match in every byte, false otherwise</returns>
        public bool CompareScreen(DisplayRecord r2)
        {
            return ScreenRecord.SequenceEqual(r2.ScreenRecord);
        }

        /// <summary>
        /// Updates display record data.
        /// </summary>
        /// <param name="screenData">
        /// Screen data from pico
        /// </param>
        public void UpdateRecord(byte[] screenData)
        {
            ScreenRecord = screenData;
        }

    }
}