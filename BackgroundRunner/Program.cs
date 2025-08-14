using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Shared;
using PickerUI;

namespace BackgroundRunner
{
    class Program
    {
        private static AppSettings _appSettings;
        private static readonly string _appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BrowserProfilePicker");
        private static readonly string _settingsFilePath = Path.Combine(_appDataPath, "settings.json");
        private const string PipeName = "BrowserProfilePickerPipe";
        private static Mutex _appMutex;
        private static Application _wpfApp;
        private static TaskCompletionSource<bool> _wpfAppReadyTcs = new TaskCompletionSource<bool>();

        private static readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            TypeInfoResolver = AppJsonSerializerContext.Default
        };


        [STAThread]
        static async Task Main(string[] args)
        {
            bool createdNew;
            _appMutex = new Mutex(true, "BrowserProfilePickerBackgroundRunnerMutex", out createdNew);
            if (!createdNew)
            {
                Console.WriteLine("BackgroundRunner is already running. Exiting.");
                return;
            }

            Console.WriteLine("BackgroundRunner started.");

            Directory.CreateDirectory(_appDataPath);

            _appSettings = new AppSettings();

            ScanAndSaveBrowsers();
            LoadAppSettings();

            Thread wpfThread = new Thread(() =>
            {
                _wpfApp = new Application();
                _wpfApp.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                _wpfApp.Startup += (s, e) => _wpfAppReadyTcs.SetResult(true);

                _wpfApp.Run();
            });
            wpfThread.SetApartmentState(ApartmentState.STA);
            wpfThread.IsBackground = true;
            wpfThread.Start();

            await _wpfAppReadyTcs.Task;
            Console.WriteLine("WPF Application is ready.");

            Task pipeServerTask = StartPipeServer();

            await pipeServerTask;

            _wpfApp.Dispatcher.Invoke(() => _wpfApp.Shutdown());

            _appMutex.ReleaseMutex();
            Console.WriteLine("BackgroundRunner exited.");
        }


        private static void LoadAppSettings()
        {
            if (File.Exists(_settingsFilePath))
            {
                try
                {
                    string json = File.ReadAllText(_settingsFilePath);
                    _appSettings = JsonSerializer.Deserialize<AppSettings>(json, _jsonSerializerOptions);
                    Console.WriteLine($"Settings loaded from {_settingsFilePath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading settings: {ex.Message}");
                    _appSettings = new AppSettings();
                }
            }
            else
            {
                _appSettings = new AppSettings();
                Console.WriteLine("settings.json not found. Will scan for browsers.");
            }
        }

        private static void SaveAppSettings()
        {
            try
            {
                string json = JsonSerializer.Serialize(_appSettings, _jsonSerializerOptions);
                File.WriteAllText(_settingsFilePath, json);
                Console.WriteLine($"Settings saved to {_settingsFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving settings: {ex.Message}");
            }
        }


