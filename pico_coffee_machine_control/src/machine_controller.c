#ifndef SPI_PROJ
#define SPI_PROJ

#include "lib/machine_controller.h"
#include "spi_recv.pio.h"
#include "reg_handler.pio.h"

#define DUMMY_NUM 32 //Used as default non-negative value wherever 0 is valid, but 32 is invalid
//NOTE: Sleep in interrupt handler will brick the device

//SPI variables
//SPI sm data
volatile bool spi_sm_started = false;
uint spi_sm_offset = 0;

//SPI DMA variables
int dma_channel_spi_read = DUMMY_NUM;
dma_channel_config dma_config_spi_read;

//SPI alarms
volatile alarm_id_t spi_sync_timer = -1;
volatile alarm_id_t spi_recv_watchdog = -1;

//SPI data (+ 1 value for alignment and 1 for terminal zero)
volatile uint32_t spi_rx_buffer_dma[SPI_BYTE_NUM + 2] = {0};
volatile uint32_t spi_rx_buffer[SPI_BYTE_NUM + 2] = {0};
bool spi_new_data = false;





//REG variables
//Register sm variables
volatile bool reg_sm_started = false;
uint reg_sm_offset = 0;

//Register DMA variables
int dma_channel_reg_read = DUMMY_NUM;
dma_channel_config dma_config_reg_read; 
int dma_channel_reg_write = DUMMY_NUM;
dma_channel_config dma_config_reg_write;

//Register watchdog
volatile alarm_id_t reg_handler_watchdog = -1;

//Register data
volatile uint32_t register_data_dma = 0;
volatile uint32_t register_command_dma = 0b00000000;
volatile uint32_t button_input_command_mismatch_num = 0;




 

//Other variables
volatile alarm_id_t push_button_timer = -1;
volatile alarm_id_t standby_detection_alarm = -1;
//volatile alarm_id_t pio_machines_start_delay_timer = -1;
event_register last_input_data = {.raw_data = 0};




//PIO start methods
/**
 * @brief Starts SPI receiving by enabling interrupt on SPI CS pin
 */
void __time_critical_func(start_spi_receiver)(){
    gpio_set_irq_enabled(SPI_CS_PIN, GPIO_IRQ_EDGE_FALL, true);
 }

 /**
  * @brief Starts register handling (PIO machine)
  */
 void __time_critical_func(start_reg_handler)(){
    pio_sm_set_enabled(REG_PIO, REG_SM, true);
    dma_channel_start(dma_channel_reg_read);
    dma_channel_start(dma_channel_reg_write);
 }

//PIO reset methods
/**
 * @brief Resets PIO machine for SPI receiving
 */
void __time_critical_func(reset_spi_receiver)(){
    gpio_set_irq_enabled(SPI_CS_PIN, GPIO_IRQ_EDGE_FALL, false);
    if (spi_sync_timer != -1){
        cancel_alarm(spi_sync_timer);
        spi_sync_timer = -1;
    }

    pio_sm_set_enabled(SPI_PIO, SPI_SM, false);
    pio_sm_restart(SPI_PIO, SPI_SM);
    pio_sm_clkdiv_restart(SPI_PIO, SPI_SM);
    pio_sm_exec(SPI_PIO, SPI_SM, pio_encode_jmp(spi_sm_offset));

    dma_channel_set_irq0_enabled(dma_channel_spi_read, false);
    dma_channel_abort(dma_channel_spi_read);
    dma_channel_acknowledge_irq0(dma_channel_spi_read);
    dma_channel_set_irq0_enabled(dma_channel_spi_read, true);
    dma_channel_set_write_addr(dma_channel_spi_read, spi_rx_buffer_dma, false);

    //memset((void*)spi_rx_buffer_dma, 0, SPI_BYTE_NUM);
    //memset((void*)spi_rx_buffer, 0, SPI_BYTE_NUM);
    //memset((void*)spi_parsed_data.spi_raw_bytes, 0, SPI_BYTE_NUM);
}

