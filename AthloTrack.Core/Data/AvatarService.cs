using System.Collections.Concurrent;
using AthloTrack.Core.Supabase;

namespace AthloTrack.Core.Data;

public sealed class AvatarService : IAvatarService
{
    private const string Bucket = "avatars";

    private readonly SupabaseClientFactory _factory;

    // Paths are unique per upload, so a cached entry never goes stale.
    private readonly ConcurrentDictionary<string, Task<byte[]?>> _cache = new();

    public AvatarService(SupabaseClientFactory factory)
    {
        _factory = factory;
    }

    public async Task<string> UploadAsync(Guid athleteId, byte[] image, string contentType)
    {
        var extension = contentType == "image/jpeg" ? "jpg" : "png";
        // A fresh name per upload: no overwrite, and no stale caches anywhere.
        var path = $"{athleteId}/avatar-{DateTime.UtcNow:yyyyMMddHHmmssfff}.{extension}";

        var client = await _factory.GetClientAsync();
        await client.Storage.From(Bucket).Upload(image, path,
            new global::Supabase.Storage.FileOptions { ContentType = contentType, Upsert = false });

        _cache[path] = Task.FromResult<byte[]?>(image);
        return path;
    }

    public Task<byte[]?> GetAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Task.FromResult<byte[]?>(null);
        }

        return _cache.GetOrAdd(path, Download);
    }

    public async Task RemoveAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        _cache.TryRemove(path, out _);
        try
        {
            var client = await _factory.GetClientAsync();
            await client.Storage.From(Bucket).Remove(new List<string> { path });
        }
        catch
        {
            // An orphaned file is harmless; don't fail the calling edit/delete over it.
        }
    }

    private async Task<byte[]?> Download(string path)
    {
        try
        {
            var client = await _factory.GetClientAsync();
            return await client.Storage.From(Bucket)
                .Download(path, (global::Supabase.Storage.TransformOptions?)null, null);
        }
        catch
        {
            // Don't cache the failure; a later view can retry.
            _cache.TryRemove(path, out _);
            return null;
        }
    }
}
