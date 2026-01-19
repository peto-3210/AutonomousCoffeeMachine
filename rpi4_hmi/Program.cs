
using Avalonia;
using Avalonia.ReactiveUI;
using System;
using System.Threading;
using System.IO.Ports;
using NModbus;
using NModbus.Serial;
using Avalonia.Controls;
using Avalonia.Input;
using rpi4_backend.ViewModels;
using DynamicData.Tests;
using ReactiveUI;
using Avalonia.Data.Core.Plugins;
using rpi4_backend.Views;

namespace rpi4_backend
{
    public static class Globals
    {
        public static string Order1 = "";
        public static string OrderedBy1 = "";
        public static string Order2 = "";
        public static string OrderedBy2 = "";
        public static string Order3 = "";
        public static string OrderedBy3 = "";
        public static string Order4 = "";
        public static string OrderedBy4 = "";

        private static int currentCup = 1;
        public static void UpdateText(string order){
            switch (currentCup){
                case 1:
                    Order1 = order;
                    break;
                case 2:
                    Order2 = order;
                    break;
                case 3:
                    Order3 = order;
                    break;
                case 4:
                    Order4 = order;
                    break;
            }
            /*currentCup++;
            if (currentCup == 5){
                currentCup = 1;
            }*/
        }

        public static void UpdateState(string state){

            switch (currentCup){
                case 1:
                    OrderedBy1 = state;
                    break;
                case 2:
                    OrderedBy2 = state;
                    break;
                case 3:
                    OrderedBy3 = state;
                    break;
                case 4:
                    OrderedBy4 = state;
                    break;
            }

            if (state.Contains("done") || state.Contains("failed")){
                currentCup++;
                if (currentCup == 5){
                    currentCup = 1;
                }
            }


        }

        public static void ResetText(){
            Order1 = "";
            OrderedBy1 = "";
            Order2 = "";
            OrderedBy2 = "";
            Order3 = "";
            OrderedBy3 = "";
            Order4 = "";
            OrderedBy4 = "";
            currentCup = 1;
        }

        public static Controller control = new();
    }
    public class Program
    {
        

        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        //[STAThread]
        /*public static void Main(string[] args) => BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);*/

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace().UseReactiveUI();
    
        static void Main(string[] args)
        {
            Globals.control.StartCommandHandler();
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

    }
}

