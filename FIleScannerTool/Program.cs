using Microsoft.Extensions.Configuration;

namespace FIleScannerTool
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
             .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
             .AddUserSecrets<Program>()
             .Build();

            FileScanner fileScanner = new FileScanner(configuration);
            CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                Console.WriteLine("Ctrl+C pressed.  Cancelling...");
                _cancellationTokenSource.Cancel();
                eventArgs.Cancel = true;
            };


            var exitThread = new Thread(() =>
            {
                while (true)
                {
                    string? input = Console.ReadLine();
                    if (!string.IsNullOrEmpty(input) && input.Equals("exit", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("Exit command received.  Cancelling...");
                        _cancellationTokenSource.Cancel();
                        break;
                    }
                }
            });
            exitThread.Start();

            try
            {
                await fileScanner.ProcessFiles(_cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Operation was cancelled.");
            }

            Console.WriteLine("Processing complete.");
            exitThread.Join();
        }
    }
}
