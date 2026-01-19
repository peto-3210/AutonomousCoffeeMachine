#include "lib/modbus_server.h"

#define DUMMY_NUM 32 //Used as default value wherever 0 is valid, but 32 is invalid

//NOTE: Return statement in void function will brick the device

//Address of SPI register read in previous operation
uint16_t last_read_SPI_register = 0;

//Precalculated CRC table
static const uint16_t crc_table[256] = {
	0x0000, 0xC0C1, 0xC181, 0x0140, 0xC301, 0x03C0, 0x0280, 0xC241,
	0xC601, 0x06C0, 0x0780, 0xC741, 0x0500, 0xC5C1, 0xC481, 0x0440,
	0xCC01, 0x0CC0, 0x0D80, 0xCD41, 0x0F00, 0xCFC1, 0xCE81, 0x0E40,
	0x0A00, 0xCAC1, 0xCB81, 0x0B40, 0xC901, 0x09C0, 0x0880, 0xC841,
	0xD801, 0x18C0, 0x1980, 0xD941, 0x1B00, 0xDBC1, 0xDA81, 0x1A40,
	0x1E00, 0xDEC1, 0xDF81, 0x1F40, 0xDD01, 0x1DC0, 0x1C80, 0xDC41,
	0x1400, 0xD4C1, 0xD581, 0x1540, 0xD701, 0x17C0, 0x1680, 0xD641,
	0xD201, 0x12C0, 0x1380, 0xD341, 0x1100, 0xD1C1, 0xD081, 0x1040,
	0xF001, 0x30C0, 0x3180, 0xF141, 0x3300, 0xF3C1, 0xF281, 0x3240,
	0x3600, 0xF6C1, 0xF781, 0x3740, 0xF501, 0x35C0, 0x3480, 0xF441,
	0x3C00, 0xFCC1, 0xFD81, 0x3D40, 0xFF01, 0x3FC0, 0x3E80, 0xFE41,
	0xFA01, 0x3AC0, 0x3B80, 0xFB41, 0x3900, 0xF9C1, 0xF881, 0x3840,
	0x2800, 0xE8C1, 0xE981, 0x2940, 0xEB01, 0x2BC0, 0x2A80, 0xEA41,
	0xEE01, 0x2EC0, 0x2F80, 0xEF41, 0x2D00, 0xEDC1, 0xEC81, 0x2C40,
	0xE401, 0x24C0, 0x2580, 0xE541, 0x2700, 0xE7C1, 0xE681, 0x2640,
	0x2200, 0xE2C1, 0xE381, 0x2340, 0xE101, 0x21C0, 0x2080, 0xE041,
	0xA001, 0x60C0, 0x6180, 0xA141, 0x6300, 0xA3C1, 0xA281, 0x6240,
	0x6600, 0xA6C1, 0xA781, 0x6740, 0xA501, 0x65C0, 0x6480, 0xA441,
	0x6C00, 0xACC1, 0xAD81, 0x6D40, 0xAF01, 0x6FC0, 0x6E80, 0xAE41,
	0xAA01, 0x6AC0, 0x6B80, 0xAB41, 0x6900, 0xA9C1, 0xA881, 0x6840,
	0x7800, 0xB8C1, 0xB981, 0x7940, 0xBB01, 0x7BC0, 0x7A80, 0xBA41,
	0xBE01, 0x7EC0, 0x7F80, 0xBF41, 0x7D00, 0xBDC1, 0xBC81, 0x7C40,
	0xB401, 0x74C0, 0x7580, 0xB541, 0x7700, 0xB7C1, 0xB681, 0x7640,
	0x7200, 0xB2C1, 0xB381, 0x7340, 0xB101, 0x71C0, 0x7080, 0xB041,
	0x5000, 0x90C1, 0x9181, 0x5140, 0x9301, 0x53C0, 0x5280, 0x9241,
	0x9601, 0x56C0, 0x5780, 0x9741, 0x5500, 0x95C1, 0x9481, 0x5440,
	0x9C01, 0x5CC0, 0x5D80, 0x9D41, 0x5F00, 0x9FC1, 0x9E81, 0x5E40,
	0x5A00, 0x9AC1, 0x9B81, 0x5B40, 0x9901, 0x59C0, 0x5880, 0x9841,
	0x8801, 0x48C0, 0x4980, 0x8941, 0x4B00, 0x8BC1, 0x8A81, 0x4A40,
	0x4E00, 0x8EC1, 0x8F81, 0x4F40, 0x8D01, 0x4DC0, 0x4C80, 0x8C41,
	0x4400, 0x84C1, 0x8581, 0x4540, 0x8701, 0x47C0, 0x4680, 0x8641,
	0x8201, 0x42C0, 0x4380, 0x8341, 0x4100, 0x81C1, 0x8081, 0x4040 };

