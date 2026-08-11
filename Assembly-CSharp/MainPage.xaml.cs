using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace AppLibrary
{
    public class GameApp : INotifyPropertyChanged
    {
        private string _name;
        private string _author;
        private string _exePath;
        private string _iconPath;
        private string _bannerPath;
        private bool _isPinned;
        private bool _isFavourite;
        private bool _isSelected;
        private Visibility _checkBoxVisibility = Visibility.Collapsed;

        public string Name { get => _name; set { _name = value; OnPropertyChanged(nameof(Name)); } }
        public string Author { get => _author; set { _author = value; OnPropertyChanged(nameof(Author)); } }
        public string ExePath { get => _exePath; set { _exePath = value; OnPropertyChanged(nameof(ExePath)); } }
        public string IconPath { get => _iconPath; set { _iconPath = value; OnPropertyChanged(nameof(IconPath)); } }
        public string BannerPath { get => _bannerPath; set { _bannerPath = value; OnPropertyChanged(nameof(BannerPath)); } }
        public bool IsPinned { get => _isPinned; set { _isPinned = value; OnPropertyChanged(nameof(IsPinned)); } }
        public bool IsFavourite { get => _isFavourite; set { _isFavourite = value; OnPropertyChanged(nameof(IsFavourite)); } }
        public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); } }
        public Visibility CheckBoxVisibility { get => _checkBoxVisibility; set { _checkBoxVisibility = value; OnPropertyChanged(nameof(CheckBoxVisibility)); } }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public sealed partial class MainPage : Page
    {
        // Internal collection holding all saved apps
        public ObservableCollection<GameApp> Apps { get; set; } = new ObservableCollection<GameApp>();
        
        // Collection currently presented to GridView based on active Tab and Search Filter
        public ObservableCollection<GameApp> DisplayedApps { get; set; } = new ObservableCollection<GameApp>();

        private GameApp _appBeingEdited = null;
        private string _currentTabTag = "all_apps";
        private bool _isSelectionActive = false;

        private const string DATA_FILE_NAME = "library_data.json";

        public MainPage()
        {
            this.InitializeComponent();

            Apps.CollectionChanged += (s, e) => { SaveLibraryDataAsync(); ApplyFilter(); };
            this.Loaded += async (s, e) => 
            {
                LoadAppHeaderTitle();
                await LoadLibraryDataAsync();
            };
        }

        public void LoadAppHeaderTitle()
        {
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue("CustomHeaderTitle", out object titleObj) &&
                titleObj is string title && !string.IsNullOrWhiteSpace(title))
            {
                AppHeaderTitle.Text = title;
            }
            else
            {
                AppHeaderTitle.Text = "App Library";
            }
        }

        // --- INTERNAL STORAGE (LocalFolder) --- //

        private async Task LoadLibraryDataAsync()
        {
            try
            {
                StorageFolder localFolder = ApplicationData.Current.LocalFolder;
                StorageFile file = await localFolder.GetFileAsync(DATA_FILE_NAME);
                string json = await FileIO.ReadTextAsync(file);

                var loadedApps = JsonSerializer.Deserialize<ObservableCollection<GameApp>>(json);
                if (loadedApps != null)
                {
                    Apps.Clear();
                    foreach (var app in loadedApps)
                    {
                        app.PropertyChanged += App_PropertyChanged;
                        Apps.Add(app);
                    }
                }
            }
            catch (FileNotFoundException) { }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading library data: {ex.Message}");
            }
            ApplyFilter();
        }

        private async void SaveLibraryDataAsync()
        {
            try
            {
                StorageFolder localFolder = ApplicationData.Current.LocalFolder;
                StorageFile file = await localFolder.CreateFileAsync(DATA_FILE_NAME, CreationCollisionOption.ReplaceExisting);
                string json = JsonSerializer.Serialize(Apps);
                await FileIO.WriteTextAsync(file, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving library data: {ex.Message}");
            }
        }

        private void App_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(GameApp.IsSelected) && e.PropertyName != nameof(GameApp.CheckBoxVisibility))
            {
                SaveLibraryDataAsync();
                ApplyFilter();
            }
        }

        // --- FILTER & TAB NAVIGATION --- //

        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            ApplyFilter();
        }

        private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.IsSettingsInvoked)
            {
                var settingsWindow = new SettingsWindow();
                settingsWindow.Activate();
                return;
            }

            if (args.InvokedItemContainer is NavigationViewItem item)
            {
                _currentTabTag = item.Tag?.ToString() ?? "all_apps";
                SwitchToTab(_currentTabTag);
            }
        }

        private void SwitchToTab(string tag)
        {
            AppsGridView.Visibility = Visibility.Collapsed;
            EditorGridView.Visibility = Visibility.Collapsed;
            EmptyStateGrid.Visibility = Visibility.Collapsed;
            AboutGrid.Visibility = Visibility.Collapsed;
            EditorPanel.Visibility = Visibility.Collapsed;
            HeaderActionsPanel.Visibility = Visibility.Visible;

            if (tag == "editor")
            {
                HeaderActionsPanel.Visibility = Visibility.Collapsed;
                EditorGridView.Visibility = Visibility.Visible;
            }
            else if (tag == "about")
            {
                HeaderActionsPanel.Visibility = Visibility.Collapsed;
                AboutGrid.Visibility = Visibility.Visible;
            }

            ApplyFilter();
        }

        private void ApplyFilter()
        {
            string query = SearchBox?.Text?.Trim().ToLower() ?? "";
            
            var filtered = Apps.AsEnumerable();

            // 1. Tab filter
            if (_currentTabTag == "pins")
                filtered = filtered.Where(a => a.IsPinned);
            else if (_currentTabTag == "favourites")
                filtered = filtered.Where(a => a.IsFavourite);

            // 2. Search query filter (filters strictly inside the current active tab context)
            if (!string.IsNullOrWhiteSpace(query))
            {
                filtered = filtered.Where(a => 
                    (!string.IsNullOrEmpty(a.Name) && a.Name.ToLower().Contains(query)) ||
                    (!string.IsNullOrEmpty(a.Author) && a.Author.ToLower().Contains(query))
                );
            }

            DisplayedApps.Clear();
            foreach (var item in filtered)
            {
                DisplayedApps.Add(item);
            }

            bool hasItems = DisplayedApps.Count > 0;
            if (_currentTabTag == "all_apps" || _currentTabTag == "pins" || _currentTabTag == "favourites")
            {
                AppsGridView.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
                EmptyStateGrid.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
            }
            else if (_currentTabTag == "editor")
            {
                EditorGridView.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
                EmptyStateGrid.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        // --- PLAY & FOLDER BUTTONS --- //

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is GameApp app && !string.IsNullOrEmpty(app.ExePath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = app.ExePath,
                        UseShellExecute = true,
                        WorkingDirectory = Path.GetDirectoryName(app.ExePath)
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to start executable: {ex.Message}");
                }
            }
        }

        private void FolderButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is GameApp app && !string.IsNullOrEmpty(app.ExePath))
            {
                try
                {
                    Process.Start("explorer.exe", $"/select,\"{app.ExePath}\"");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to open directory: {ex.Message}");
                }
            }
        }

        // --- EDITOR PANEL & ASSET PICKING --- //

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is GameApp app)
            {
                _appBeingEdited = app;
                EditNameBox.Text = app.Name ?? "";
                EditAuthorBox.Text = app.Author ?? "";

                EditIconPreview.Source = !string.IsNullOrEmpty(app.IconPath) ? new BitmapImage(new Uri(app.IconPath)) : null;
                EditBannerPreview.Source = !string.IsNullOrEmpty(app.BannerPath) ? new BitmapImage(new Uri(app.BannerPath)) : null;

                AppsGridView.Visibility = Visibility.Collapsed;
                EditorGridView.Visibility = Visibility.Collapsed;
                EmptyStateGrid.Visibility = Visibility.Collapsed;
                AboutGrid.Visibility = Visibility.Collapsed;
                HeaderActionsPanel.Visibility = Visibility.Collapsed;

                EditorPanel.Visibility = Visibility.Visible;
            }
        }

        private async void ChangeIcon_Click(object sender, RoutedEventArgs e)
        {
            if (_appBeingEdited == null) return;

            var picker = new FileOpenPicker();
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".ico");

            var hwnd = WinRT.Interop.WindowNative.GetWindowNative(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                string folder = Path.Combine(ApplicationData.Current.LocalFolder.Path, "Icons");
                Directory.CreateDirectory(folder);
                string destPath = Path.Combine(folder, $"{Guid.NewGuid()}{file.FileType}");
                
                await file.CopyAsync(await StorageFolder.GetFolderFromPathAsync(folder), Path.GetFileName(destPath), NameCollisionOption.ReplaceExisting);

                _appBeingEdited.IconPath = destPath;
                EditIconPreview.Source = new BitmapImage(new Uri(destPath));
            }
        }

        private async void ChangeBanner_Click(object sender, RoutedEventArgs e)
        {
            if (_appBeingEdited == null) return;

            var picker = new FileOpenPicker();
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");

            var hwnd = WinRT.Interop.WindowNative.GetWindowNative(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                string folder = Path.Combine(ApplicationData.Current.LocalFolder.Path, "Banners");
                Directory.CreateDirectory(folder);
                string destPath = Path.Combine(folder, $"{Guid.NewGuid()}{file.FileType}");

                await file.CopyAsync(await StorageFolder.GetFolderFromPathAsync(folder), Path.GetFileName(destPath), NameCollisionOption.ReplaceExisting);

                _appBeingEdited.BannerPath = destPath;
                EditBannerPreview.Source = new BitmapImage(new Uri(destPath));
            }
        }

        private void SaveEdit_Click(object sender, RoutedEventArgs e)
        {
            if (_appBeingEdited != null)
            {
                _appBeingEdited.Name = EditNameBox.Text;
                _appBeingEdited.Author = EditAuthorBox.Text;
            }
            CloseEditor();
        }

        private void DiscardEdit_Click(object sender, RoutedEventArgs e)
        {
            CloseEditor();
        }

        private void CloseEditor()
        {
            _appBeingEdited = null;
            SwitchToTab(_currentTabTag);
        }

        // --- AUTOMATED IMPORT & METADATA EXTRACTION --- //

        private async void NewButton_Click(object sender, RoutedEventArgs e) => await ProcessFolderPick(searchSubFolders: false);
        private async void ImportButton_Click(object sender, RoutedEventArgs e) => await ProcessFolderPick(searchSubFolders: true);

        private async Task ProcessFolderPick(bool searchSubFolders)
        {
            FolderPicker folderPicker = new FolderPicker();
            folderPicker.SuggestedStartLocation = PickerLocationId.Desktop;
            folderPicker.FileTypeFilter.Add("*");

            var hwnd = WinRT.Interop.WindowNative.GetWindowNative(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                try
                {
                    SearchOption option = searchSubFolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                    var exeFiles = Directory.GetFiles(folder.Path, "*.exe", option);

                    if (exeFiles.Length > 0)
                    {
                        foreach (var exePath in exeFiles)
                        {
                            string appName = Path.GetFileNameWithoutExtension(exePath);
                            string authorName = "Unknown Author";

                            // Automatically snatch metadata
                            try
                            {
                                var info = FileVersionInfo.GetVersionInfo(exePath);
                                if (!string.IsNullOrWhiteSpace(info.ProductName)) appName = info.ProductName;
                                else if (!string.IsNullOrWhiteSpace(info.FileDescription)) appName = info.FileDescription;

                                if (!string.IsNullOrWhiteSpace(info.CompanyName)) authorName = info.CompanyName;
                            }
                            catch { }

                            // Snatch icon
                            string extractedIconPath = ExtractAndSaveIcon(exePath);

                            var newApp = new GameApp
                            {
                                Name = appName,
                                Author = authorName,
                                ExePath = exePath,
                                IconPath = extractedIconPath,
                                IsPinned = false,
                                IsFavourite = false
                            };

                            newApp.PropertyChanged += App_PropertyChanged;
                            Apps.Add(newApp);
                        }

                        await ShowSuccessDialog();
                    }
                    else
                    {
                        await ShowErrorDialog();
                    }
                }
                catch
                {
                    await ShowErrorDialog();
                }
            }
        }

        private string ExtractAndSaveIcon(string exePath)
        {
            try
            {
                using (var sysIcon = System.Drawing.Icon.ExtractAssociatedIcon(exePath))
                {
                    if (sysIcon != null)
                    {
                        string iconsFolder = Path.Combine(ApplicationData.Current.LocalFolder.Path, "Icons");
                        Directory.CreateDirectory(iconsFolder);
                        string destPath = Path.Combine(iconsFolder, $"{Guid.NewGuid()}.png");

                        using (var bitmap = sysIcon.ToBitmap())
                        {
                            bitmap.Save(destPath, System.Drawing.Imaging.ImageFormat.Png);
                        }
                        return destPath;
                    }
                }
            }
            catch { }
            return null;
        }

        // --- SELECTION MODE & BULK DELETE --- //

        private void SelectSplitButton_Click(SplitButton sender, SplitButtonClickEventArgs args)
        {
            _isSelectionActive = !_isSelectionActive;
            Visibility vis = _isSelectionActive ? Visibility.Visible : Visibility.Collapsed;

            foreach (var app in Apps)
            {
                app.CheckBoxVisibility = vis;
            }
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var app in DisplayedApps)
            {
                app.IsSelected = true;
            }
        }

        private void DeleteSelection_Click(object sender, RoutedEventArgs e)
        {
            var selectedList = Apps.Where(a => a.IsSelected).ToList();
            foreach (var app in selectedList)
            {
                Apps.Remove(app);
            }
        }

        // --- ABOUT LINKS --- //

        private async void LicenseButton_Click(object sender, RoutedEventArgs e) =>
            await Windows.System.Launcher.LaunchUriAsync(new Uri("https://choosealicense.com/licenses/mit/"));

        private async void SourceCodeButton_Click(object sender, RoutedEventArgs e) =>
            await Windows.System.Launcher.LaunchUriAsync(new Uri("https://github.com"));

        // --- DIALOGS --- //

        private async Task ShowSuccessDialog()
        {
            StackPanel contentPanel = new StackPanel();
            contentPanel.Children.Add(new TextBlock
            {
                Text = "Process was completed successfully, would you like to update their Metadata?",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 16)
            });
            contentPanel.Children.Add(new CheckBox { Content = "Do not show this again" });

            ContentDialog successDialog = new ContentDialog
            {
                XamlRoot = this.XamlRoot,
                Content = contentPanel,
                PrimaryButtonText = "Open Editor",
                SecondaryButtonText = "No, thanks"
            };

            Style buttonStyle = new Style(typeof(Button));
            buttonStyle.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush(Colors.Blue)));
            buttonStyle.Setters.Add(new Setter(Button.ForegroundProperty, new SolidColorBrush(Colors.White)));
            successDialog.PrimaryButtonStyle = buttonStyle;
            successDialog.SecondaryButtonStyle = buttonStyle;

            if (await successDialog.ShowAsync() == ContentDialogResult.Primary)
            {
                NavView.SelectedItem = NavView.MenuItems[3];
                NavView_ItemInvoked(NavView, new NavigationViewItemInvokedEventArgs() { InvokedItemContainer = (NavigationViewItem)NavView.MenuItems[3] });
            }
        }

        private async Task ShowErrorDialog()
        {
            ContentDialog errorDialog = new ContentDialog
            {
                XamlRoot = this.XamlRoot,
                Content = new TextBlock { Text = "Process failed due to an error, would you like to retry?" },
                PrimaryButtonText = "Yes",
                SecondaryButtonText = "No"
            };

            Style buttonStyle = new Style(typeof(Button));
            buttonStyle.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush(Colors.Blue)));
            buttonStyle.Setters.Add(new Setter(Button.ForegroundProperty, new SolidColorBrush(Colors.White)));
            errorDialog.PrimaryButtonStyle = buttonStyle;
            errorDialog.SecondaryButtonStyle = buttonStyle;

            if (await errorDialog.ShowAsync() == ContentDialogResult.Primary)
            {
                ImportButton_Click(null, null);
            }
        }
    }
}