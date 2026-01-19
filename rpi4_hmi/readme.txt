Spustenie projektu:
Pomocou príkazu "dotnet run" v zložke /home/rpi4/programs/autonomous-coffe-machine/Firmware/rpi4_hmi

Projekt obsahuje 2 podprojekty - ovládanie pohybu hrnčekov a ovládanie kávovaru 
(každý z nich je umiestnený v samostatnej zložke)

Súbor Program.cs je zdrojový kód hlavnej metódy, obsahuje inicializáciu Avalonia
GUI a globálne premenné používané v celom programe. 
V súbore MainWindow.axaml je definovaný vzhľad HMI, v súbore MainWindowViewModel.cs 
sú metódy na interakciu s HMI. 

Controller.cs predstavuje backend, sú odtiaľto spúšťané procedúry na podávači hrnčekov
a samotnom kávovare.