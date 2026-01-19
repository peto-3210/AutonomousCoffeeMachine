cmake_minimum_required(VERSION 3.12)
include(pico_sdk_import.cmake)
project(machine_emulator)
set(PICO_BOARD pico_w)

pico_sdk_init() 

add_executable(machine_emulator
                src/machine_simulator.c
                src/modbus_master.c)

target_include_directories(machine_emulator PUBLIC
                            ${CMAKE_CURRENT_LIST_DIR})                  

pico_add_extra_outputs(machine_emulator)
target_link_libraries(machine_emulator 
                        pico_stdlib
                        hardware_spi
                        pico_multicore)
                        #pico_cyw43_arch_none)

#pico_enable_stdio_usb(spi_project 1)
#pico_enable_stdio_uart(spi_project 0)