# CUP FETCHER

## 1. Overview
This script handles the controll of cup fetching mechanism. Cup 
fetching mechanism is the assembly of devices (stepper and DC 
motors, solenoids) used to move the cup from cup stack to the 
coffee machine workspace and to the storage of drinks.

## 2. Supported procedures
### 1.) Calibration
Calibrates all steppers
### 2.) Home
Sends all steppers to home position
### 3.) Fetch cup
Takes the cup out of cupt stack and relocates it under the
coffee machine.
### 4.) Export coffee 
Takes the cup from the coffee machine and moves it to storage
of drinks.

## 3. Communication
|||
|-|-|
| Protocol | ModbusRTU |
| Baud rate | 115200 | 
| Device ID | 1 |
| Default connection (on RPI4) | ttyAMA3 | 
| Tx pin number (on RPI4) | 7 (label GPIO4) |
| Rx pin number (on RPI4) | 29 (label GPIO5)| 

## 4. How to use
Create instance of *NHduinoController*. Each procedure has 
corresponding method. Calling this method will start procedure
execution, if the prerequisities have been met. If the 
prerequisities are not met, calling these methods will return 
*False* and procedure will not start.
| Method | Prerequisity |
|-|-|
| *Calibrate*() | None |
| *Home*() | Steppers are calibrated |
| *FetchCup*() | Manipulators are at home position |
| *ExportCoffee*() | Cup fetching ended successfully |
| *Stop*() | None |

The method *Stop()* will immediately stop all 
manipulators and reset the assembly.

The controller does not contain main loop. Therefore, the 
application using *NHduinoController* must implement all the
supervision. It should contain state machine, which will ensure 
that all procedures are called in correct order. It should also
handle all possible errors and diagnostic flags.

There is a method *CheckIfDone()*, which will read data from
the controller and update all diagnostic flags. This method 
also returns true if no procedure is running.

All diagnostic flags can be accessed via public property of
*NHduinoController* object called *diagnostics*. To verify 
status of the flag, corresponding method must be called. List of 
all diagnostic methods:
- *IsRunning()* - returns *True* if any procedure is running
- *IsCalibrated()* - returns *True* if steppers are calibrated
 - *IsHome()* - returns *True* if steppers are homed
- *FirstPhaseDone()* - returns *True* if cup fetching
  procedure ended successfully

Errors:
- *StepperLost()* - returns *True* if any stepper got lost
- *CupMissing()* - returns *True* if cup is not present on 
manipulator when it should be
- *CupPresent()* - returns *True* if cup is present on 
manipulator when there should be none
- *FullConveyor()* - returns *True* if drink storage is full
- *IsError()* - returns *True* if any error is present

The method *PrintStatus()* will print all diagnostics flags, 
along with current Phase on which the assembly is.

Finally, there is a method *CupOccupancy()*, which returns
the binary representation of drink storage state. Each bit
represents one position on the conveyor. If the bit is set to 1,
it means that the position is occupied by a cup.

## 5. Testing loop
In *Program.cs* there is a simple testing loop, which can be used
to verify the functionality of the assembly. It will read the 
command from console and execute corresponding procedure. The
list of commands:
- **0** - Calibrate
- **1** - Home
- **2** - Fetch cup
- **3** - Export coffee
- **p** - Print diagnostic data

When the procedure has been called, another one can be called only
when the previous one has ended. The testing loop will also 
display the result of procedure call (True/False).





