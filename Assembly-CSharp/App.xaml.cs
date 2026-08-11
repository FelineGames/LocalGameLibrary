using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AppLibrary
{
    public partial class App : Application
    {
        public static Window MainWindow { get; private set; }

        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            MainWindow = new Window();
            MainWindow.Title = "App Library";

            Frame rootFrame = new Frame();
            rootFrame.Navigate(typeof(MainPage), args.Arguments);

            MainWindow.Content = rootFrame;
            MainWindow.Activate();
        }
    }
}
