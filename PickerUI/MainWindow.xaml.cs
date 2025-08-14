

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Shared;
using SIcon = System.Drawing.Icon;

namespace PickerUI
{
    public partial class MainWindow : Window
    {
        public string _urlToOpen { get; set; }
        public Browser SelectedBrowser { get; private set; }
        public BrowserProfile SelectedProfile { get; private set; }
        private Button _lastSelectedButton;

        public MainWindow(string url, AppSettings appSettings)
        {
            InitializeComponent();
            _urlToOpen = url;
            PopulateBrowserList(appSettings.Browsers);
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void PopulateBrowserList(List<Browser> browsers)
        {
            BrowserListPanel.Children.Clear();
            _lastSelectedButton = null;

            bool firstProfileSelected = false;

            foreach (var browser in browsers)
            {
                if (!browser.Profiles.Any()) continue;

                var browserHeaderPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 24, 0, 16) };

                UIElement browserIconElement = null;
                if (!string.IsNullOrEmpty(browser.ExecutablePath) && File.Exists(browser.ExecutablePath))
                {
                    try
                    {
                        var iconSource = SIcon.ExtractAssociatedIcon(browser.ExecutablePath)?.ToBitmapSource();
                        if (iconSource != null)
                        {
                            var browserIcon = new Image
                            {
                                Source = iconSource,
                                Width = 24,
                                Height = 24,
                                Margin = new Thickness(0, 0, 12, 0),
                                VerticalAlignment = VerticalAlignment.Center
                            };
                            browserIconElement = browserIcon;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error loading browser icon for {browser.Name}: {ex.Message}");
                    }
                }

                if (browserIconElement != null)
                {
                    browserHeaderPanel.Children.Add(browserIconElement);
                }

                var browserNameText = new TextBlock
                {
                    Text = browser.Name,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x32, 0x31, 0x30)),
                    FontSize = 18,
                    FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center
                };
                browserHeaderPanel.Children.Add(browserNameText);

