using Avalonia.Controls;
using ReactiveUI;
using rpi4_backend.Views;
using System.Windows.Input;


namespace rpi4_backend.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
#pragma warning disable CA1822 // Mark members as static
    private string _Order1 = "";
    public string Order1
    {
        get => _Order1;
        set => this.RaiseAndSetIfChanged(ref _Order1, Globals.Order1);
    }
    private string _OrderedBy1 = "";
    public string OrderedBy1
    {
        get => _OrderedBy1;
        set => this.RaiseAndSetIfChanged(ref _OrderedBy1, value);
    }
    private string _Order2 = "";
    public string Order2
    {
        get => _Order2;
        set => this.RaiseAndSetIfChanged(ref _Order2, value);
    }
    private string _OrderedBy2 = "";
    public string OrderedBy2
    {
        get => _OrderedBy2;
        set => this.RaiseAndSetIfChanged(ref _OrderedBy2, value);
    }
    private string _Order3 = "";
    public string Order3
    {
        get => _Order3;
        set => this.RaiseAndSetIfChanged(ref _Order3, value);
    }
    private string _OrderedBy3 = "";
    public string OrderedBy3
    {
        get => _OrderedBy3;
        set => this.RaiseAndSetIfChanged(ref _OrderedBy3, value);
    }
    private string _Order4 = "";
    public string Order4
    {
        get => _Order4;
        set => this.RaiseAndSetIfChanged(ref _Order4, value);
    }
    private string _OrderedBy4 = "";
    public string OrderedBy4
    {
        get => _OrderedBy4;
        set => this.RaiseAndSetIfChanged(ref _OrderedBy4, value);
    }
    public ICommand OrderCoffee { get; }
    public ICommand OrderEspresso { get; }
    public ICommand OrderAmericano { get; }
    public ICommand Calibrate { get; }
    public ICommand SetAroma { get; }
    public ICommand SetTemperature { get; }
    public ICommand StartMachine { get; }

    public MainWindowViewModel()
    {
        var Error = new ErrorWindow();
        OrderCoffee = ReactiveCommand.Create(() =>
        {
            /*
            const string PrimarySerialPortName = "COM1";
            Connection conn = new(PrimarySerialPortName, 115200);
            var PicoController = new rpi4_backend.PicoController(conn, 2);
            bool yolo = PicoController.MakeCoffee();*/
            //var Error = new ErrorWindowViewModel();

            if (Globals.control.SetCommand(Controller.Commands.coffee) == false)
            {
                Globals.UpdateText($"Command running: {Globals.control.CurrentCommand}");
                Globals.UpdateState($"Machine state: {Globals.control.Machine.State}");
                try
                {
                    Error.Show();
                }
                catch
                {
                    Error = new ErrorWindow();
                    Error.Show();
                }
            }
            else
            {
                Globals.UpdateText("Coffee");
            }
            Thread.Sleep(200);
        });

        OrderEspresso = ReactiveCommand.Create(() =>
        {
            /*const string PrimarySerialPortName = "COM1";
            Connection conn = new(PrimarySerialPortName, 115200);
            var PicoController = new rpi4_backend.PicoController(conn, 2);
            bool yolo = PicoController.MakeEspresso();*/
            if (Globals.control.SetCommand(Controller.Commands.espresso) == false)
            {
                Globals.UpdateText($"Command running: {Globals.control.CurrentCommand}");
                Globals.UpdateState($"Machine state: {Globals.control.Machine.State}");
                try
                {
                    Error.Show();
                }
                catch
                {
                    Error = new ErrorWindow();
                    Error.Show();
                }
            }
            else
            {
                Globals.UpdateText("Espresso");
            }
            Thread.Sleep(200);
        });

        OrderAmericano = ReactiveCommand.Create(() =>
        {
            /*
            const string PrimarySerialPortName = "COM1";
            Connection conn = new(PrimarySerialPortName, 115200);
            var PicoController = new rpi4_backend.PicoController(conn, 2);
            bool yolo = PicoController.MakeCappucino();*/
            if (Globals.control.SetCommand(Controller.Commands.americano) == false)
            {
                Globals.UpdateText($"Command running: {Globals.control.CurrentCommand}");
                Globals.UpdateState($"Machine state: {Globals.control.Machine.State}");
                try
                {
                    Error.Show();
                }
                catch
                {
                    Error = new ErrorWindow();
                    Error.Show();
                }
            }
            else
            {
                Globals.UpdateText("Americano");
            }
            Thread.Sleep(200);
        });

        StartMachine = ReactiveCommand.Create(() =>
        {
            /*
            const string PrimarySerialPortName = "COM1";
            Connection conn = new(PrimarySerialPortName, 115200);
            var PicoController = new rpi4_backend.PicoController(conn, 2);
            bool yolo = PicoController.MakeLatte();*/
            if (Globals.control.SetCommand(Controller.Commands.startMachine) == false)
            {
                Globals.UpdateText($"Command running: {Globals.control.CurrentCommand}");
                Globals.UpdateState($"Machine state: {Globals.control.Machine.State}");
                try
                {
                    Error.Show();
                }
                catch
                {
                    Error = new ErrorWindow();
                    Error.Show();
                }
            }
            else
            {
                Globals.UpdateText("Power on");
            }
            Thread.Sleep(200);
        });

        Calibrate = ReactiveCommand.Create(() =>
        {
            if (Globals.control.SetCommand(Controller.Commands.calibrate) == false)
            {
                Globals.UpdateText($"Command running: {Globals.control.CurrentCommand}");
                Globals.UpdateState($"Machine state: {Globals.control.Machine.State}");
                try
                {
                    Error.Show();
                }
                catch
                {
                    Error = new ErrorWindow();
                    Error.Show();
                }
            }
            else
            {
                Globals.UpdateText("Calibration");
            }
            Thread.Sleep(200);
            //Order = "Latte Macchiato";
            //OrderedBy = "HMI ORDER";
        });

        SetAroma = ReactiveCommand.Create(() =>
        {
            if (Globals.control.SetCommand(Controller.Commands.setAroma) == false)
            {
                Globals.UpdateText($"Command running: {Globals.control.CurrentCommand}");
                Globals.UpdateState($"Machine state: {Globals.control.Machine.State}");
                try
                {
                    Error.Show();
                }
                catch
                {
                    Error = new ErrorWindow();
                    Error.Show();
                }
            }
            else
            {
                Globals.UpdateText("SetAroma");
            }
            Thread.Sleep(200);
            //Order = "Latte Macchiato";
            //OrderedBy = "HMI ORDER";
        });

        SetTemperature = ReactiveCommand.Create(() =>
        {
            if (Globals.control.SetCommand(Controller.Commands.setTemperature) == false)
            {
                Globals.UpdateText($"Command running: {Globals.control.CurrentCommand}");
                Globals.UpdateState($"Machine state: {Globals.control.Machine.State}");
                try
                {
                    Error.Show();
                }
                catch
                {
                    Error = new ErrorWindow();
                    Error.Show();
                }
            }
            else
            {
                Globals.UpdateText("SetTemperature");
            }
            Thread.Sleep(200);
            //Order = "Latte Macchiato";
            //OrderedBy = "HMI ORDER";
        });
    }

#pragma warning restore CA1822 // Mark members as static
}


