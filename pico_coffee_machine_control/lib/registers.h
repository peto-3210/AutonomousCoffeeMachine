#ifndef REGISTERS
#define REGISTERS

#include <stdio.h>
#include <string.h>
#include "pico/stdlib.h"
#include "hardware/gpio.h"
#include "hardware/spi.h"
#include "pico/multicore.h"
#include "pico/binary_info.h"
#include "hardware/uart.h"
#include "hardware/dma.h"
#include "hardware/irq.h"

//Length of 1 SPI transmission in bytes
#define SPI_BYTE_NUM 1063

//Maximum number of registers in 1 register group.
#define MAX_REGISTER_NUM 107
/*
Due to limitation in ModbusRTU protocol, data must
be transmitted in multiple transactions.
*/

/**
 * @brief Used for storing SPI data into the 16-bit registers
 */
typedef union {
    uint16_t spi_raw_buffer[(SPI_BYTE_NUM + 1) / 2 + 1];
    struct {
        uint16_t register_group1[MAX_REGISTER_NUM];
        uint16_t register_group2[MAX_REGISTER_NUM];
        uint16_t register_group3[MAX_REGISTER_NUM];
        uint16_t register_group4[MAX_REGISTER_NUM];
        uint16_t register_group5[MAX_REGISTER_NUM];
    };
} spi_registers;

/**
 * @brief Input register with diagnostics data from coffee machine
 */
typedef union {
    uint16_t raw_data;
    struct {
        uint8_t buttons;
        uint8_t events;
    };
    struct {
        uint8_t button_espresso_pushed  :1;
        uint8_t button_latte_pushed     :1;
        uint8_t button_capuccino_pushed :1;
        uint8_t button_menu_pushed      :1;
        uint8_t button_pushed_manually  :1;
        uint8_t button_push_failed      :1;
        uint8_t button_aroma_pushed     :1;
        uint8_t button_coffee_pushed    :1;

        uint8_t power_button_pushed :1;
        uint8_t dummy1              :1;
        uint8_t powered_on          :1;
        uint8_t standby_on          :1;
        uint8_t red_screen          :1;
        uint8_t white_screen        :1;
        uint8_t spi_recv_running    :1;
        uint8_t reg_handler_running :1;
    };
} event_register;

/**
 * @brief Holding register used to send commands to pico
 */
typedef union {
    uint16_t raw_data;
    struct {
        uint8_t buttons;
        uint8_t commands;
    };
    struct {
        uint8_t button_espresso_push    :1;
        uint8_t button_latte_push       :1;
        uint8_t button_capuccino_push   :1;
        uint8_t button_menu_push        :1;
        uint8_t dummy1                  :1;
        uint8_t dummy2                  :1;
        uint8_t button_aroma_push       :1;
        uint8_t button_coffee_push      :1;

        uint8_t power_button_push       :1;
        uint8_t dummy3                  :1;
        uint8_t button_clear_disabled   :1; //If enabled, buttton will remain pushed only for 200ms
        uint8_t dummy4                  :1; 
        uint8_t dummy5                  :1; 
        uint8_t dummy6                  :1; 
        uint8_t dummy7                  :1; 
        uint8_t dummy8                  :1;     
        uint8_t dummy9                  :1;
    };
} command_register;

//Used to put 16-bit value into buffer of bytes
#define put_16bit_into_byte_buffer(buffer, offset, value) {(buffer)[(offset) + 1] = ((value) & 0xff00) >> 8; (buffer)[(offset)] = (value) & 0xff;}

//Used to get 16-bit value from buffer of bytes
#define get_16bit_from_byte_buffer(buffer, offset) (((uint16_t)((buffer)[(offset) + 1]) << 8) | (buffer)[(offset)])

//Used to swap endianity of 16-bit value
#define endianity_swap_16bit(value) ((uint16_t)(((value) & 0xff) << 8) | (((value) & 0xff00) >> 8))

#endif