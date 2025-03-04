namespace FIleScannerTool.Interfaces;

internal interface IConfidentialDataDetector
{
    Task<string> IsContainsConfidentialAsync(byte[] fileBytes, string fileKey);
}
