using System;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;

namespace Launcher
{
    class Program
    {
        private const string PipeName = "BrowserProfilePickerPipe";
        private const string BackgroundRunnerExeName = "BackgroundRunner.exe";

        static async Task Main(string[] args)
        {
            string url = null;
            if (args.Length > 0)
            {
                url = args[0];
            }
            else
            {
                Console.WriteLine("Launcher started without URL. This usually happens if not invoked by OS directly.");
            }

            if (string.IsNullOrEmpty(url))
            {
                Console.WriteLine("No URL to process. Exiting Launcher.");
                return;
            }

            if (!IsBackgroundRunnerRunning())
            {
                Console.WriteLine("BackgroundRunner not detected. Attempting to start it...");
                StartBackgroundRunner();
                await Task.Delay(2000);
            }

            bool sent = await SendUrlToBackgroundRunner(url);

            if (!sent)
            {
                Console.WriteLine("Failed to send URL to BackgroundRunner. It might not be running or pipe is busy.");
            }

            Console.WriteLine("Launcher finished.");
        }

        private static bool IsBackgroundRunnerRunning()
        {
            return Process.GetProcessesByName(Path.GetFileNameWithoutExtension(BackgroundRunnerExeName)).Any();
        }

        private static void StartBackgroundRunner()
        {
            try
            {
                string launcherDir = AppContext.BaseDirectory;
                string backgroundRunnerPath = Path.Combine(launcherDir, BackgroundRunnerExeName);

                if (File.Exists(backgroundRunnerPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = backgroundRunnerPath,
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WorkingDirectory = launcherDir
                    });
                    Console.WriteLine($"Started {BackgroundRunnerExeName}");
                }
                else
                {
                    Console.WriteLine($"Error: {BackgroundRunnerExeName} not found at {backgroundRunnerPath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting BackgroundRunner: {ex.Message}");
            }
        }

        private static async Task<bool> SendUrlToBackgroundRunner(string url)
        {
            try
            {
                using (var pipeClient = new NamedPipeClientStream(".", PipeName, PipeDirection.Out))
                {
                    await pipeClient.ConnectAsync(1500);
                    if (!pipeClient.IsConnected)
                    {
                        Console.WriteLine("Could not connect to BackgroundRunner pipe within timeout.");
                        return false;
                    }

                    using (var writer = new StreamWriter(pipeClient))
                    {
                        await writer.WriteLineAsync(url);
                        await writer.FlushAsync();
                    }
                    Console.WriteLine("URL sent to BackgroundRunner.");
                    return true;
                }
            }
            catch (TimeoutException)
            {
                Console.WriteLine("Timeout connecting to BackgroundRunner pipe.");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending URL via pipe: {ex.Message}");
                return false;
            }
        }
    }
}