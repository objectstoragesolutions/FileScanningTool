using Amazon.Runtime;
using Interfaces;

namespace FIleScannerTool.Implementations
{
    internal class AWSCredentials: IAWSCredentials
    {
        public bool AwsS3RegionLogin { get; set; }
        public string AwsRegion { get; set; }
        public string AwsAccessKey { get; set; }
        public string AwsSecretAccessKey { get; set; }
    }
}
