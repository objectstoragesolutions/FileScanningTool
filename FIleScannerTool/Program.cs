using AWSSDK.Extensions.CrtIntegration;

using Microsoft.Extensions.Configuration;

namespace FIleScannerTool;

internal class Program
{
    static async Task Main(string[] args)
    {
        // Register the checksum provider for Native AOT builds.
        Amazon.RuntimeDependencies.GlobalRuntimeDependencyRegistry.Instance.RegisterChecksumProvider(new CrtChecksums());

        Console.WriteLine($"{DateTime.Now}: Start the process.");
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddJsonFile(path: "appsettings.json", optional: false, reloadOnChange: true)
            .AddUserSecrets<Program>()
            .Build();

        Console.WriteLine($"{DateTime.Now}: Start FileScanner initialization.");
        FileScanner fileScanner = new(configuration);
        CancellationTokenSource _cancellationTokenSource = new();

        Console.CancelKeyPress += (sender, eventArgs) =>
        {
            Console.WriteLine($"{DateTime.Now}: Ctrl+C pressed. Cancelling...");
            _cancellationTokenSource.Cancel();
            eventArgs.Cancel = true;
        };

        Thread exitThread = new(start: () =>
        {
            while (true)
            {
                string? input = Console.ReadLine();
                if (!string.IsNullOrEmpty(input) && input.Equals(value: "exit", comparisonType: StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"{DateTime.Now}: Exit command received. Cancelling...");
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
            Console.WriteLine($"{DateTime.Now}: Operation was cancelled.");
        }

        Console.WriteLine($"{DateTime.Now}: Processing complete.");
        exitThread.Join();
        Console.WriteLine($"{DateTime.Now}: Program exiting.");
    }
}
