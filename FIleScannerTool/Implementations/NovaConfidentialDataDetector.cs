using FIleScannerTool.Interfaces;

using LLMApiModels;

using Microsoft.Extensions.Configuration;

namespace FIleScannerTool.Implementations;

internal class NovaConfidentialDataDetector : IConfidentialDataDetector
{
    private readonly ILLMClient _llmClient;
    private string _instruction { get; set; } = "Please check the document for confidential information. Response only True or False.";

    public NovaConfidentialDataDetector(ILLMClient llmClient, IConfiguration configuration)
    {
        _llmClient = llmClient;

        IConfigurationSection novaConfig = configuration.GetSection(key: "Nova");
        if (novaConfig != null)
        {
            string? configInstruction = novaConfig[key: "Instruction"];
            if (!string.IsNullOrEmpty(configInstruction))
            {
                _instruction = configInstruction;
            }
        }

        Console.WriteLine($"{DateTime.Now}: NovaConfidentialDataDetector initialized with instruction: {_instruction}");
    }

    private string GetContentType(string fileKey)
    {
        return "application/pdf";
    }

    public async Task<string> IsContainsConfidentialAsync(byte[] fileBytes, string fileKey)
    {
        Console.WriteLine($"{DateTime.Now}: NovaConfidentialDataDetector processing file: {fileKey}");

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

        string textResponse = llmApiResponse.FullMessage;
        Console.WriteLine($"{DateTime.Now}: NovaConfidentialDataDetector received response: {textResponse} for file: {fileKey}");

        if (bool.TryParse(textResponse, out bool hasConfidentialInformation))
        {
            return hasConfidentialInformation.ToString();
        }

        return textResponse;
    }
}