//For this core, other than default alarm pool must be used
volatile alarm_id_t onboard_led_timer = -1;
volatile alarm_id_t spi_registers_read_timer = -1;
struct alarm_pool* p1 = NULL;

/**
 * @brief Calculates CRC for MODBUS message.
 * 
 * @param packet_data Modbus packet in form of raw data
 * @param length Length of buffer (in bytes, excluding CRC)
 * @param response If false, calculated CRC is compared with request crc and result is returned.
 * If true, CRC is calculated and stored at the end of message (return value is true).
 * @return Whether the CRCs match
 */
bool calculate_crc(volatile uint8_t* packet_data, uint16_t length, bool response)
{
	uint8_t xor = 0;
	uint16_t crc = 0xFFFF;

	for (int i = 0; i < length; ++i)
	{
		xor = packet_data[i] ^ crc;
		crc >>= 8;
		crc ^= crc_table[xor];
	}

	if (response){
        //Stores CRC at the end of packet
        put_16bit_into_byte_buffer(packet_data, length, crc);
        return true;
    }
    else {
        return get_16bit_from_byte_buffer(packet_data, length) == crc;
    }
}





//Timers
/**
 * @brief Callback for onboard_led.
 * 
 * Led flashes until command is processed, at least 100ms.
 */
int64_t __time_critical_func(onboard_led_time_callback)(alarm_id_t id, __unused void *user_data){
    
    onboard_led_timer = -1;
    gpio_put(ONBOARD_LED_PIN, 0);
    return 0;
}

/**
 * @brief Timeout for SPI registers transmission
 * 
 * SPI data remain locked until all 5 register groups are read.
 * In case the transmission fails, SPI reading must be unlocked by timer.
 * @param id Not used
 * @param user_data Not used
 * @return 0
 */
int64_t __time_critical_func(spi_lock_data_timeout_callback)(alarm_id_t id, __unused void *user_data){
    
    spi_registers_read_timer = -1;
    spi_lock_data = false;
    //last_read_SPI_register = 0; Not necessary
    return 0;
}






//Response senders
/**
 * @brief Sends response to received Modbus packet
 * 
 * @param packet_data Modbus packet in form of raw data
 * @param length Length of packet (in bytes, excluding CRC)
 */
void send_response(volatile uint8_t* packet_data, uint16_t length){
    calculate_crc(packet_data, length, true);
    uart_write_blocking(MODBUS_UART, (const uint8_t*)packet_data, length + CRC_LEN);
}

/**
 * @brief Sends error response when exception occured
 * 
 * @param packet Modbus packet
 * @param error_code Code of exception
 */
void send_error_response(volatile request_packet* packet, uint8_t error_code){
    uint8_t mb_response[MODBUS_READ_RESPONSE_BASE_LEN + CRC_LEN] = {packet->address, packet->function_code | 0b10000000, error_code};
    send_response(mb_response, MODBUS_READ_RESPONSE_BASE_LEN);
}





//Request handlers
/**
 * @brief Handles Read_Holding_Registers request and sends response
 * 
 * @param packet Request packet
 * @return True if response was sent successfully, false in case of error.
 */
bool read_holding_registers_handler(volatile request_packet* packet){
    if (packet->first_register != HOLDING_REGISTER_ADDRESS || packet->register_count != 1){
        send_error_response(packet, EX_ILLEGAL_ADDRESS);
        return false;
    }

    uint8_t mb_response[MODBUS_READ_RESPONSE_BASE_LEN + 2 + CRC_LEN] = {0};
    mb_response[0] = packet->address;
    mb_response[1] = packet->function_code;
    mb_response[2] = 2; //Number of bytes to follow
    put_16bit_into_byte_buffer(mb_response, MODBUS_READ_RESPONSE_BASE_LEN, endianity_swap_16bit(command_data.raw_data));

    send_response(mb_response, MODBUS_READ_RESPONSE_BASE_LEN + 2);
    return true;
}