/**
 * @brief Resets PIO machine for register handling
 */
void __time_critical_func(reset_reg_handler)(){
    pio_sm_set_enabled(REG_PIO, REG_SM, false);
    pio_sm_restart(REG_PIO, REG_SM);
    pio_sm_clkdiv_restart(REG_PIO, REG_SM);
    pio_sm_exec(REG_PIO, REG_SM, pio_encode_jmp(reg_sm_offset));

    dma_channel_set_irq0_enabled(dma_channel_reg_read, false);
    dma_channel_abort(dma_channel_reg_read);
    dma_channel_acknowledge_irq0(dma_channel_reg_read);
    dma_channel_set_irq0_enabled(dma_channel_reg_read, true);

    dma_channel_set_irq0_enabled(dma_channel_reg_write, false);
    dma_channel_abort(dma_channel_reg_write);
    dma_channel_acknowledge_irq0(dma_channel_reg_write);
    dma_channel_set_irq0_enabled(dma_channel_reg_write, true);

    register_data_dma = 0;
    register_command_dma = 0;
    input_data.buttons = 0;
}





//Alarms and timers callbacks

/**
 * @brief Callback for standby mode detection. 
 * 
 * Standby is signalized by LED blinking every STANDBY_LED_TIMEOUS_US. 
 * If the timer finishes, the coffee machine is no longer in standby mode.
 * @param id Not used
 * @param user_data Not used
 * @return 0
 */
int64_t __time_critical_func(standby_not_detected_callback)(alarm_id_t id, __unused void *user_data){
    input_data.standby_on = false;
    standby_detection_alarm = -1;

    return 0;
}

/**
 * @brief Callback for spi_recv state machine watchdog.
 * 
 * When timer finishes, spi_recv state machine has stopped sending data and should be reset.
 * @param id Not used
 * @param user_data Not used
 * @return 0
 */
int64_t __time_critical_func(spi_recv_watchdog_callback)(alarm_id_t id, __unused void *user_data){
    input_data.spi_recv_running = false;
    spi_recv_watchdog = -1;
    return 0;
}

/**
 * @brief Callback for reg_handler state machine watchdog.
 * 
 * When timer finishes, reg_handler state machine has stopped sending data and should be reset.
 * @param id Not used
 * @param user_data Not used
 * @return 0
 */
int64_t __time_critical_func(reg_handler_watchdog_callback)(alarm_id_t id, __unused void *user_data){
    input_data.reg_handler_running = false;
    reg_handler_watchdog = -1;

    register_data_dma = 0;
    input_data.buttons = 0;
    command_data.buttons = 0;
    return 0;
}

/**
 * @brief Callback for SPI timer. 
 * 
 * This timer starts when the falling edge on CS pin is detected and lasts for 
 * SPI_TRANSMISSION_TIME_US. When done, it enables state machine receiving SPI traffic.
 * 
 * This mechanism is implemented to ensure proper synchronization with coffee machine.
 * Even if SPI CLK signal gets interrupted, PIO machine will resync with following 
 * SPI transmission. 
 * @param id Not used
 * @param user_data Not used
 * @return 0
 */
int64_t __time_critical_func(spi_sync_timer_callback)(alarm_id_t id, __unused void *user_data){
    spi_sync_timer = -1;
    pio_sm_set_enabled(SPI_PIO, SPI_SM, true);
    dma_channel_start(dma_channel_spi_read);
    return 0;
}

/**
 * @brief Callback for push button command timer.
 * 
 * When timer finishes, all currently pushed buttons will be released.
 * @param id Not used
 * @param user_data Not used
 * @return 0
 */