                var countBadge = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(0xE1, 0xE1, 0xE1)),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(8, 2, 8, 2),
                    Margin = new Thickness(12, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = browser.Profiles.Count.ToString(),
                        FontSize = 12,
                        FontWeight = FontWeights.Medium,
                        Foreground = new SolidColorBrush(Color.FromRgb(0x60, 0x5E, 0x5C))
                    }
                };
                browserHeaderPanel.Children.Add(countBadge);

                BrowserListPanel.Children.Add(browserHeaderPanel);

                foreach (var profile in browser.Profiles)
                {
                    var profileButton = new Button
                    {
                        Tag = new Tuple<Browser, BrowserProfile>(browser, profile),
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        Style = (Style)FindResource("ProfileItemButtonStyle")
                    };
                    profileButton.Click += ProfileButton_Click;

                    var buttonContentStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

                    UIElement profileImageElement = null;

                    if (!string.IsNullOrEmpty(profile.ProfilePicturePath) && File.Exists(profile.ProfilePicturePath))
                    {
                        try
                        {
                            var bitmapImage = new BitmapImage();
                            bitmapImage.BeginInit();
                            bitmapImage.UriSource = new Uri(profile.ProfilePicturePath);
                            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                            bitmapImage.EndInit();

                            var img = new Image { Source = bitmapImage, Style = (Style)FindResource("ProfileImageStyle") };
                            profileImageElement = img;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error loading specific profile picture {profile.ProfilePicturePath}: {ex.Message}");
                            profileImageElement = null;
                        }
                    }

                    if (profileImageElement == null && !string.IsNullOrEmpty(profile.IconPath) && File.Exists(profile.IconPath))
                    {
                        try
                        {
                            var iconSource = SIcon.ExtractAssociatedIcon(profile.IconPath)?.ToBitmapSource();
                            if (iconSource != null)
                            {
                                var img = new Image { Source = iconSource, Style = (Style)FindResource("ProfileImageStyle") };
                                profileImageElement = img;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error loading browser icon {profile.IconPath}: {ex.Message}");
                        }
                    }

                    if (profileImageElement == null)
                    {
                        string initials = GetInitials(profile.DisplayName);
                        var initialsTextBlock = new TextBlock
                        {
                            Text = initials,
                            Foreground = Brushes.White,
                            FontSize = 16,
                            FontWeight = FontWeights.SemiBold,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        var initialsBorder = new Border
                        {
                            Background = GetModernColorForInitials(profile.DisplayName),
                            Child = initialsTextBlock,
                            Style = (Style)FindResource("ProfileInitialsStyle")
                        };
                        profileImageElement = initialsBorder;
                    }

                    buttonContentStack.Children.Add(profileImageElement);

                    var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

                    var displayNameTextBlock = new TextBlock
                    {
                        Text = profile.DisplayName,
                        Foreground = new SolidColorBrush(Color.FromRgb(0x32, 0x31, 0x30)),
                        FontSize = 15,
                        FontWeight = FontWeights.Medium
                    };
                    textStack.Children.Add(displayNameTextBlock);

                    if (!string.IsNullOrEmpty(profile.Id) && profile.Id != profile.DisplayName)
                    {
                        var profileIdText = new TextBlock
                        {
                            Text = profile.Id,
                            Foreground = new SolidColorBrush(Color.FromRgb(0x60, 0x5E, 0x5C)),
                            FontSize = 12,
                            Margin = new Thickness(0, 2, 0, 0)
                        };
                        textStack.Children.Add(profileIdText);
                    }

                    buttonContentStack.Children.Add(textStack);
                    profileButton.Content = buttonContentStack;

                    BrowserListPanel.Children.Add(profileButton);

                    if (!firstProfileSelected)
                    {
                        SelectProfileButton(profileButton);
                        firstProfileSelected = true;
                    }
                }
            }
        }

        private string GetInitials(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return "??";
            var parts = displayName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1) return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpper();
            if (parts.Length >= 2) return (parts[0][0].ToString() + parts[1][0].ToString()).ToUpper();
            return "?";
        }

        private SolidColorBrush GetModernColorForInitials(string displayName)
        {
            var modernColors = new[]
            {
                Color.FromRgb(0x00, 0x78, 0xD4),
                Color.FromRgb(0x10, 0x6E, 0xBE),
                Color.FromRgb(0x0F, 0x7B, 0x0F),
                Color.FromRgb(0xFF, 0x8C, 0x00),
                Color.FromRgb(0xE7, 0x4C, 0x3C),
                Color.FromRgb(0x88, 0x17, 0x98),
                Color.FromRgb(0x00, 0xB7, 0xC3),
                Color.FromRgb(0xCA, 0x50, 0x10)
            };

            int hash = Math.Abs(displayName.GetHashCode());
            return new SolidColorBrush(modernColors[hash % modernColors.Length]);
        }

        private void ProfileButton_Click(object sender, RoutedEventArgs e)
        {
            SelectProfileButton(sender as Button);
        }

        private void SelectProfileButton(Button currentButton)
        {
            if (currentButton == null) return;

            if (_lastSelectedButton != null && _lastSelectedButton != currentButton)
            {
                _lastSelectedButton.Style = (Style)FindResource("ProfileItemButtonStyle");
            }

            currentButton.Style = (Style)FindResource("SelectedProfileItemButtonStyle");
            _lastSelectedButton = currentButton;

            if (currentButton.Tag is Tuple<Browser, BrowserProfile> selection)
            {
                SelectedBrowser = selection.Item1;
                SelectedProfile = selection.Item2;
            }
        }

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedBrowser == null || SelectedProfile == null)
            {
                ShowModernMessageBox("Please select a browser profile.", "No Selection", MessageBoxImage.Information);
                return;
            }
            OpenBrowser();
        }

        private void OpenBrowser()
        {
            if (SelectedBrowser != null && SelectedProfile != null)
            {
                try
                {
                    string profileArg = string.Format(SelectedBrowser.CommandLineArgumentFormat, SelectedProfile.Id);
                    string arguments = $"{profileArg} \"{_urlToOpen}\"";

                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = SelectedBrowser.ExecutablePath,
                        Arguments = arguments,
                        UseShellExecute = false
                    });

                    this.DialogResult = true;
                    this.Close();
                }
                catch (Exception ex)
                {
                    ShowModernMessageBox($"Error opening browser: {ex.Message}", "Error", MessageBoxImage.Error);
                    this.DialogResult = false;
                    this.Close();
                }
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void ShowModernMessageBox(string message, string title, MessageBoxImage icon)
        {
            MessageBox.Show(this, message, title, MessageBoxButton.OK, icon);
        }
    }

    public static class IconExtensions
    {
        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        public static BitmapSource ToBitmapSource(this System.Drawing.Icon icon)
        {
            if (icon == null) return null;

            BitmapSource bitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            bitmapSource.Freeze();
            return bitmapSource;
        }
    }
}