/**
 * @brief Handles Read_Input_Registers request and sends response
 * 
 * @param packet Request packet
 * @return True if response was sent successfully, false in case of error.
 */
bool read_input_registers_handler(volatile request_packet* packet){
    //Read input data
    if (packet->first_register == INPUT_REGISTER_ADDRESS){
        last_read_SPI_register = 0;

        if (packet->register_count != 1){
            send_error_response(packet, EX_ILLEGAL_ADDRESS);
            return false;
        }
        
        uint8_t mb_response[MODBUS_READ_RESPONSE_BASE_LEN + 2 + CRC_LEN] = {0};
        mb_response[0] = packet->address;
        mb_response[1] = packet->function_code;
        mb_response[2] = 2; //Number of bytes to follow
        put_16bit_into_byte_buffer(mb_response, MODBUS_READ_RESPONSE_BASE_LEN, endianity_swap_16bit(input_data.raw_data));

        send_response(mb_response, MODBUS_READ_RESPONSE_BASE_LEN + 2);
        unread_input_data = false;
        input_data.button_push_failed = false;
        input_data.button_pushed_manually = false;
        return true;
    }

    //Read SPI data
    else{
        if (packet->register_count != MAX_REGISTER_NUM || 
            (packet->first_register != SPI_INPUT_REGISTER_ADDRESS_G1 && packet->first_register != last_read_SPI_register + 1000)){
            send_error_response(packet, EX_ILLEGAL_ADDRESS);
            return false;
        }
        uint8_t mb_response[MAX_RESPONSE_LENGTH] = {0};
        mb_response[0] = packet->address;
        mb_response[1] = packet->function_code;
        mb_response[2] = MAX_REGISTER_NUM * 2;
        switch (packet->first_register){

            case SPI_INPUT_REGISTER_ADDRESS_G1:
                if (spi_registers_read_timer != -1){
                    alarm_pool_cancel_alarm(p1, spi_registers_read_timer);
                }
                spi_lock_data = true;
                spi_registers_read_timer = alarm_pool_add_alarm_in_us(p1, SPI_REGISTERS_READ_TIMEOUT_US, spi_lock_data_timeout_callback, NULL, false);
                last_read_SPI_register = SPI_INPUT_REGISTER_ADDRESS_G1;

                memcpy((void*)(mb_response + MODBUS_READ_RESPONSE_BASE_LEN), (const void *)spi_parsed_data.register_group1, MAX_REGISTER_NUM * 2);
                send_response(mb_response, MAX_REGISTER_NUM * 2 + MODBUS_READ_RESPONSE_BASE_LEN);
                break;

            case SPI_INPUT_REGISTER_ADDRESS_G2:
                last_read_SPI_register = SPI_INPUT_REGISTER_ADDRESS_G2;
                memcpy((void*)(mb_response + MODBUS_READ_RESPONSE_BASE_LEN), (const void *)spi_parsed_data.register_group2, MAX_REGISTER_NUM * 2);
                send_response(mb_response, MAX_REGISTER_NUM * 2 + MODBUS_READ_RESPONSE_BASE_LEN);
                break;

            case SPI_INPUT_REGISTER_ADDRESS_G3:
                last_read_SPI_register = SPI_INPUT_REGISTER_ADDRESS_G3;
                memcpy((void*)(mb_response + MODBUS_READ_RESPONSE_BASE_LEN), (const void *)spi_parsed_data.register_group3, MAX_REGISTER_NUM * 2);
                send_response(mb_response, MAX_REGISTER_NUM * 2 + MODBUS_READ_RESPONSE_BASE_LEN);
                break;

            case SPI_INPUT_REGISTER_ADDRESS_G4:
                last_read_SPI_register = SPI_INPUT_REGISTER_ADDRESS_G4;
                memcpy((void*)(mb_response + MODBUS_READ_RESPONSE_BASE_LEN), (const void *)spi_parsed_data.register_group4, MAX_REGISTER_NUM * 2);
                send_response(mb_response, MAX_REGISTER_NUM * 2 + MODBUS_READ_RESPONSE_BASE_LEN);
                break;

            case SPI_INPUT_REGISTER_ADDRESS_G5:
                last_read_SPI_register = 0;
                memcpy((void*)(mb_response + MODBUS_READ_RESPONSE_BASE_LEN), (const void *)spi_parsed_data.register_group5, MAX_REGISTER_NUM * 2);
                send_response(mb_response, MAX_REGISTER_NUM * 2 + MODBUS_READ_RESPONSE_BASE_LEN);
                
                if (spi_registers_read_timer != -1){
                    alarm_pool_cancel_alarm(p1, spi_registers_read_timer);
                }
                spi_lock_data = false;
                unread_screen_data = false;
                break;

            default:
                send_error_response(packet, EX_ILLEGAL_ADDRESS);
                return false;
        }
        return true;
    }
}

