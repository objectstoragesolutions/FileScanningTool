using Amazon;
using Amazon.S3;
using Amazon.S3.Model;

using AmazonNova;

using CsvHelper;

using FIleScannerTool.Implementations;
using FIleScannerTool.Interfaces;

using LLMApiModels;

using Microsoft.Extensions.Configuration;

using System.Globalization;
using System.Text;
using System.Threading.Tasks.Dataflow;

namespace FIleScannerTool;

internal class FileScanner
{
    private readonly IConfidentialDataDetector _confidentialDataDetector;
    private readonly string _outputCsvFilePath;
    private HashSet<string> _processedFiles;
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;

    private const int _maxDegreeOfParallelism = 4;

    public FileScanner(IConfiguration configuration)
    {
        Console.WriteLine($"{DateTime.Now}: Initializing FileScanner...");
        IConfigurationSection awsConfig = configuration.GetSection(key: "AWS");

        _bucketName = awsConfig[key: "BucketName"] ?? throw new InvalidOperationException(message: "BucketName is required in appsettings.json");
        Console.WriteLine($"{DateTime.Now}: BucketName: {_bucketName}.");

        _outputCsvFilePath = configuration[key: "OutputCsvFilePath"] ?? "results.csv";
        Console.WriteLine($"{DateTime.Now}: OutputCsvFilePath: {_outputCsvFilePath}.");

        AWSCredentials awsCredentials = new()
        {
            AwsRegion = awsConfig[key: "Region"],
            AwsAccessKey = configuration[key: "AwsAccessKeyId"],
            AwsSecretAccessKey = configuration[key: "AwsSecretAccessKey"],
            AwsS3RegionLogin = !bool.Parse(awsConfig[key: "UseAwsAccessKey"])
        };
        Console.WriteLine($"{DateTime.Now}: AwsRegion: {awsCredentials.AwsRegion}.");

        if (awsCredentials.AwsS3RegionLogin)
        {
            _s3Client = new AmazonS3Client(RegionEndpoint.GetBySystemName(systemName: awsCredentials.AwsRegion));
        }
        else
        {
            _s3Client = new AmazonS3Client(
                awsAccessKeyId: awsCredentials.AwsAccessKey,
                awsSecretAccessKey: awsCredentials.AwsSecretAccessKey,
                region: RegionEndpoint.GetBySystemName(systemName: awsCredentials.AwsRegion));
        }
        Console.WriteLine($"{DateTime.Now}: Amazon S3 client initialized.");

        Console.WriteLine($"{DateTime.Now}: Start AmazonNovaClient initialization.");
        ILLMClient llmClient = new AmazonNovaClient(awsCredentials);
        Console.WriteLine($"{DateTime.Now}: AmazonNovaClient initialized.");

        Console.WriteLine($"{DateTime.Now}: Start NovaConfidentialDataDetector initialization.");
        _confidentialDataDetector = new NovaConfidentialDataDetector(llmClient, configuration);
        Console.WriteLine($"{DateTime.Now}: NovaConfidentialDataDetector initialized.");
    }

    public async Task ProcessFiles(CancellationToken cancellationToken)
    {
        Console.WriteLine($"{DateTime.Now}: Loading processed files.");
        _processedFiles = LoadProcessedFiles();

        Console.WriteLine($"{DateTime.Now}: Starting to process S3 files.");
        await ProcessS3FilesAsync(cancellationToken);
        Console.WriteLine($"{DateTime.Now}: Finished processing S3 files.");
    }

    private async Task ProcessS3FilesAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine($"{DateTime.Now}: Entering ProcessS3FilesAsync function.");

