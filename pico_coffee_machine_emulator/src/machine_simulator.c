#ifndef SPI_PROJ_MASTER
#define SPI_PROJ_MASTER

#include "lib/machine_simulator.h"

uint64_t last_reg_trans = 0;
uint64_t last_spi_trans = 0;
uint64_t last_standby_trans = 0;

/**
 * @brief Emulates reading from shift register of coffee machine
 */
void emul_reg(){
    for (int i = 0; i < 9; ++i){
        if (i == 0){
            gpio_put(ld, false);
        }
        //gpio_put(qh, true);
        gpio_put(reg_clk, false);
        sleep_us(2);

        gpio_put(reg_clk, true);
        if (i == 0){
            gpio_put(ld, true);
        }
        gpio_put(qh, !((1 << i) & sw_value));
        sleep_us(3);
    }
}

/**
 * Emulates SPI transmission to display
 */
void emul_spi(){

    //spi_write_blocking(master_spi, clust1, 7);
    
    //spi_write_blocking(master_spi, clust2, 4);
    
    //spi_write_blocking(master_spi, clust3, 2);
    
    //spi_write_blocking(master_spi, clust4, 3);

    spi_write_blocking(master_spi, packet, 1063);

    /*for(int i = 0; i < tx_byte_num; ++i){
        spi_write_blocking(master_spi, &clust5, 1);
    }*/
}

/**
 * Emulates SPI transmission to display
 */
void emul_sample_spi(){
    char buffer1[8] = {1,2,3,4,5,6,7,8};
    char buffer2[8] = {9,10,11,12,13,14,15,16};
    gpio_put(master_cs, 0);
    //cyw43_arch_gpio_put(CYW43_WL_GPIO_LED_PIN, 1);
    spi_write_blocking(master_spi, buffer1, 7);
    //cyw43_arch_gpio_put(CYW43_WL_GPIO_LED_PIN, 0);
    gpio_put(master_cs, 1);

    //sleep_ms(2000);
    
    /*gpio_put(master_cs, 0);
    spi_write_blocking(master_spi, buffer2, 7);
    gpio_put(master_cs, 1);

    sleep_us(2);
    gpio_put(master_cs, 0);
    spi_write_blocking(master_spi, &clust5, 1);
    gpio_put(master_cs, 1);

    sleep_us(2);
    gpio_put(master_cs, 0);
    spi_write_blocking(master_spi, &clust5, 1);
    gpio_put(master_cs, 1);*/

}

/**
 * @brief Emulates the behaviour of standby LED on coffee machine
 */
void emul_standby(){
    current_standby_delay_num = (current_standby_delay_num + 1) % 2;
    //cyw43_arch_gpio_put(CYW43_WL_GPIO_LED_PIN, current_standby_delay_num);
    gpio_put(standby_led, current_standby_delay_num);
}

/**
 * @brief Main loop for signal emulation
 */
void main(){

    gpio_init(qh);
    gpio_set_dir(qh, GPIO_OUT);
    gpio_pull_up(qh);

    gpio_init(ld);
    gpio_set_dir(ld, GPIO_OUT);
    gpio_pull_up(ld);

    gpio_init(reg_clk);
    gpio_set_dir(reg_clk, GPIO_OUT);
    gpio_pull_up(reg_clk);

    
    gpio_init(standby_led);
    gpio_set_dir(standby_led, GPIO_OUT);
    gpio_pull_up(standby_led);
    
    spi_init(master_spi, 2500000);
    spi_set_format( master_spi,   // SPI instance
                    8,      // Number of bits per transfer
                    1,      // Polarity (CPOL)
                    1,      // Phase (CPHA)
                    SPI_MSB_FIRST);
    gpio_set_function(master_mosi, GPIO_FUNC_SPI);
    gpio_set_function(master_cs, GPIO_FUNC_SPI);
    gpio_set_function(master_spi_clk, GPIO_FUNC_SPI);

    //cyw43_arch_init();
    //cyw43_arch_gpio_put(CYW43_WL_GPIO_LED_PIN, 0);

    sleep_ms(1000);

    multicore_launch_core1(modbus_main);

    while(1){
        //SPI transmission
        if (to_us_since_boot(get_absolute_time()) - last_spi_trans >= SPI_DELAY){
            emul_spi();
            last_spi_trans = to_us_since_boot(get_absolute_time());
        }
        //Reg reading
        else if (to_us_since_boot(get_absolute_time()) - last_reg_trans >= SWITCH_DELAY){
            emul_reg();
            last_reg_trans = to_us_since_boot(get_absolute_time());
        }
        //LED blinking
        else if (to_us_since_boot(get_absolute_time()) - last_standby_trans >= standby_delay[current_standby_delay_num]){
            emul_standby();
            last_standby_trans = to_us_since_boot(get_absolute_time());
        }
    }
}

#endif