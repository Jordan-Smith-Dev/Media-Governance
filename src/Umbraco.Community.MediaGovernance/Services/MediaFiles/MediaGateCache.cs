using Microsoft.Extensions.Caching.Memory;

namespace Umbraco.Community.MediaGovernance.Services.MediaFiles;

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
