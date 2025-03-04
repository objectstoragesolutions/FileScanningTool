namespace Interfaces
{
    public interface IAWSCredentials
    {
        bool AwsS3RegionLogin { get; }
        string AwsRegion { get; }
        string AwsAccessKey { get; }
        string AwsSecretAccessKey { get; }
    }
}
