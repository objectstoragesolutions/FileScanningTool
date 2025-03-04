using System.Collections.Generic;

namespace LLMApiModels
{
    public class LLMApiRequest
    {
        public LLMApiRequest()
        {
            Messages = new List<LLMApiRequestMessage>();
        }

        public string Model { get; set; }

        public List<LLMApiRequestMessage> Messages { get; set; }
    }
}
