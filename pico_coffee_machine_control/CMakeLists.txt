cmake_minimum_required(VERSION 3.12)
include(pico_sdk_import.cmake)
project(machine_controller)

pico_sdk_init() 

add_executable(machine_controller
                src/machine_controller.c
                src/modbus_server.c)

target_include_directories(machine_controller PUBLIC
                            ${CMAKE_CURRENT_LIST_DIR})                  

pico_add_extra_outputs(machine_controller)
target_link_libraries(machine_controller 
                        pico_stdlib
                        hardware_spi
                        pico_multicore
                        hardware_pio
                        hardware_dma
                        hardware_irq)

pico_generate_pio_header(machine_controller ${CMAKE_CURRENT_LIST_DIR}/pio/spi_recv.pio)
pico_generate_pio_header(machine_controller ${CMAKE_CURRENT_LIST_DIR}/pio/reg_handler.pio)

#pico_enable_stdio_usb(machine_controller 1)
#pico_enable_stdio_uart(machine_controller 0)