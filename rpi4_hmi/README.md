# HMI

## 1. Overview
This is the HMI which controls cup fetcher and coffee 
machine. HMI consists of 7 control buttons. Under the 
buttons, there are 4 images of coffee cups, which 
represent 4 drink in the drink storage. As of now, the 
lower panel with the cup images is set to display current request and phase of its execution.

There is a separate thread which handles the requests
from HMI and controlls the cup fetcher and coffee 
machine accordingly. The thread is started when the HMI is launched. In case of coffee machine, this thread 
send the commands via *SetRequest* method and reads the
state of the machine. In case of cup fetcher, the thread
sends the commands via corresponding methods and then
continuosly reads its state (it acts as a main loop for 
cup fetcher).
The HMI also continously reads the state of both 
components and updates the execution phase
accordingly.

## 2. Supported procedures
The new procedure can be started only if the machine is not busy by executing the previous one. 
On the left side, there are 3 buttons, which are used to
select the drink to be prepared. List of available 
drinks:
#### 1.) Espresso
#### 2.) Coffee
#### 3.) Americano
In the middle, there is one large button used to swith the machine on.

On the right side, there are 3 buttons, which are used
for the configuration of assembly. These buttons are:
#### 1.) Set Temperature
#### 2.) Set Aroma
#### 3.) Calibrate
As of now, operations **Set Temperature** and **Set Aroma** buttons have fixed parameters defined in source code.

Before the drink making process starts, calibration 
must always be performed. This has to be done only once,
after the HMI is powered on.

## 3. Communication
|For NHduino||
|-|-|
| Protocol | ModbusRTU |
| Baud rate | 115200 | 
| Device ID | 1 |
| Default connection (on RPI4) | ttyAMA3 | 
| Tx pin number (on RPI4) | 7 (label GPIO4) |
| Rx pin number (on RPI4) | 29 (label GPIO5)| 

|For RPI Pico||
|-|-|
| Protocol | ModbusRTU |
| Baud rate | 115200 | 
| Device ID | 2 |
| Default connection (on RPI4) | ttyAMA4 | 
| Tx pin number (on RPI4) | 24 (label GPIO8) |
| Rx pin number (on RPI4) | 21 (label GPIO9) | 
| Reset pin number (on RPI4) | 25 (label GPIO7) |
| New data pin number (on RPI4) | 23 (label GPIO11) |

## 4. How to use
First, clone the whole repository, including
*rpi4_cup_fetcher controller* and 
*rpi4_coffee_machine controller*.
Then, launch the HMI using commands:
```
cd ~/programs/autonomous-coffe-machine/rpi4_hmi
dotnet run
```
HMI screen should appear. You can then click the buttons
to control the assembly. 
In case of any error, there will be a popup message
with the text "Error" displayed. 



