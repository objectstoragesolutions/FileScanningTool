namespace LLMApiModels
{
    public class FileData
    {
        public FileData()
        {
            Detail = FileDataDetails.High;
        }
        public string ContentType { get; set; }
        public byte[] Data { get; set; }
        public FileDataDetails Detail { get; set; }
    }
}
