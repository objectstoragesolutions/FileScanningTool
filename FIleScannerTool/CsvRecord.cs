using System.Diagnostics.CodeAnalysis;

namespace FIleScannerTool;

[DynamicallyAccessedMembers(memberTypes: DynamicallyAccessedMemberTypes.PublicProperties)]
public class CsvRecord
{
    public string FilePath { get; set; } = string.Empty;
    public string ContainsConfidentialInformation { get; set; } = string.Empty;
}
