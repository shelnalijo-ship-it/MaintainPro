namespace MaintainPro.Infrastructure.Files;

public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";
    public string RootDirectory { get; set; } = ".maintainpro-storage";
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;
}
