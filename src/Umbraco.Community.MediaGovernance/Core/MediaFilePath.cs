using System.Text.Json;

namespace Umbraco.Community.MediaGovernance.Core;

/// <summary>Helpers for matching a requested media URL to the file path Umbraco stores on a media item.</summary>
internal static class MediaFilePath
{
	/// <summary>
	/// Makes a request path and a stored path comparable: no query or fragment, URL-decoded, forward slashes,
	/// no leading slash, dot segments resolved, lower case. Returns null when there is no path.
	/// </summary>
	public static string? Normalize(string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
			return null;

		var cut = path.IndexOfAny(['?', '#']);
		if (cut >= 0)
			path = path[..cut];

		string decoded;
		try
		{
			decoded = Uri.UnescapeDataString(path);
		}
		catch (UriFormatException)
		{
			decoded = path;
		}

		var segments = new List<string>();
		foreach (var segment in decoded.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
		{
			if (segment == ".")
				continue;
			if (segment == "..")
			{
				if (segments.Count > 0)
					segments.RemoveAt(segments.Count - 1);
				continue;
			}
			segments.Add(segment);
		}

		return segments.Count == 0 ? null : string.Join('/', segments).ToLowerInvariant();
	}

	/// <summary>
	/// Reads the file path from an Umbraco <c>umbracoFile</c> value: either JSON like <c>{"src":"/media/…"}</c> or a plain path.
	/// </summary>
	public static string? ExtractSrc(string? umbracoFileValue)
	{
		if (string.IsNullOrWhiteSpace(umbracoFileValue))
			return null;
		var value = umbracoFileValue.Trim();

		if (value.StartsWith('{'))
		{
			try
			{
				using var doc = JsonDocument.Parse(value);
				return doc.RootElement.TryGetProperty("src", out var src) && src.ValueKind == JsonValueKind.String
					? src.GetString()
					: null;
			}
			catch (JsonException)
			{
				return null;
			}
		}

		return value.StartsWith('/') ? value : null;
	}
}