int64_t __time_critical_func(push_button_timer_callback)(alarm_id_t id, __unused void *user_data){
    //If power button should have been pushed but is not
    if (command_data.power_button_push == true && input_data.power_button_pushed == false){
        input_data.button_push_failed = true;  
    }

    command_data.buttons = 0;
    command_data.power_button_push = false;

    gpio_put(POWER_BUTTON_CONTROL, false);
    push_button_timer = -1;
    return 0;
}





//Interrupt handlers
/**
 * @brief Global interrupt handler for GPIO pins
 * @section SPI_CS: Detects whether SPI CS pin goes low, which signalizes the beginning of 
 * SPI transaction. This interrupt is fired only once before disabled. It is used to synchronize
 * SPI receiver with coffee machine (see spi_sync_timer_callback()).
 * @section POWER_BUTTON: Detects whether Main switch has been pushed.
 * @section STANDBY_ON: Detects whether the machine is in standby mode.
 * 
 * @param gpio Number of pin
 * @param event_mask Type of event which caused interrupt
 */
void __time_critical_func(gpio_irq_handler)(uint gpio, uint32_t event_mask){

    //Handler for SPI CS pin
    if (gpio == SPI_CS_PIN && event_mask == GPIO_IRQ_EDGE_FALL){

        gpio_set_irq_enabled(SPI_CS_PIN, GPIO_IRQ_EDGE_FALL, false); //Disable interrupts, for data are comming
        spi_sync_timer = add_alarm_in_us(SPI_TRANSMISSION_TIME_US, spi_sync_timer_callback, NULL, false);
    }

    //Handler for standby mode detection
    if (gpio == STANDBY_LED_PIN && event_mask == GPIO_IRQ_EDGE_RISE){
        if (standby_detection_alarm != -1){
            cancel_alarm(standby_detection_alarm);
        }
        if (input_data.standby_on == false){
            input_data.standby_on = true;  
        }
        standby_detection_alarm = add_alarm_in_us(STANDBY_LED_TIMEOUT_US, standby_not_detected_callback, NULL, false);
    }

    //Handler for power button push
    if (gpio == POWER_BUTTON_PIN && event_mask == GPIO_IRQ_EDGE_FALL){
        input_data.power_button_pushed = true;

        //If the button was pushed manually
        if (command_data.power_button_push == false){
            input_data.button_pushed_manually = true;
        }
    }
    else if (gpio == POWER_BUTTON_PIN && event_mask == GPIO_IRQ_EDGE_RISE){
        input_data.power_button_pushed = false;

    }
}

/**
 * @brief Global interrupt handler for DMA
 * @section dma_channel_spi_read: Fired when SPI transaction finished (SPI_BYTE_NUM bytes has been read).
 * @section dma_channel_reg_read: Fired when reading from button shift register finished.
 * @section dma_channel_reg_write: Fired when button push was executed. 
 */
