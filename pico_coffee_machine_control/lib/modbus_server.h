#include "lib/registers.h"

/*Modbus is implemented as non-inverted UART with even parity and 1 stop bit. Only
ReadInputRegisters, ReadHoldingRegisters and WriteSingleRegister functions are implemented, so the
standard request packet should consist of 6 bytes + CRC (2 bytes). Protocol data, such as 
number of registers and first register address are transmitted in big endian, payload (and CRC)
is transmitted "as is" (little endian).
*/

//UART0 variables
#define MODBUS_UART uart0
#define MODBUS_UART_BAUD_RATE 115200
#define MODBUS_UART_TX_PIN 0
#define MODBUS_UART_RX_PIN 1
#define UART_BIT_NUMBER 8
#define UART_PARITY_BITS 1
#define UART_STOP_BITS 1
#define UART_RX_BUFFER_SIZE 128

//ModbusRTU variables
#define MY_ADDRESS 2
#define MODBUS_REQUEST_BASE_LENGTH 6
#define MODBUS_READ_RESPONSE_BASE_LEN 3
#define CRC_LEN 2
#define SINGLE_READ_RESPONSE_LEN 5
#define MAX_RESPONSE_LENGTH 256
#define WAIT_FOR_BYTES 2

#define BITS_PER_BYTE (1 + UART_BIT_NUMBER + UART_PARITY_BITS + UART_STOP_BITS) //1 for start bit
#define MAX_DELAY_US (1000000 / MODBUS_UART_BAUD_RATE * BITS_PER_BYTE * WAIT_FOR_BYTES)
#define SPI_REGISTERS_READ_TIMEOUT_US 200000

#define FC_READ_HOLDING_REGISTERS 3
#define FC_READ_INPUT_REGISTERS 4
#define FC_WRITE_SINGLE_REGISTER 6
//#define FC_WRITE_MULTIPLE_REGISTERS 16

#define EX_ILLEGAL_FUNCTION 1
#define EX_ILLEGAL_ADDRESS 2
//#define EX_SERVER_BUSY 6

#define INPUT_REGISTER_ADDRESS 0000
#define HOLDING_REGISTER_ADDRESS 0000

#define SPI_INPUT_REGISTER_ADDRESS_G1 1000
#define SPI_INPUT_REGISTER_ADDRESS_G2 2000
#define SPI_INPUT_REGISTER_ADDRESS_G3 3000
#define SPI_INPUT_REGISTER_ADDRESS_G4 4000
#define SPI_INPUT_REGISTER_ADDRESS_G5 5000

#define ONBOARD_LED_TIME_US 200000
#define ONBOARD_LED_PIN 25

#define MAX_TIMERS_NUM 2

//Public registers
extern volatile spi_registers spi_parsed_data;
extern volatile bool spi_lock_data;
extern volatile event_register input_data;
extern volatile command_register command_data;
extern volatile bool command_update_request;
extern volatile bool unread_input_data;
extern volatile bool unread_screen_data;

typedef union {
    uint8_t raw_data[MODBUS_REQUEST_BASE_LENGTH + CRC_LEN + 1];
    struct {
        uint8_t address;
        uint8_t function_code;
        uint16_t first_register;
        union {
            uint16_t register_count;
            uint16_t single_register_data;
        };
        //Last register will hold CRC data
        uint16_t crc;
    };
} request_packet; 

/**
 * @brief Main loop for communication, should run on dedicated core.
 */
void communication_loop();








