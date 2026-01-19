# MACHINE CONTROLLER

## 1. Overview
This script handles the controll of coffee machine. 
Machine itself is controlled by microcontroller RPI Pico.
This microcontroller is able to read all output signals
from the machine, including the image displayed on its
screen. It is also capable of pushing the buttons on
the machine's control panel. Microcontroller itself
does not implement any high-level logic, it only executes
received commands. All controll, including the
state machine, is implemented in this script. The script
communicates with Pico, reads and parses all output data
and sends commands for button push. It also maintains database with all supported states of the machine, determined by image displayed on machine's screen.

## 2. Supported procedures
The new procedure can be started only if the machine is not busy
by executing the previous one. This also appplies for **GetState**, because during procedure execution, the state is
being retrieved continuously.
### 1.) GetState
Retrieves current state of the coffee machine. State of 
the machine is public property which can be read, but cannot be modified externally. It also supports *ToString()* method to obtain current state in string form. 
Possible states:
| State | Description |
|-|-|
| Off | Machine is off, but plugged in |
| AC_disconnected | Machine is not plugged in |
| Switching | Machine is turning on or off |
| Running | Machine is on and is not in error state (current state is unknown)|
| Idle | Machine is at home screen |
| MachineSetting | Machine is adjusting its parameters |
| DrinkMaking | Machine is preparing a drink |
| Resetting | Machine is performing reset after exceptional state |
| Error | Machine is on and in error state |
| FatalError | Fatal error occured |
### 2.) Power On
Turns on the machine. Can be executed only if the machine is plugged in. If the machine 
is already on, this procedure does nothing.
### 3.) Power Off
Turns off the machine.
If the machine is already off, this procedure does nothing.

#### Following procedures can be executed only if the machine is on and in idle state. Also, if the machine runs out of coffee, no drink can be produced, except of hot water.
### 4.) Make Espresso
Commands the machine to make espresso.
### 5.) Make Coffee
Commands the machine to make coffee.
### 6.) Make Americano
Commands the machine to make americano.
### 7.) Make Hot Water
Commands the machine to fill the cup with hot water.
### 8.) Set Aroma
Sets the aroma level of the drink.
Possible levels: 1, 2, 3, 4, 5 and 0 for ground coffee option.
### 9.) Set Temperature
Sets the temperature level of the drink.
Possible levels: 0 = low, 1 = medium, 2 = high.

The last 2 procedures require additional parameter, which specifies the value to be set.

## 3. Communication
|||
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
Create instance of *MachineController*, provided by the
paths to database and log file, then run 
*StartControlLoop* method. This method will start the
main control loop in separate thread. The loop will
take care of all user commands and communication
with the Pico. The only public method is *SetRequest(procedure)*,
which will execute the requested procedure and return *True*, 
if the prerequisities have been met. Otherwise, calling this method
will return *False* and procedure will not start. Some 
procedurtes may require additional parameter, which may be 
passed as second argument of the method. 

Microcontroller RPI Pico is constantly monitoring all output
signals from the machine, including the image on its screen.
If any of these signals change, the RPI4 is notified via
dedicated pin. The main loop will then read all output data
from the Pico and update the state of the machine,
so polling is not necessary. 

If the Pico does not respond, the 
*PicoErrorException* is thrown. Microcontroller is set 
to automatically start reading the display data and 
handling the buttons on controll panel. If any of those
operations fail, this exception will be thrown, too. The main loop will
then try to reset microcontroller via dedicated pin. If the Pico 
does not communicates even after the reset, the main loop will 
go to fatal error.

Normal execution of the procedure may get interrupted. 
That may be caused by unknown state detected on the machine, 
or by its error state. In this case, the 
procedure execution will be aborted and the serving method 
will throw *UnknownStateException*. Controller will then 
diagnose the state of machine. If the machine is 
unplugged or turned off, it will change the state 
accordingly. If the machine is in error state, it will 
determine the error and reports it via *ErrorState* 
property. If the initialization process is running, 
controller will wait until it ends. Similarly, if some 
procedure is running (which is usually signalized by progress 
bar displayed on the screen), controller will wait until it's done. Afterwards, the controller will try to reset the
machine and bring it back to idle state. This is done by
pushing the back button repeatedly, until the home screen
is detected. If the machine cannot be brought back to idle
state, the controller will go to fatal error.

If the machine runs out of coffee, it is signalized by
specific image on the display instead of home screen. In
this case, the controller will set the *OutOfCoffee*
property to *True*.

Pico is also able to detect if the user has
pushed any button on the machine's control panel manually. 
If that happens, the current procedure will be aborted and
*ButtonPushedManuallyException* will be thrown. The 
controller will then wait until the user finishes his
interaction with the machine by waiting minimal amount of time
with no button pushed. During this time, the machine
controller will prevent the user from launching any
procedure which requires milk frother, to avoid
hot steam spillage. Controller will then try to bring the 
machine back to idle state (as in previous case).

The main controll loop thread can be terminated by
calling *StopControlLoop* method.

All errors and important events are logged to the log file,
specified during *MachineController* creation. These log 
files contain timestamp of the event, exception which was 
thrown, method where the exception was thrown and a
corresponding error message. It may also contain diagnostic
data, such as values of registers read from Pico.

### Database
The database (stored in JSON file) contains
all supported states of the machine, determined by
image displayed on its screen. These images are 
stored as ASCII picture, where each character
represents one pixel of display. Character '0'
means that pixel is on, '.' means pixel is off.
Each state has its unique ID. There are three types
of states:
- **Standard records** - used to determine
the non-error state of machine. These states are compared to
machine display 1-to-1, which means that every single pixel 
state must match.
- **Mask records** - used to determine
the non-error state of machine. These states are compared to
machine display with mask, which means that all pixels 
which are turned on in mask record must match, but
pixels which are turned off in mask record are ignored
(can be either on or off on display).
This is useful for states which contain dynamic 
elements, such as progress bar.
- **Error records** - used to determine
the error state of machine. These states must always be compared
to machine display 1-to-1 (no masks are allowed).



## 5. Testing loop
In *Program.cs* there is a simple testing loop, which can be used
to verify the functionality of the controller. It will read the 
command from console and execute corresponding procedure. The
list of commands:
- **d** - Get state
- **1** - Power on
- **0** - Power off
- **e** - Make espresso
- **c** - Make coffee
- **a** - Make americano
- **h** - Make hot water
- **t** - Set temperature 
- **i** - Set aroma
- **s** - No command, just prints current state

Commands **t** and **i** require additional parameter, which
specifies the value to be set. Value shall be written right
after the command, f.e. **t2** will set the temperature
to high.

The testing loop will also display the result of procedure call 
(True/False), current state of machine and error state 
description (in case of error).

In case of exception, the exception messages will be printed to 
console before the command is executed. You can use **s** command to
obtain the latest exception messages, if any.




