using Microsoft.Extensions.Caching.Memory;
using Umbraco.Community.MediaGovernance.Core;
using Umbraco.Community.MediaGovernance.Core.Models;

namespace Umbraco.Community.MediaGovernance.Services;

/// <summary>
/// A small shared cache for file requests. Entries last 30 seconds so a busy page does not query the database
/// for every image, and <see cref="Clear"/> is called whenever governance data changes on this server.
/// On a load-balanced site, other servers can lag by up to 30 seconds.
/// </summary>
public class MediaGateCache
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    public bool TryGet<T>(string key, out T? value)
    {
        if (_cache.TryGetValue(key, out var stored) && stored is T typed)
        {
            value = typed;
            return true;
        }
        value = default;
        return false;
    }

    public void Set<T>(string key, T value) => _cache.Set(key, value, Lifetime);

    public void Clear() => _cache.Compact(1.0);
}

public record GateDecision(bool Allowed, GovernanceStatus? Status = null);

/// <summary>Decides whether a request for a media file may be served.</summary>
public class MediaFileGate(
    IMediaFileIndex index,
    IMediaGovernanceUsage usage,
    IWithheldMediaReporter reporter,
    MediaGateCache cache)
{
    public static readonly GateDecision Allow = new(true);

    public async Task<GateDecision> CheckAsync(string requestPath, bool isBackofficeUser)
    {
        // Editors must always be able to see an image, or they could not find and fix it.
        if (isBackofficeUser) return Allow;

        var normalized = MediaFilePath.Normalize(requestPath);
        if (normalized is null) return Allow;

        var key = await index.FindMediaKeyAsync(normalized);
        if (key is null) return Allow;

        var statusKey = "mg-status-" + key;
        if (!cache.TryGet<GovernanceStatus>(statusKey, out var status))
        {
            status = await usage.GetStatusAsync(key.Value);
            cache.Set(statusKey, status);
        }

        if (UsagePolicy.IsUsable(status)) return Allow;

        reporter.Report(key.Value, status, $"{requestPath} (direct file request)");
        return new GateDecision(false, status);
    }

    public void ClearCache() => cache.Clear();
}