void __time_critical_func(dma_irq0_handler)() {
    if (dma_hw->ints0 & (1u << dma_channel_spi_read)){
        dma_hw->ints0 = 1u << dma_channel_spi_read;
        
        //Do not update if old data has not been parsed yet or old ones are being transmitted
        if (spi_new_data == false){
            if (memcmp((void*)spi_rx_buffer, (const void*)spi_rx_buffer_dma, sizeof(uint32_t) * SPI_BYTE_NUM) != 0){
                memcpy((void*)spi_rx_buffer, (const void*)spi_rx_buffer_dma, sizeof(uint32_t) * SPI_BYTE_NUM);
                spi_new_data = true;
            }   
        }
        dma_channel_set_write_addr(dma_channel_spi_read, spi_rx_buffer_dma, true);

        if (spi_recv_watchdog != -1){
            cancel_alarm(spi_recv_watchdog);
        }
        input_data.spi_recv_running = true;
        spi_recv_watchdog = add_alarm_in_us(SPI_RECV_WATCHDOG_TIMEOUT_US, spi_recv_watchdog_callback, NULL, false);
        
        reset_spi_receiver();
        start_spi_receiver();
    }

    if (dma_hw->ints0 & (1u << dma_channel_reg_read)){
        // Clear the interrupt request.
        dma_hw->ints0 = 1u << dma_channel_reg_read;

        input_data.buttons = ((~register_data_dma) >> 24);

        //To keep error flags
        input_data.button_pushed_manually = last_input_data.button_pushed_manually;
        input_data.button_push_failed = last_input_data.button_push_failed;

        //Detects if the requested buttons have been pushed or button was pushed manually
        if (input_data.buttons != command_data.buttons){
            //Few mismatches can be tolerated
            if (button_input_command_mismatch_num < REG_BUTTON_MISMATCH_LIMIT){
                button_input_command_mismatch_num++;
            }
            else {
                //Button was not pushed
                if (input_data.buttons == 0 && command_data.buttons != 0){
                    input_data.button_push_failed = true;
                }
                //Another button was pushed manually
                else {
                    input_data.button_pushed_manually = true;
                }  
            }
        }
        else {
            button_input_command_mismatch_num = 0;
        }
        
        if (reg_handler_watchdog != -1){
            cancel_alarm(reg_handler_watchdog);
        }
        input_data.reg_handler_running = true;
        reg_handler_watchdog = add_alarm_in_us(REG_HANDLER_WATCHDOG_TIMEOUT_US, reg_handler_watchdog_callback, NULL, false);
        
        dma_channel_start(dma_channel_reg_read);
    }

    if (dma_hw->ints0 & (1u << dma_channel_reg_write)){
        // Clear the interrupt request.
        dma_hw->ints0 = 1u << dma_channel_reg_write;
        register_command_dma = command_data.buttons;
        dma_channel_start(dma_channel_reg_write);
    }
}





//Status detection
/**
 * @brief Detects whether the machine is powered on and machine error status
 * @section POWER_5V: Detects whether the machine is powered on.
 * @section STANDBY_LED: Detects whether the machine is in standby mode. The signal on
 * this pin must be detected before STANDBY_LED_TIMEOUS_US time passes. If not, the 
 * machine is no longer in standby mode.
 * @section SCREEN_RED: Detects whether the screen goes red, which signalizes error state.
 * @section SCREEN_WHITE: Detects whether the screen goes white, which signalizes that coffee
 * machine is in operational state.
 */
void detect_status(){

    //Handler for +5V Power detection
    if (gpio_get(POWER_5V_PIN) == 1 && input_data.powered_on == false){
        input_data.powered_on = true;  
    }
    if (gpio_get(POWER_5V_PIN) == 0 && input_data.powered_on == true){
        input_data.powered_on = false;  
    }
    

    //Handler for Red Screen detection
    if (gpio_get(SCREEN_RED_PIN) == 1 && input_data.red_screen == false){
        input_data.red_screen = true; 
    }
    if (gpio_get(SCREEN_RED_PIN) == 0 && input_data.red_screen == true){
        input_data.red_screen = false; 
    }


    //Handler for Green Screen detection
    if (gpio_get(SCREEN_WHITE_PIN) == 1 && input_data.white_screen == false){
        input_data.white_screen = true;
        
    }
    if (gpio_get(SCREEN_WHITE_PIN) == 0 && input_data.white_screen == true){
        (SCREEN_WHITE_PIN, GPIO_IRQ_EDGE_FALL);
        input_data.white_screen = false; 
    }


    //If screen is lighting, start PIO machines
    if ((gpio_get(SCREEN_RED_PIN) == 1 || gpio_get(SCREEN_WHITE_PIN) == 1)){
        if (spi_sm_started == false){
            start_spi_receiver();
            spi_sm_started = true;
        }
        if (reg_sm_started == false){
            start_reg_handler();
            reg_sm_started = true;
        }
    }

    //If screen does not light, stop PIO machines
    if ((gpio_get(SCREEN_RED_PIN) == 0 && gpio_get(SCREEN_WHITE_PIN) == 0)){
        if (spi_sm_started == true){
            reset_spi_receiver();
            spi_sm_started = false;
        }
        if (reg_sm_started == true){
            reset_reg_handler();
            reg_sm_started = false;
        }
    }

}