/**
 * @brief Handles Write_Single_Register request, 
 * waits until commands are parsed and sends response.
 * 
 * @param packet Request packet
 * @return True if response was sent successfully, false in case of error.
 */
bool write_single_register_handler(volatile request_packet* packet){
    if (packet->first_register != HOLDING_REGISTER_ADDRESS){
        send_error_response(packet, EX_ILLEGAL_ADDRESS);
        return false;
    }

    //Waits until command is parsed
    command_data.raw_data = packet->single_register_data;
    command_update_request = true;

    //Wait for main thread to complete actions
    while (command_update_request == true);

    packet->single_register_data = endianity_swap_16bit(command_data.raw_data);
    send_response(packet->raw_data, MODBUS_REQUEST_BASE_LENGTH);
    return true;
}

/**
 * @brief Parses the first part of packet and selects
 * the proper handler according to function code.
 */
void handle_request(request_packet* packet){
    packet->first_register = endianity_swap_16bit(packet->first_register);
    packet->register_count = endianity_swap_16bit(packet->register_count);
    
    switch (packet->function_code){
        case FC_READ_HOLDING_REGISTERS:
            read_holding_registers_handler(packet);
            break;
        case FC_READ_INPUT_REGISTERS:
            read_input_registers_handler(packet);
            break;
        case FC_WRITE_SINGLE_REGISTER:
            write_single_register_handler(packet);
            break;
        default:
            send_error_response(packet, EX_ILLEGAL_FUNCTION);
    }
}



/**
 * @brief Initializes all pins and UART communication
 */
void init_modbus_uart(){
    uart_init(MODBUS_UART, MODBUS_UART_BAUD_RATE);
    gpio_set_function(MODBUS_UART_TX_PIN, GPIO_FUNC_UART);
    gpio_set_function(MODBUS_UART_RX_PIN, GPIO_FUNC_UART);
    uart_set_format(MODBUS_UART, UART_BIT_NUMBER, UART_STOP_BITS, UART_PARITY_EVEN);

    gpio_init(ONBOARD_LED_PIN);
    gpio_set_dir(ONBOARD_LED_PIN, GPIO_OUT);
    //gpio_pull_down(ONBOARD_LED_PIN);
}



void communication_loop(){

    init_modbus_uart();

    p1 = alarm_pool_create_with_unused_hardware_alarm(MAX_TIMERS_NUM);

    int received_bytes = 0;
    request_packet received_packet = {};

    while(true){
        if (uart_is_readable_within_us(MODBUS_UART, MAX_DELAY_US)){
            received_packet.raw_data[received_bytes++] = uart_getc(MODBUS_UART);
            if (received_bytes > MODBUS_REQUEST_BASE_LENGTH + CRC_LEN){
                received_bytes = 0;
            }
        }
        else {
            if (received_bytes == MODBUS_REQUEST_BASE_LENGTH + CRC_LEN &&
                received_packet.address == MY_ADDRESS &&
                calculate_crc(received_packet.raw_data, MODBUS_REQUEST_BASE_LENGTH, false) == true){

                handle_request(&received_packet);
                if (onboard_led_timer != -1){
                    alarm_pool_cancel_alarm(p1, onboard_led_timer);
                }
                gpio_put(ONBOARD_LED_PIN, 1);
                onboard_led_timer = alarm_pool_add_alarm_in_us(p1, ONBOARD_LED_TIME_US, onboard_led_time_callback, NULL, false);
            }
            received_bytes = 0;
        }
       
    }
}