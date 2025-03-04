using System.Threading.Tasks;

namespace LLMApiModels
{
    public interface ILLMClient
    {
        bool WorkingWithImages { get; }

        bool WorkingWithDocuments { get; }

        Task<LLMApiResponse> CallAsync(LLMApiRequest request);
    }
}
