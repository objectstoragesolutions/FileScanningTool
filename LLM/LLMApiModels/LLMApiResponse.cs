using Newtonsoft.Json;

using System.Collections.Generic;

namespace LLMApiModels
{
    public class LLMApiResponse
    {
        public LLMApiResponse()
        {
            Messages = new List<string>();
        }

        public LLMApiResponse(string message)
        {
            Messages = new List<string>() { message };
        }

        public string FullMessage => string.Join("\n", Messages);

        public List<string> Messages { get; set; }

        public List<string> GetJsonBlocks()
        {
            var jsonBlocks = new List<string>();
            foreach (string message in Messages)
            {
                string messageCopy = message.ToString();

                while (messageCopy.Contains("```json"))
                {
                    int start = messageCopy.IndexOf("```json");

                    messageCopy = messageCopy.Substring(startIndex: start + 7);

                    int end = messageCopy.IndexOf("```");

                    string jsonBlock = messageCopy.Substring(startIndex: 0, length: end);
                    jsonBlocks.Add(jsonBlock);

                    messageCopy = messageCopy.Substring(startIndex: end + 3);
                }
            }

            if (jsonBlocks.Count == 1)
            {
                try
                {
                    List<object> dynamicArray = JsonConvert.DeserializeObject<List<object>>(jsonBlocks[0]);

                    if (dynamicArray.Count > 1)
                    {
                        jsonBlocks.Clear();
                        foreach (object item in dynamicArray)
                        {
                            jsonBlocks.Add(item.ToString());
                        }
                    }
                }
                catch
                {

                }
            }

            return jsonBlocks;
        }
    }
}
