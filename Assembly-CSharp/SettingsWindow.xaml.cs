using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Storage;

namespace AppLibrary
{
    public sealed partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            this.InitializeComponent();

            // Load saved display text title
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue("CustomHeaderTitle", out object titleObj) && titleObj is string title)
            {
                CustomTitleBox.Text = title;
            }

            // Load current dark mode state
            if (App.MainWindow?.Content is FrameworkElement root)
            {
                DarkModeToggle.IsOn = root.RequestedTheme == ElementTheme.Dark;
            }
        }

        private void SettingsSave_Click(object sender, RoutedEventArgs e)
        {
            // Save title permanently to LocalSettings
            string customText = CustomTitleBox.Text;
            ApplicationData.Current.LocalSettings.Values["CustomHeaderTitle"] = customText;

            // Instantly update header title on main page
            if (App.MainWindow?.Content is Frame frame && frame.Content is MainPage mainPage)
            {
                mainPage.LoadAppHeaderTitle();
            }

            // Save Theme setting
            bool isDark = DarkModeToggle.IsOn;
            ApplicationData.Current.LocalSettings.Values["OledDarkMode"] = isDark;

            // Apply OLED theme across main window
            if (App.MainWindow?.Content is FrameworkElement mainRoot)
            {
                mainRoot.RequestedTheme = isDark ? ElementTheme.Dark : ElementTheme.Light;
                if (mainRoot is Frame mainFrame && mainFrame.Content is Page page && page.Content is Grid mainGrid)
                {
                    mainGrid.Background = isDark ? new SolidColorBrush(Colors.Black) : new SolidColorBrush(Colors.Transparent);
                }
            }

            // Apply theme to Settings Window
            this.SettingsRoot.RequestedTheme = isDark ? ElementTheme.Dark : ElementTheme.Light;
            this.SettingsRoot.Background = isDark ? new SolidColorBrush(Colors.Black) : new SolidColorBrush(Colors.Transparent);
        }
    }
}