        private static void ScanAndSaveBrowsers()
        {
            var detectedBrowsers = new List<Browser>();

            string chromeExePath = FindBrowserExecutable("chrome.exe", "Google\\Chrome\\Application");
            if (chromeExePath != null)
            {
                var chrome = new Browser
                {
                    Name = "Google Chrome",
                    ExecutablePath = chromeExePath,
                    ProfileRootPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google\\Chrome\\User Data"),
                    CommandLineArgumentFormat = "--profile-directory=\"{0}\""
                };
                ScanChromiumProfiles(chrome);
                if (chrome.Profiles.Any()) detectedBrowsers.Add(chrome);
            }

            string edgeExePath = FindBrowserExecutable("msedge.exe", "Microsoft\\Edge\\Application");
            if (edgeExePath != null)
            {
                var edge = new Browser
                {
                    Name = "Microsoft Edge",
                    ExecutablePath = edgeExePath,
                    ProfileRootPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft\\Edge\\User Data"),
                    CommandLineArgumentFormat = "--profile-directory=\"{0}\""
                };
                ScanChromiumProfiles(edge);
                if (edge.Profiles.Any()) detectedBrowsers.Add(edge);
            }

            string braveExePath = FindBrowserExecutable("brave.exe", "BraveSoftware\\Brave-Browser\\Application");
            if (braveExePath == null)
            {
                braveExePath = FindBrowserExecutable("brave.exe", "BraveSoftware\\Brave-Browser\\Application", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
            }

            if (braveExePath != null)
            {
                var brave = new Browser
                {
                    Name = "Brave Browser",
                    ExecutablePath = braveExePath,
                    ProfileRootPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BraveSoftware\\Brave-Browser\\User Data"),
                    CommandLineArgumentFormat = "--profile-directory=\"{0}\""
                };
                ScanChromiumProfiles(brave);
                if (brave.Profiles.Any()) detectedBrowsers.Add(brave);
            }


            string firefoxExePath = FindBrowserExecutable("firefox.exe", "Mozilla Firefox");
            if (firefoxExePath != null)
            {
                var firefox = new Browser
                {
                    Name = "Mozilla Firefox",
                    ExecutablePath = firefoxExePath,
                    ProfileRootPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mozilla\\Firefox\\Profiles"),
                    CommandLineArgumentFormat = "-p \"{0}\""
                };
                ScanFirefoxProfiles(firefox);
                if (firefox.Profiles.Any()) detectedBrowsers.Add(firefox);
            }

            _appSettings.Browsers = detectedBrowsers;
            SaveAppSettings();
        }

        private static string FindBrowserExecutable(string exeName, string commonPathPart, string basePath = null)
        {
            string[] possibleBasePaths;
            if (basePath != null)
            {
                possibleBasePaths = new string[] { basePath };
            }
            else
            {
                possibleBasePaths = new string[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                };
            }


            foreach (var path in possibleBasePaths)
            {
                string fullPath = Path.Combine(path, commonPathPart, exeName);
                if (File.Exists(fullPath)) return fullPath;
            }
            return null;
        }

        private static void ScanChromiumProfiles(Browser browser)
        {
            if (!Directory.Exists(browser.ProfileRootPath)) return;

            var profileInfoMap = new Dictionary<string, (string DisplayName, string PictureFileName)>();
            string localStatePath = Path.Combine(browser.ProfileRootPath, "Local State");

            if (File.Exists(localStatePath))
            {
                try
                {
                    string localStateJson = File.ReadAllText(localStatePath);
                    using (JsonDocument doc = JsonDocument.Parse(localStateJson))
                    {
                        if (doc.RootElement.TryGetProperty("profile", out JsonElement profileElement) &&
                            profileElement.TryGetProperty("info_cache", out JsonElement infoCacheElement))
                        {
                            foreach (JsonProperty profileEntry in infoCacheElement.EnumerateObject())
                            {
                                string profileId = profileEntry.Name;
                                string displayName = null;
                                string pictureFileName = null;
                                string gaiaName = null;
                                string name = null;
                                string userName = null;
                                string hostedDomain = null;

                                if (profileEntry.Value.TryGetProperty("gaia_name", out JsonElement gaiaNameElement))
                                {
                                    gaiaName = gaiaNameElement.GetString();
                                }
                                if (profileEntry.Value.TryGetProperty("name", out JsonElement nameElement))
                                {
                                    name = nameElement.GetString();
                                }
                                if (profileEntry.Value.TryGetProperty("user_name", out JsonElement userNameElement))
                                {
                                    userName = userNameElement.GetString();
                                }
                                if (profileEntry.Value.TryGetProperty("hosted_domain", out JsonElement hostedDomainElement))
                                {
                                    hostedDomain = hostedDomainElement.GetString();
                                }
                                if (profileEntry.Value.TryGetProperty("gaia_picture_file_name", out JsonElement pictureElement))
                                {
                                    pictureFileName = pictureElement.GetString();
                                }

                                if (!string.IsNullOrEmpty(gaiaName) && gaiaName != "Default")
                                {
                                    displayName = gaiaName;
                                }
                                else if (!string.IsNullOrEmpty(name))
                                {
                                    displayName = name;
                                }
                                else if (!string.IsNullOrEmpty(userName))
                                {
                                    displayName = userName;
                                }
                                else
                                {
                                    displayName = profileId;
                                }

                                if (!string.IsNullOrEmpty(userName) && displayName != userName && !displayName.Contains(userName, StringComparison.OrdinalIgnoreCase))
                                {
                                    if (!displayName.Contains("@"))
                                    {
                                        displayName += $" ({userName})";
                                    }
                                }

                                if (!string.IsNullOrEmpty(hostedDomain) && hostedDomain != "NO_HOSTED_DOMAIN")
                                {
                                    if (!displayName.Contains(hostedDomain, StringComparison.OrdinalIgnoreCase))
                                    {
                                        displayName += $" ({hostedDomain})";
                                    }
                                }

                                if (!string.IsNullOrEmpty(displayName) || !string.IsNullOrEmpty(pictureFileName))
                                {
                                    profileInfoMap[profileId] = (displayName, pictureFileName);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading Local State for {browser.Name}: {ex.Message}");
                }
            }


            var profileDirectories = Directory.EnumerateDirectories(browser.ProfileRootPath)
                                              .Where(dir =>
                                              {
                                                  string dirName = new DirectoryInfo(dir).Name;
                                                  return dirName == "Default" || dirName.StartsWith("Profile ");
                                              });

            foreach (var dir in profileDirectories)
            {
                string profileDirName = new DirectoryInfo(dir).Name;
                string displayName = profileDirName;
                string profilePicturePath = null;

                if (profileInfoMap.TryGetValue(profileDirName, out var mappedInfo))
                {
                    if (!string.IsNullOrEmpty(mappedInfo.DisplayName))
                    {
                        displayName = mappedInfo.DisplayName;
                    }

                    if (!string.IsNullOrEmpty(mappedInfo.PictureFileName))
                    {
                        string potentialPicturePath = Path.Combine(dir, mappedInfo.PictureFileName);
                        if (File.Exists(potentialPicturePath))
                        {
                            profilePicturePath = potentialPicturePath;
                        }
                    }
                }
                else
                {
                    string preferencesPath = Path.Combine(dir, "Preferences");
                    if (File.Exists(preferencesPath))
                    {
                        try
                        {
                            string preferencesJson = File.ReadAllText(preferencesPath);
                            using (JsonDocument doc = JsonDocument.Parse(preferencesJson))
                            {
                                if (doc.RootElement.TryGetProperty("profile", out JsonElement profileElement) &&
                                    profileElement.TryGetProperty("name", out JsonElement nameElement))
                                {
                                    string extractedDisplayName = nameElement.GetString();
                                    if (!string.IsNullOrEmpty(extractedDisplayName))
                                    {
                                        displayName = extractedDisplayName;
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error reading preferences for {profileDirName}: {ex.Message}");
                        }
                    }
                }

                browser.Profiles.Add(new BrowserProfile
                {
                    Id = profileDirName,
                    DisplayName = displayName,
                    IconPath = browser.ExecutablePath,
                    ProfilePicturePath = profilePicturePath
                });
            }
        }

        private static void ScanFirefoxProfiles(Browser browser)
        {
            string profilesIniPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mozilla\\Firefox\\profiles.ini");
            if (!File.Exists(profilesIniPath)) return;

            try
            {
                string[] lines = File.ReadAllLines(profilesIniPath);
                string currentProfilePath = null;
                string currentProfileName = null;

                foreach (string line in lines)
                {
                    if (line.StartsWith("[Profile"))
                    {
                        if (currentProfilePath != null && currentProfileName != null)
                        {
                            browser.Profiles.Add(new BrowserProfile
                            {
                                Id = currentProfileName,
                                DisplayName = currentProfileName,
                                IconPath = browser.ExecutablePath
                            });
                        }
                        currentProfilePath = null;
                        currentProfileName = null;
                    }
                    else if (line.StartsWith("Path="))
                    {
                        currentProfilePath = line.Substring("Path=".Length);
                    }
                    else if (line.StartsWith("Name="))
                    {
                        currentProfileName = line.Substring("Name=".Length);
                    }
                }
                if (currentProfilePath != null && currentProfileName != null)
                {
                    browser.Profiles.Add(new BrowserProfile
                    {
                        Id = currentProfileName,
                        DisplayName = currentProfileName,
                        IconPath = browser.ExecutablePath
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading Firefox profiles.ini: {ex.Message}");
            }
        }


        private static async Task StartPipeServer()
        {
            while (true)
            {
                try
                {
                    using (var pipeServer = new NamedPipeServerStream(PipeName, PipeDirection.In, 1))
                    {
                        Console.WriteLine("Waiting for client connection...");
                        await pipeServer.WaitForConnectionAsync();
                        Console.WriteLine("Client connected.");

                        using (var reader = new StreamReader(pipeServer))
                        {
                            string url = await reader.ReadLineAsync();
                            Console.WriteLine($"Received URL: {url}");

                            _wpfApp.Dispatcher.Invoke(() =>
                            {
                                var pickerWindow = new MainWindow(url, _appSettings);
                                pickerWindow.ShowDialog();
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Pipe server error: {ex.Message}");
                    await Task.Delay(1000);
                }
            }
        }
    }
}