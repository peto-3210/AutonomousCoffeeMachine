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
#include "lib/registers.h"

//UART1 variables
/*#define DEBUG_UART uart0
#define DEBUG_UART_BAUD_RATE 9600 //921600
#define DEBUG_UART_TX_PIN 0
#define DEBUG_UART_RX_PIN 1*/

//SPI variables
#define SPI_TRANSMISSION_TIME_US 7000
#define SPI_RECV_WATCHDOG_TIMEOUT_US 3*102000 //Duration of 3 SPI transmissions + delay
#define SPI_PIO pio0
#define SPI_SM 0
#define SPI_CLKDIV 5

//Register variables
#define REG_TRANSMISSION_TIME_US 45
#define REG_HANDLER_WATCHDOG_TIMEOUT_US 3*10000 //Duration of 3 REG transmission + delay
#define NUMBER_OF_REG_TRANSACTIONS 1
#define REG_PIO pio0
#define REG_SM 1
#define REG_CLKDIV 10
#define REG_BUTTON_MISMATCH_LIMIT 5


//Operation mode detection
#define NEW_DATA_SIGNAL 6
#define POWER_5V_PIN 18
#define STANDBY_LED_PIN 19
#define POWER_BUTTON_PIN 20
#define POWER_BUTTON_CONTROL 8
#define SCREEN_RED_PIN 21
#define SCREEN_WHITE_PIN 22
#define STANDBY_LED_TIMEOUT_US 3500000
#define PUSH_BUTTON_DURATION 200000


//Public registers
volatile spi_registers spi_parsed_data = {0};
volatile bool spi_lock_data = false;
volatile event_register input_data = {0};
volatile command_register command_data = {0};
volatile bool command_update_request = false;
volatile bool unread_input_data = false;
volatile bool unread_screen_data = false;

//Linked from another header
void communication_loop();

