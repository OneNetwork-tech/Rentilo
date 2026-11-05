namespace Rentilo.Services;

public interface IImageStorage
{
    /// <summary>Validates and stores an uploaded image. Returns the site-relative path on success.</summary>
    Task<ServiceResult<string>> SaveAsync(IFormFile file, CancellationToken ct = default);

    /// <summary>Deletes a previously stored image. Ignores paths this storage didn't create.</summary>
    void Delete(string? path);
}

/// <summary>
/// Stores uploads on local disk under {WebRoot}/uploads/items. Files get random names, and the content
/// is checked against JPEG/PNG/WebP/GIF signatures so the extension can't be used to smuggle other content.
/// </summary>
public class LocalImageStorage(IWebHostEnvironment env, ILogger<LocalImageStorage> logger) : IImageStorage
{
    public const long MaxBytes = 5 * 1024 * 1024;
    private const string PublicPrefix = "/uploads/items/";

    private string Folder => Path.Combine(env.WebRootPath, "uploads", "items");

    public async Task<ServiceResult<string>> SaveAsync(IFormFile file, CancellationToken ct = default)
    {
        if (file.Length == 0) return ServiceResult<string>.Fail("The uploaded file is empty.");
        if (file.Length > MaxBytes) return ServiceResult<string>.Fail("Images can be at most 5 MB.");

        var header = new byte[12];
        await using (var peek = file.OpenReadStream())
        {
            var read = await peek.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);
            if (read < header.Length) return ServiceResult<string>.Fail("That file is not a supported image.");
        }

        var extension = DetectExtension(header);
        if (extension is null) return ServiceResult<string>.Fail("Please upload a JPEG, PNG, WebP or GIF image.");

        Directory.CreateDirectory(Folder);
        var name = $"{Guid.NewGuid():N}{extension}";
        await using (var target = File.Create(Path.Combine(Folder, name)))
        await using (var source = file.OpenReadStream())
        {
            await source.CopyToAsync(target, ct);
        }
        return ServiceResult<string>.Ok(PublicPrefix + name);
    }

    public void Delete(string? path)
    {
        if (string.IsNullOrEmpty(path) || !path.StartsWith(PublicPrefix, StringComparison.Ordinal)) return;
        var name = Path.GetFileName(path);
        if (name != path[PublicPrefix.Length..]) return; // reject anything with directory parts
        try
        {
            File.Delete(Path.Combine(Folder, name));
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "Could not delete image {Path}", path);
        }
    }

    internal static string? DetectExtension(ReadOnlySpan<byte> h)
    {
        if (h.Length >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF) return ".jpg";
        if (h.Length >= 8 && h[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) return ".png";
        if (h.Length >= 6 && (h[..6].SequenceEqual("GIF87a"u8) || h[..6].SequenceEqual("GIF89a"u8))) return ".gif";
        if (h.Length >= 12 && h[..4].SequenceEqual("RIFF"u8) && h[8..12].SequenceEqual("WEBP"u8)) return ".webp";
        return null;
    }
}