        TransformBlock<string, ListObjectsV2Response> listObjectsBlock = new(
            transform: async bucket =>
            {
                Console.WriteLine($"{DateTime.Now}: Listing objects in bucket: {bucket}.");
                cancellationToken.ThrowIfCancellationRequested();
                ListObjectsV2Response response = await _s3Client.ListObjectsV2Async(request: new ListObjectsV2Request
                {
                    BucketName = bucket
                });
                Console.WriteLine($"{DateTime.Now}: Retrieved {response.S3Objects.Count} objects from bucket.");

                return response;
            },
            dataflowBlockOptions: new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = _maxDegreeOfParallelism
            }
        );

        TransformManyBlock<ListObjectsV2Response, S3Object> filterProcessedBlock = new(transform: response =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            IEnumerable<S3Object> filtered = response.S3Objects.Where(s3Object => !_processedFiles.Contains(s3Object.Key));
            Console.WriteLine($"{DateTime.Now}: Filtered objects: {filtered.Count()} objects to process.");

            return filtered;
        });

        TransformBlock<S3Object, (string FilePath, string NovaResult)> processFileBlock = new(
            transform: async s3Object =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                Console.WriteLine($"{DateTime.Now}: Processing file: {s3Object.Key}.");

                string fileResult = "false";

                if (!s3Object.Key.EndsWith("/"))
                {
                    try
                    {
                        using GetObjectResponse getObjectResponse = await _s3Client.GetObjectAsync(bucketName: _bucketName, key: s3Object.Key);
                        using MemoryStream memoryStream = new();
                        await getObjectResponse.ResponseStream.CopyToAsync(destination: memoryStream);
                        byte[] fileBytes = memoryStream.ToArray();
                        Console.WriteLine($"{DateTime.Now}: File {s3Object.Key} downloaded successfully. Size: {fileBytes.Length} bytes.");

                        fileResult = await SendToNovaAsync(fileBytes, fileKey: s3Object.Key);
                        Console.WriteLine($"{DateTime.Now}: Nova result for file {s3Object.Key}: {fileResult}.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"{DateTime.Now}: Error downloading file {s3Object.Key}: {ex}. With message: {ex.Message}.");
                    }
                }
                else
                {
                    Console.WriteLine($"{DateTime.Now}: Skipping directory: {s3Object.Key}.");
                }

                return (FilePath: s3Object.Key, NovaResult: fileResult);
            },
            dataflowBlockOptions: new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = _maxDegreeOfParallelism });

        ActionBlock<(string FilePath, string NovaResult)> writeToCsvBlock = new(
            action: async result =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                await WriteResultToCsvAsync(result.FilePath, result.NovaResult);
                _processedFiles.Add(result.FilePath);
                Console.WriteLine($"{DateTime.Now}: Processed file: {result.FilePath}, Nova result: {result.NovaResult}.");
            },
            dataflowBlockOptions: new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 1 });

        listObjectsBlock.LinkTo(target: filterProcessedBlock, linkOptions: new DataflowLinkOptions { PropagateCompletion = true });
        filterProcessedBlock.LinkTo(target: processFileBlock, linkOptions: new DataflowLinkOptions { PropagateCompletion = true });
        processFileBlock.LinkTo(target: writeToCsvBlock, linkOptions: new DataflowLinkOptions { PropagateCompletion = true });

        listObjectsBlock.Post(item: _bucketName);
        listObjectsBlock.Complete();

        await writeToCsvBlock.Completion;
    }

    private async Task<string> SendToNovaAsync(byte[] fileBytes, string fileKey)
    {
        try
        {
            Console.WriteLine($"{DateTime.Now}: Sending file {fileKey} to Nova for confidential check.");
            string result = await _confidentialDataDetector.IsContainsConfidentialAsync(fileBytes, fileKey);
            Console.WriteLine($"{DateTime.Now}: Received response from Nova for file {fileKey}: {result}.");

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{DateTime.Now}: Error communicating with Nova for {fileKey}: {ex.Message}");

            return "error";
        }
    }

    private async Task WriteResultToCsvAsync(string filePath, string novaResult)
    {
        Console.WriteLine($"{DateTime.Now}: Writing result for file {filePath} to CSV.");

        CsvRecord record = new() { FilePath = filePath, ContainsConfidentialInformation = novaResult };

        bool fileExist = File.Exists(_outputCsvFilePath);

        using FileStream stream = new(
            path: _outputCsvFilePath,
            mode: FileMode.Append,
            access: FileAccess.Write,
            share: FileShare.ReadWrite);
        using StreamWriter writer = new(stream, encoding: Encoding.UTF8);
        using CsvWriter csv = new(writer, culture: CultureInfo.InvariantCulture);
        csv.Context.RegisterClassMap<CsvRecordMap>(); // Register the explicit mapping

        if (!fileExist)
        {
            csv.WriteHeader<CsvRecord>();
            await csv.NextRecordAsync();
            Console.WriteLine($"{DateTime.Now}: CSV header written.");
        }

        await csv.WriteRecordsAsync(records: new List<CsvRecord> { record });
        Console.WriteLine($"{DateTime.Now}: Record for {filePath} written to CSV.");
    }

    private HashSet<string> LoadProcessedFiles()
    {
        Console.WriteLine($"{DateTime.Now}: Starting LoadProcessedFiles function.");
        bool exist = File.Exists(path: _outputCsvFilePath);
        Console.WriteLine($"{DateTime.Now}: CSV file exists: {exist}.");

        HashSet<string> processedFiles = new();
        if (exist)
        {
            using StreamReader reader = new(path: _outputCsvFilePath);
            using CsvReader csv = new(reader, culture: CultureInfo.InvariantCulture);
            csv.Read();
            csv.ReadHeader();

            while (csv.Read())
            {
                try
                {
                    string filePath = csv.GetField("FilePath") ?? string.Empty;
                    processedFiles.Add(item: filePath);
                }
                catch (CsvHelper.MissingFieldException)
                {
                    Console.WriteLine($"{DateTime.Now}: Warning: A row in the CSV is missing the 'FilePath' field.");
                }
            }
        }

        Console.WriteLine($"{DateTime.Now}: Loaded {processedFiles.Count} processed files.");

        return processedFiles;
    }
}
