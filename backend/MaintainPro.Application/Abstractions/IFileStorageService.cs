namespace MaintainPro.Application.Abstractions;

/// <summary>Stores private file bytes; callers persist metadata and enforce record authorization.</summary>
public interface IFileStorageService
{
    Task<StoredFile> StoreAsync(Stream content, string originalFilename, string mimeType,
        long declaredLength, CancellationToken ct = default);
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default);
    Task DeleteAsync(string storageKey, CancellationToken ct = default);
}

public sealed record StoredFile(string StorageKey, string OriginalFilename, string MimeType, long FileSize);
