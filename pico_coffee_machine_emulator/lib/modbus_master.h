#ifndef HEADERS_LIB
#define HEADERS_LIB

#include <stdio.h>
#include "pico/stdlib.h"
#include "hardware/gpio.h"
#include "hardware/spi.h"

#define MODBUS_UART uart0
#define MODBUS_UART_BAUD_RATE 115200
#define MODBUS_UART_TX_PIN 0
#define MODBUS_UART_RX_PIN 1
#define UART_BIT_NUMBER 8
#define UART_PARITY_BITS 1
#define UART_STOP_BITS 1
#define UART_RX_BUFFER_SIZE 128

//ModbusRTU variables
#define MAX_REGISTER_NUM 123
#define MY_ADDRESS 2
#define MODBUS_PACKET_BASE_LENGTH 6
#define CRC_LEN 2

#define FC_READ_HOLDING_REGISTERS 3
#define FC_READ_INPUT_REGISTERS 4
#define FC_WRITE_SINGLE_REGISTER 6
//#define FC_WRITE_MULTIPLE_REGISTERS 16

#define SPI_INPUT_REGISTER_ADDRESS_G1 1000
#define SPI_INPUT_REGISTER_ADDRESS_G2 2000
#define SPI_INPUT_REGISTER_ADDRESS_G3 3000
#define SPI_INPUT_REGISTER_ADDRESS_G4 4000
#define SPI_INPUT_REGISTER_ADDRESS_G5 5000

#define EX_ILLEGAL_FUNCTION 1
#define EX_ILLEGAL_ADDRESS 2
//#define EX_SERVER_BUSY 6

typedef union {
    uint8_t raw_data[256];
    struct {
        uint8_t address;
        uint8_t function_code;
        union {
            uint16_t first_register;
            struct {
                uint8_t exception_code;
                uint8_t dummy_byte;
            };
        };
        union {
            uint16_t register_count;
            uint16_t single_register_data;
        };
        //Last register will hold CRC data
        uint16_t registers_payload[MAX_REGISTER_NUM + 1];
    };
} modbus_packet;

//Used to swap endianity
#define endianity_swap_16bit(value) ((uint16_t)(((value) & 0xff) << 8) | (((value) & 0xff00) >> 8))

#define BUTTON_BIT_0 10
#define BUTTON_BIT_1 11
#define BUTTON_BIT_2 12
#define BUTTON_BIT_3 13

#endif