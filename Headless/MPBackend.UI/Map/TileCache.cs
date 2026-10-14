using System.Collections.Concurrent;
using MPBackend.UI.Services;

namespace MPBackend.UI.Map;

/// <summary>
/// Tiles as data URIs for the map's img elements, fetched from the backend in the background (at most four at a
/// time), with a bounded cache. <see cref="Changed"/> fires on a worker thread when a tile arrives.
/// </summary>
public sealed class TileCache
{
    private const int MaxEntries = 400;
    private static readonly TimeSpan RetryMissing = TimeSpan.FromSeconds(60);

    private readonly IGcs gcs;
    private readonly ConcurrentDictionary<string, Entry> entries = new();
    private readonly ConcurrentDictionary<string, byte> pending = new();
    private readonly SemaphoreSlim slots = new(4);

    private sealed class Entry
    {
        public string? DataUri;
        public DateTime Touched;
        public DateTime? MissingAt;
    }

    public TileCache(IGcs gcs)
    {
        this.gcs = gcs;
    }

    public event Action? Changed;

    /// <summary>The tile's data URI when cached; otherwise null and a fetch is queued.</summary>
    public string? Get(string provider, int zoom, long x, long y)
    {
        var key = $"{provider}/{zoom}/{x}/{y}";
        if (entries.TryGetValue(key, out var e))
        {
            e.Touched = DateTime.UtcNow;
            if (e.DataUri != null)
                return e.DataUri;
            if (e.MissingAt != null && DateTime.UtcNow - e.MissingAt < RetryMissing)
                return null;
        }

        if (pending.TryAdd(key, 0))
            _ = FetchAsync(key, provider, zoom, x, y);
        return null;
    }

    private async Task FetchAsync(string key, string provider, int zoom, long x, long y)
    {
        await slots.WaitAsync();
        try
        {
            var tile = await gcs.GetTileAsync(provider, zoom, x, y);
            var entry = new Entry { Touched = DateTime.UtcNow };
            if (tile is { } t)
                entry.DataUri = $"data:{t.ContentType};base64,{Convert.ToBase64String(t.Data)}";
            else
                entry.MissingAt = DateTime.UtcNow;
            entries[key] = entry;
            Trim();
        }
        catch
        {
            entries[key] = new Entry { Touched = DateTime.UtcNow, MissingAt = DateTime.UtcNow };
        }
        finally
        {
            slots.Release();
            pending.TryRemove(key, out _);
        }

        Changed?.Invoke();
    }

    private void Trim()
    {
        if (entries.Count <= MaxEntries)
            return;
        foreach (var old in entries.OrderBy(kv => kv.Value.Touched).Take(entries.Count - MaxEntries).Select(kv => kv.Key).ToList())
            entries.TryRemove(old, out _);
    }
}
