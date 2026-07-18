namespace SmartRentalPlatform.Infrastructure.Options;

public sealed class S3StorageOptions
{
    public const string SectionPath = "Aws:S3";

    public string AccessKeyId { get; set; } = string.Empty;

    public string SecretAccessKey { get; set; } = string.Empty;

    public string Region { get; set; } = string.Empty;

    public string BucketName { get; set; } = string.Empty;
}
