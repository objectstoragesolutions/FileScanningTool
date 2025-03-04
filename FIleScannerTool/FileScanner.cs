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

namespace FIleScannerTool
{
    internal class FileScanner
    {
        private const int _maxDegreeOfParallelism = 4;

        private readonly IAmazonS3 _s3Client;
        private readonly string _bucketName;
        private readonly string _outputCsvFilePath;
        private HashSet<string> _processedFiles;

        private IConfidentialDataDetector _confidentialDataDetector;
        public FileScanner(IConfiguration configuration)
        {
            var awsConfig = configuration.GetSection("AWS");
            _bucketName = awsConfig["BucketName"] ?? throw new InvalidOperationException("BucketName is required in appsettings.json");

            _outputCsvFilePath = configuration["OutputCsvFilePath"] ?? "results.csv";

            var awsCredentials = new AWSCredentials()
            {
                AwsRegion = awsConfig["Region"],
                AwsAccessKey = configuration["AwsAccessKeyId"],
                AwsSecretAccessKey = configuration["AwsSecretAccessKey"],
                AwsS3RegionLogin = !bool.Parse(awsConfig["UseAwsAccessKey"])
            };



            if (awsCredentials.AwsS3RegionLogin)
            {
                _s3Client = new AmazonS3Client(RegionEndpoint.GetBySystemName(awsCredentials.AwsRegion));
            }
            else
            {
                _s3Client = new AmazonS3Client(awsCredentials.AwsAccessKey, awsCredentials.AwsSecretAccessKey, RegionEndpoint.GetBySystemName(awsCredentials.AwsRegion));
            }

            ILLMClient llmClient = new AmazonNovaClient(awsCredentials);
            _confidentialDataDetector = new NovaConfidentialDataDetector(llmClient, configuration);
        }

        public async Task ProcessFiles(CancellationToken cancellationToken)
        {
            _processedFiles = LoadProcessedFiles();

            // Create the dataflow pipeline
            await ProcessS3FilesAsync(cancellationToken);
        }

        private async Task ProcessS3FilesAsync(CancellationToken cancellationToken)
        {

            var listObjectsBlock = new TransformBlock<string, ListObjectsV2Response>(
                async bucket =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        return await _s3Client.ListObjectsV2Async(new ListObjectsV2Request
                        {
                            BucketName = bucket,
                        });
                    },
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = _maxDegreeOfParallelism
                }
            );

            var filterProcessedBlock = new TransformManyBlock<ListObjectsV2Response, S3Object>(response =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return response.S3Objects.Where(s3Object => !_processedFiles.Contains(s3Object.Key));
            });

            var processFileBlock = new TransformBlock<S3Object, (string FilePath, string NovaResult)>(async s3Object =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                string fileResult = "false";

                if (!s3Object.Key.EndsWith("/"))
                {
                    try
                    {
                        using var getObjectResponse = await _s3Client.GetObjectAsync(_bucketName, s3Object.Key);
                        using var memoryStream = new MemoryStream();
                        await getObjectResponse.ResponseStream.CopyToAsync(memoryStream);
                        byte[] fileBytes = memoryStream.ToArray();

                        fileResult = await SendToNovaAsync(fileBytes, s3Object.Key);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error downloading file {s3Object.Key}: {ex.Message}");
                    }
                }

                return (FilePath: s3Object.Key, NovaResult: fileResult);
            }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = _maxDegreeOfParallelism });


            var writeToCsvBlock = new ActionBlock<(string FilePath, string NovaResult)>(async result =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                await WriteResultToCsvAsync(result.FilePath, result.NovaResult);
                _processedFiles.Add(result.FilePath);
                Console.WriteLine($"Processed: {result.FilePath}, Result: {result.NovaResult}");
            }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 1 });

            listObjectsBlock.LinkTo(filterProcessedBlock, new DataflowLinkOptions { PropagateCompletion = true });
            filterProcessedBlock.LinkTo(processFileBlock, new DataflowLinkOptions { PropagateCompletion = true });
            processFileBlock.LinkTo(writeToCsvBlock, new DataflowLinkOptions { PropagateCompletion = true });

            listObjectsBlock.Post(_bucketName);
            listObjectsBlock.Complete();

            await writeToCsvBlock.Completion;
        }

        private async Task<string> SendToNovaAsync(byte[] fileBytes, string fileKey)
        {
            try
            {
                return await _confidentialDataDetector.IsContainsConfidentialAsync(fileBytes, fileKey);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error communicating with Nova for {fileKey}: {ex.Message}");
                return "error";
            }
        }

        private async Task WriteResultToCsvAsync(string filePath, string novaResult)
        {
            var record = new { FilePath = filePath, ContainsConfidentialInformation = novaResult };
            var records = new List<dynamic> { record };

            bool fileExist = File.Exists(_outputCsvFilePath);

            using var stream = new FileStream(_outputCsvFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            using var writer = new StreamWriter(stream, Encoding.UTF8);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

            if (!fileExist)
            {
                csv.WriteHeader<dynamic>();
                await csv.NextRecordAsync();
            }

            await csv.WriteRecordsAsync(records);
        }

        private HashSet<string> LoadProcessedFiles()
        {
            var processedFiles = new HashSet<string>();
            if (File.Exists(_outputCsvFilePath))
            {
                using var reader = new StreamReader(_outputCsvFilePath);
                using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
                csv.Read();
                csv.ReadHeader();

                while (csv.Read())
                {
                    try
                    {
                        processedFiles.Add(csv.GetField("FilePath") ?? string.Empty);
                    }
                    catch (CsvHelper.MissingFieldException)
                    {
                        Console.WriteLine("Warning: A row in the CSV is missing the 'FilePath' field.");
                    }
                }
            }
            return processedFiles;
        }
    }
}