/**
 * @brief Initialization of used pins, PIO state machines,
 * DMA channels, interrupts and interrupt handlers.
 */
void controller_init(){

    gpio_init(SPI_CS_PIN);
    gpio_set_dir(SPI_CS_PIN, GPIO_IN);

    gpio_init(POWER_5V_PIN);
    gpio_set_dir(POWER_5V_PIN, GPIO_IN);
    //gpio_pull_down(POWER_5V_PIN);

    gpio_init(STANDBY_LED_PIN);
    gpio_set_dir(STANDBY_LED_PIN, GPIO_IN);
    //gpio_pull_down(STANDBY_LED_PIN);

    gpio_init(POWER_BUTTON_PIN);
    gpio_set_dir(POWER_BUTTON_PIN, GPIO_IN);
    //gpio_pull_up(POWER_BUTTON_PIN);

    gpio_init(SCREEN_RED_PIN);
    gpio_set_dir(SCREEN_RED_PIN, GPIO_IN);
    //gpio_pull_down(SCREEN_RED_PIN);

    gpio_init(SCREEN_WHITE_PIN);
    gpio_set_dir(SCREEN_WHITE_PIN, GPIO_IN);
    //gpio_pull_down(SCREEN_WHITE_PIN);

    gpio_init(POWER_BUTTON_CONTROL);
    gpio_set_dir(POWER_BUTTON_CONTROL, GPIO_OUT);
    //gpio_pull_down(POWER_BUTTON_CONTROL);

    gpio_init(NEW_DATA_SIGNAL);
    gpio_set_dir(NEW_DATA_SIGNAL, GPIO_OUT);
    //gpio_pull_down(NEW_DATA_SIGNAL);


    // Set up a PIO state machine to read spi
    spi_sm_offset = pio_add_program(pio0, &spi_recv_program);
    spi_recv_program_init(SPI_PIO, SPI_SM, spi_sm_offset, SPI_CLKDIV);

    // Set up a PIO state machine to read and write to register
    reg_sm_offset = pio_add_program(pio0, &reg_handler_program);
    reg_handler_program_init(REG_PIO, REG_SM, reg_sm_offset, REG_CLKDIV);

    // Configure a channel to read the same word (32 bits) repeatedly from PIO0
    // SM0's RX FIFO, paced by the data request signal from that peripheral.
    dma_channel_spi_read = dma_claim_unused_channel(true);
    dma_config_spi_read = dma_channel_get_default_config(dma_channel_spi_read);
    channel_config_set_transfer_data_size(&dma_config_spi_read, DMA_SIZE_32);
    channel_config_set_read_increment(&dma_config_spi_read, false);
    channel_config_set_write_increment(&dma_config_spi_read, true);
    channel_config_set_dreq(&dma_config_spi_read, DREQ_PIO0_RX0);
    dma_channel_configure(
        dma_channel_spi_read,
        &dma_config_spi_read,
        spi_rx_buffer_dma, 
        &SPI_PIO->rxf[SPI_SM],          
        SPI_BYTE_NUM, 
        false             // Don't start yet
    );
    
    //Configures dma channel to read register data
    dma_channel_reg_read = dma_claim_unused_channel(true);
    dma_config_reg_read = dma_channel_get_default_config(dma_channel_reg_read);
    channel_config_set_transfer_data_size(&dma_config_reg_read, DMA_SIZE_32);
    channel_config_set_read_increment(&dma_config_reg_read, false);
    channel_config_set_write_increment(&dma_config_reg_read, false);
    channel_config_set_dreq(&dma_config_reg_read, DREQ_PIO0_RX1);

    dma_channel_configure(
        dma_channel_reg_read,
        &dma_config_reg_read,
        &register_data_dma, 
        &REG_PIO->rxf[REG_SM],          
        NUMBER_OF_REG_TRANSACTIONS, 
        false             
    );


    //Configures dma channel to write register data
    dma_channel_reg_write = dma_claim_unused_channel(true);
    dma_config_reg_write = dma_channel_get_default_config(dma_channel_reg_write);
    channel_config_set_transfer_data_size(&dma_config_reg_write, DMA_SIZE_32);
    channel_config_set_read_increment(&dma_config_reg_write, false);
    channel_config_set_write_increment(&dma_config_reg_write, false);
    channel_config_set_dreq(&dma_config_reg_write, DREQ_PIO0_TX1);

    dma_channel_configure(
        dma_channel_reg_write,
        &dma_config_reg_write,
        &REG_PIO->txf[REG_SM], 
        &register_command_dma,          
        NUMBER_OF_REG_TRANSACTIONS, 
        false             
    );


    // Tell the DMA to raise IRQ line 0 when the channel finishes a block
    dma_channel_set_irq0_enabled(dma_channel_spi_read, true);
    dma_channel_set_irq0_enabled(dma_channel_reg_read, true);
    dma_channel_set_irq0_enabled(dma_channel_reg_write, true);
    // Configure the processor to run dma_handler() when DMA IRQ 0 is asserted
    irq_set_exclusive_handler(DMA_IRQ_0, dma_irq0_handler);
    irq_set_enabled(DMA_IRQ_0, true);


    //Configures interrupts from pins
    gpio_set_irq_callback(gpio_irq_handler);
    irq_set_enabled(IO_IRQ_BANK0, true);

    gpio_set_irq_enabled(STANDBY_LED_PIN, GPIO_IRQ_EDGE_RISE, true);
    gpio_set_irq_enabled(POWER_BUTTON_PIN, GPIO_IRQ_EDGE_RISE | GPIO_IRQ_EDGE_FALL, true);

}





