using FIleScannerTool.Interfaces;
using LLMApiModels;
using Microsoft.Extensions.Configuration;

namespace FIleScannerTool.Implementations
{
    internal class NovaConfidentialDataDetector : IConfidentialDataDetector
    {
        private readonly ILLMClient llmClient;

        public NovaConfidentialDataDetector(ILLMClient llmClient, IConfiguration configuration)
        {
            this.llmClient = llmClient;

            var novaConfig = configuration.GetSection("Nova");
            if (novaConfig != null)
            {
                var configInstruction = novaConfig["Instruction"];
                if (!string.IsNullOrEmpty(configInstruction))
                {
                    _instruction = configInstruction;
                }
            }
        }

        private string _instruction { get; set; } = "Please check the document for confidential information. Response only True or False.";

        private string GetContentType(string fileKey)
        {
            return "application/pdf";
        }

        public async Task<string> IsContainsConfidentialAsync(byte[] fileBytes, string fileKey)
        {
            LLMApiRequest llmApiRequest = new LLMApiRequest();

            var userMessage = new LLMApiRequestMessage(RequestMessageRoles.User)
            {
                Content = new LLMApiRequestMessageContent()
                {
                    Texts = new List<string>() { _instruction }
                }
            };

            userMessage.Content.Images = new List<FileData>() {
                new FileData()
                {
                    Data = fileBytes,
                    ContentType = GetContentType(fileKey)
                }
            };

            llmApiRequest.Messages.Add(userMessage);

            LLMApiResponse llmApiResponse = await llmClient.CallAsync(llmApiRequest);

            string textResponse = llmApiResponse.FullMessage;

            if (bool.TryParse(textResponse, out bool hasConfidentialInformation))
            {
                return hasConfidentialInformation.ToString();
            }
            else
            {
                return textResponse;
            }
        }
    }
}
