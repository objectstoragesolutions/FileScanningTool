using System.Collections.Generic;

namespace LLMApiModels
{
    public class LLMApiRequestMessageContent
    {
        public LLMApiRequestMessageContent()
        {
            Texts = new List<string>();
            Images = new List<FileData>();
        }

        public List<string> Texts { get; set; }

        public List<FileData> Images { get; set; }
    }
}
