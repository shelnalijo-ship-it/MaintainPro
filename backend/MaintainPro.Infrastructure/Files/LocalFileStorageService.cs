using System.Text;
using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using Microsoft.Extensions.Options;

namespace MaintainPro.Infrastructure.Files;

/// <summary>Private development storage. No filename supplied by a client becomes a physical path.</summary>
public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly string rootDirectory;
    private readonly long maximumSize;

    public LocalFileStorageService(IOptions<FileStorageOptions> options)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.RootDirectory))
            throw new InvalidOperationException("FileStorage:RootDirectory must be configured.");
        if (settings.MaxFileSizeBytes is < 1 or > 100 * 1024 * 1024)
            throw new InvalidOperationException("FileStorage:MaxFileSizeBytes must be between 1 and 104857600.");
        rootDirectory = Path.GetFullPath(settings.RootDirectory);
        maximumSize = settings.MaxFileSizeBytes;
    }

    public async Task<StoredFile> StoreAsync(Stream content, string originalFilename, string mimeType,
        long declaredLength, CancellationToken ct = default)
    {
        var filename = Guard.Required(originalFilename, "OriginalFilename", 255);
        if (filename.IndexOfAny(['/', '\\', ':']) >= 0 || filename.Any(char.IsControl) || filename is "." or "..")
            throw new AppException(400, "OriginalFilename must be a filename without a path or control characters.");
        var normalizedMime = Guard.Required(mimeType, "MimeType", 100).ToLowerInvariant();
        var extension = Path.GetExtension(filename).ToLowerInvariant();
        var expectedMime = extension switch { ".jpg" or ".jpeg" => "image/jpeg", ".png" => "image/png", ".pdf" => "application/pdf", _ => null };
        if (expectedMime is null || normalizedMime != expectedMime)
            throw new AppException(400, "Evidence must be a JPG, JPEG, PNG, or PDF with a matching MIME type.");
        if (declaredLength <= 0) throw new AppException(400, "The uploaded file is empty.");
        if (declaredLength > maximumSize) throw new AppException(413, $"The uploaded file exceeds the {maximumSize}-byte limit.");

        // Count bytes ourselves: multipart metadata and stream lengths are untrusted.
        await using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        while (true)
        {
            var read = await content.ReadAsync(chunk, ct);
            if (read == 0) break;
            if (buffer.Length + read > maximumSize)
                throw new AppException(413, $"The uploaded file exceeds the {maximumSize}-byte limit.");
            await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
        }
        if (buffer.Length != declaredLength) throw new AppException(400, "Uploaded file size does not match its metadata.");
        if (!HasExpectedStructure(buffer.GetBuffer().AsSpan(0, checked((int)buffer.Length)), normalizedMime))
            throw new AppException(400, "Uploaded file content does not match its declared format.");
        var key = Guid.NewGuid().ToString("N");
        var path = ResolveKey(key);
        Directory.CreateDirectory(rootDirectory);
        var created = false;
        try
        {
            await using (var destination = new FileStream(path, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 81920, FileOptions.Asynchronous))
            {
                created = true;
                buffer.Position = 0;
                await buffer.CopyToAsync(destination, ct);
                await destination.FlushAsync(ct);
            }
            return new(key, filename, normalizedMime, buffer.Length);
        }
        catch
        {
            if (created) File.Delete(path);
            throw;
        }
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        Stream stream = new FileStream(ResolveKey(storageKey), FileMode.Open, FileAccess.Read,
            FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        File.Delete(ResolveKey(storageKey));
        return Task.CompletedTask;
    }

    private string ResolveKey(string key)
    {
        if (key.Length != 32 || key.Any(c => c is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
            throw new AppException(400, "Invalid storage key.");
        return Path.Combine(rootDirectory, key);
    }

    private static bool HasExpectedStructure(ReadOnlySpan<byte> bytes, string mime)
    {
        if (mime == "image/png")
            return bytes.Length >= 45 && bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) &&
                bytes.Slice(8, 8).SequenceEqual(new byte[] { 0, 0, 0, 13, 73, 72, 68, 82 }) &&
                bytes[^12..].SequenceEqual(new byte[] { 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 });
        if (mime == "image/jpeg")
            return bytes.Length >= 10 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff &&
                bytes[^2] == 0xff && bytes[^1] == 0xd9;
        return bytes.Length >= 12 && bytes[..5].SequenceEqual("%PDF-"u8) &&
            Encoding.ASCII.GetString(bytes[Math.Max(0, bytes.Length - 1024)..]).Contains("%%EOF", StringComparison.Ordinal);
    }
}