//Data parsers 
/**
 * @brief Parses received SPI data into single consecutive stream for modbus registers.
 */
void parse_spi_data(){
    int iterator_8bit = 0;
    for (int i = 0; i < (SPI_BYTE_NUM + 1) / 2; ++i){
        uint16_t val = spi_rx_buffer[iterator_8bit++] | (spi_rx_buffer[iterator_8bit++] << 8);
        spi_parsed_data.spi_raw_buffer[i] = endianity_swap_16bit(val);
    }
}

/**
 * @brief Parses and executes received commands.
 */
void parse_commands(){
    //Unable to push when reg_handler does not run
    if (input_data.reg_handler_running == false && command_data.buttons > 0){
        input_data.button_push_failed = true;
    }

    //Button push timer reset
    if (push_button_timer != -1){
        cancel_alarm(push_button_timer);
    }
    //Button pushing
    gpio_put(POWER_BUTTON_CONTROL, command_data.power_button_push);
    //Sets alarm to release pushed buttons
    if ((command_data.buttons > 0 || command_data.power_button_push == true) && command_data.button_clear_disabled == false){
        push_button_timer = add_alarm_in_us(PUSH_BUTTON_DURATION, push_button_timer_callback, NULL, false);
    }

}




/**
 * @brief Main controller loop
 */
int main(){
    stdio_init_all();
    multicore_launch_core1(communication_loop);

    controller_init();

    while(true){ 
        if (last_input_data.raw_data != input_data.raw_data){
            last_input_data.raw_data = input_data.raw_data;
            unread_input_data = true;
        }
        if (spi_new_data == true  && spi_lock_data == false){
            parse_spi_data();
            spi_new_data = false;
            unread_screen_data = true;
        }
        if (command_update_request == true){
            parse_commands();            
            command_update_request = false;
        }

        detect_status();

        gpio_put(NEW_DATA_SIGNAL, unread_input_data | unread_screen_data);
        sleep_us(10);

    }
}



#endif