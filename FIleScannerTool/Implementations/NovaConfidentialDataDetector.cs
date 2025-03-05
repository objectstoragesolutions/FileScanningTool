using FIleScannerTool.Documents;
using FIleScannerTool.Interfaces;

using LLMApiModels;

using Microsoft.Extensions.Configuration;

namespace FIleScannerTool.Implementations;

internal class NovaConfidentialDataDetector : IConfidentialDataDetector
{
    private readonly ILLMClient _llmClient;
    private readonly IConfiguration _configuration;

    private string _instruction { get; set; } = "Please check the document for confidential information. Response only 'True' or 'False' without any details.";
    private bool _showTrace;

    private void WriteTrace(string message)
    {
        if (_showTrace)
        {
            Console.WriteLine($"{DateTime.Now} NovaConfidentialDataDetector: {message}");
        }
    }

    public NovaConfidentialDataDetector(ILLMClient llmClient, IConfiguration configuration)
    {
        _llmClient = llmClient;
        _configuration = configuration;
        bool.TryParse(configuration["ShowTrace"], out _showTrace);

        IConfigurationSection novaConfig = configuration.GetSection(key: "Nova");
        if (novaConfig != null)
        {
            string? configInstruction = novaConfig[key: "Instruction"];
            if (!string.IsNullOrEmpty(configInstruction))
            {
                _instruction = configInstruction;
            }
        }

        WriteTrace($"Initialized with instruction: {_instruction}");
    }

    private string GetContentType(string fileKey)
    {
        string extension = Path.GetExtension(fileKey);
        return extension switch
        {
            ".doc" => "application/msword",
            ".xls" => "application/vnd.ms-excel",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".txt" or ".json" or ".jsonl" => "text/plain",
            ".html" or ".htm" => "text/html",
            ".csv" => "text/csv",
            ".pdf" => "application/pdf",
            _ => "application/pdf",
        };
    }

    bool ConvertToTextFileIfNecessary(byte[] bytes, string fileKey, out byte[] textFileBytes)
    {
        string extension = Path.GetExtension(path: fileKey);
        OpenOfficeContentExtractor contentExtractor = new(configuration: _configuration);
        switch (extension)
        {
            case ".docx":
                textFileBytes = contentExtractor.ExtractTextFromDocxBytes(bytes);
                return true;
            case ".xlsx":
                textFileBytes = contentExtractor.ExtractTextFromXlsxBytes(bytes);
                return true;
            default:
                textFileBytes = new byte[0];
                return false;
        }
    }

    public async Task<string> IsContainsConfidentialAsync(byte[] fileBytes, string fileKey)
    {
        WriteTrace($"Processing file: {fileKey}");

        if (ConvertToTextFileIfNecessary(fileBytes, fileKey, out byte[] textFileBytes))
        {
            fileBytes = textFileBytes;
            fileKey = fileKey.Replace(Path.GetExtension(fileKey), ".txt");
        }

        LLMApiRequest llmApiRequest = new();

        LLMApiRequestMessage userMessage = new(RequestMessageRoles.User)
        {
            Content = new LLMApiRequestMessageContent()
            {
                Texts = new List<string>() { _instruction }
            }
        };

        userMessage.Content.Images = new List<FileData>()
        {
            new()
            {
                Data = fileBytes,
                ContentType = GetContentType(fileKey)
            }
        };

        llmApiRequest.Messages.Add(userMessage);

        LLMApiResponse llmApiResponse = await _llmClient.CallAsync(llmApiRequest);

        string textResponse = llmApiResponse.Messages.FirstOrDefault() ?? "Null";

        if (string.IsNullOrEmpty(textResponse))
        {
            textResponse = "False";
        }

        WriteTrace($"Received response: {textResponse} for file: {fileKey}");
        if (llmApiResponse.Messages.Count > 1)
        {
            WriteTrace($"Received exception: {textResponse} for file: {fileKey}");
        }

        if (bool.TryParse(textResponse, out bool hasConfidentialInformation))
        {
            return hasConfidentialInformation.ToString();
        }

        return textResponse;
    }
}
