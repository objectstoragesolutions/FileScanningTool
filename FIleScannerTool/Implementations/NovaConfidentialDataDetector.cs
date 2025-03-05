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
        var extension = Path.GetExtension(fileKey);
        switch (extension)
        {
            case ".doc":
                return "application/msword";
            case ".xls":
                return "application/vnd.ms-excel";
            case ".docx":
                return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
            case ".xlsx":
                return "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            case ".txt":
            case ".json":
            case ".jsonl":
                return "text/plain";
            case ".html":
            case ".htm":
                return "text/html";
            case ".csv":
                return "text/csv";
            case ".pdf":
                return "application/pdf";
            default:
                return "application/pdf";
        }
    }


    bool ConvertToTextFileIfNecessary(byte[] bytes, string fileKey, out byte[] textFileBytes)
    {
        var extension = Path.GetExtension(fileKey);
        OpenOfficeContentExtractor contentExtractor = new OpenOfficeContentExtractor(_configuration);
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

        if(ConvertToTextFileIfNecessary(fileBytes, fileKey, out byte[] textFileBytes))
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
            new FileData()
            {
                Data = fileBytes,
                ContentType = GetContentType(fileKey)
            }
        };

        llmApiRequest.Messages.Add(userMessage);

        LLMApiResponse llmApiResponse = await _llmClient.CallAsync(llmApiRequest);

        string textResponse = llmApiResponse.Messages.FirstOrDefault();

        if (string.IsNullOrEmpty(textResponse))
        {
            textResponse = "False";
        }

        WriteTrace($"Received response: {textResponse} for file: {fileKey}");
        if (llmApiResponse.Messages.Count > 1)
        {
            WriteTrace($"Received exception: {textResponse} for file: {fileKey}");
        }

        var words = textResponse
            .Replace("\r\n", " ")
            .Replace("\n", " ")
            .Split(" ").ToList();
        if (words.Count > 1)
        {
            textResponse = words[0];
        }

        if (bool.TryParse(textResponse, out bool hasConfidentialInformation))
        {
            return hasConfidentialInformation.ToString();
        }

        return textResponse;
    }
}
