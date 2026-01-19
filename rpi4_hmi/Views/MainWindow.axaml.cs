using Avalonia.Controls;
using Avalonia.Threading;
using System;
using System.Threading.Tasks;


namespace rpi4_backend.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        // Execute OnTextFromAnotherThread on the thread pool
        // to demonstrate how to access the UI thread from
        // there.
        //Program testing = new Program();
        Thread myThread = new Thread(new ThreadStart(OrdersChanger));
        myThread.Start();
    }

    private void SetText1(string text) => Order1.Text = text;
    private void SetText11(string text) => OrderedBy1.Text = text;
    private void SetText2(string text) => Order2.Text = text;
    private void SetText22(string text) => OrderedBy2.Text = text;
    private void SetText3(string text) => Order3.Text = text;
    private void SetText33(string text) => OrderedBy3.Text = text;
    private void SetText4(string text) => Order4.Text = text;
    private void SetText44(string text) => OrderedBy4.Text = text;
    private string GetText() => Order1.Text ?? "";

    private async void OnTextFromAnotherThread(string Order1, string OrderedBy1, string Order2, string OrderedBy2, string Order3, string OrderedBy3, string Order4, string OrderedBy4)
    {
        try
        {
            // Start the job on the ui thread and return immediately.
            Dispatcher.UIThread.Post(() => SetText1(Order1));
            Dispatcher.UIThread.Post(() => SetText11(OrderedBy1));
            Dispatcher.UIThread.Post(() => SetText2(Order2));
            Dispatcher.UIThread.Post(() => SetText22(OrderedBy2));
            Dispatcher.UIThread.Post(() => SetText3(Order3));
            Dispatcher.UIThread.Post(() => SetText33(OrderedBy3));
            Dispatcher.UIThread.Post(() => SetText4(Order4));
            Dispatcher.UIThread.Post(() => SetText44(OrderedBy4));


            // Start the job on the ui thread and wait for the result.
            var result = await Dispatcher.UIThread.InvokeAsync(GetText);

            // This invocation would cause an exception because we are
            // running on a worker thread:
            // System.InvalidOperationException: 'Call from invalid thread'
            //SetText(text);
        }
        catch (Exception)
        {
            throw; // Todo: Handle exception.
        }
    }

    void OrdersChanger()
    {
        while (true)
        {
            Thread.Sleep(1000);
            _ = Task.Run(() => OnTextFromAnotherThread(Globals.Order1, Globals.OrderedBy1, Globals.Order2, Globals.OrderedBy2, Globals.Order3, Globals.OrderedBy3, Globals.Order4, Globals.OrderedBy4));
        }

    }

}
