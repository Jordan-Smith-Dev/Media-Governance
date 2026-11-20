using System.Text.RegularExpressions;

namespace Umbraco.Community.MediaGovernance.Services.Evidence;

/// <summary>Which evidence file types are accepted, what their bytes must look like, and the content type to serve them with.</summary>
internal static class EvidenceContent
{
	public const long MaxBytes = 10 * 1024 * 1024;

	private static readonly byte[] Pdf = "%PDF-"u8.ToArray();
	private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
	private static readonly byte[] Jpg = [0xFF, 0xD8, 0xFF];

	// person folder / file, both GUIDs, with an allowed extension. Anything else is not ours and is never touched.
	private static readonly Regex StoredPath = new(@"^[0-9a-fA-F]{8}(-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}/[0-9a-fA-F]{8}(-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}\.(pdf|png|jpg|jpeg)$", RegexOptions.Compiled);

	public static bool IsStoredPath(string? path) => path is not null && StoredPath.IsMatch(path);

	public static bool IsAllowedExtension(string extension) => ContentTypeFor(extension) is not null;

	/// <summary>The content type for an allowed extension, otherwise null. Never derived from anything the client sent.</summary>
	public static string? ContentTypeFor(string extension) => extension.ToLowerInvariant() switch
	{
		".pdf" => "application/pdf",
		".png" => "image/png",
		".jpg" or ".jpeg" => "image/jpeg",
		_ => null,
	};

	/// <summary>A short label for the file type, for display.</summary>
	public static string TypeLabel(string path) => Path.GetExtension(path).ToLowerInvariant() switch
	{
		".pdf" => "PDF",
		".png" => "PNG",
		".jpg" or ".jpeg" => "JPG",
		_ => "File",
	};

	/// <summary>True when the file starts with the signature its extension claims.</summary>
	public static bool MatchesExtension(string extension, ReadOnlySpan<byte> data)
	{
		var signature = extension.ToLowerInvariant() switch
		{
			".pdf" => Pdf,
			".png" => Png,
			".jpg" or ".jpeg" => Jpg,
			_ => null,
		};
		return signature is not null && data.StartsWith(signature);
	}

	/// <summary>Reads an upload, refusing anything that is not a real PDF, PNG or JPG of 10 MB or less.</summary>
	public static async Task<(string Extension, byte[] Bytes)> ReadAndCheckAsync(string fileName, Stream data)
	{
		var extension = Path.GetExtension(Path.GetFileName(fileName ?? string.Empty)).ToLowerInvariant();
		if (string.IsNullOrEmpty(extension) || !IsAllowedExtension(extension))
			throw new ArgumentException("Evidence must be a PDF, PNG or JPG file.");

		// Read at most MaxBytes + 1 so oversized uploads are rejected without buffering them in full.
		using var buffer = new MemoryStream();
		var chunk = new byte[81920];
		int read;
		while ((read = await data.ReadAsync(chunk)) > 0)
		{
			buffer.Write(chunk, 0, read);
			if (buffer.Length > MaxBytes)
				throw new ArgumentException("Evidence files must be 10 MB or smaller.");
		}

		if (!MatchesExtension(extension, buffer.GetBuffer().AsSpan(0, (int)buffer.Length)))
			throw new ArgumentException("The file contents do not match its type. Evidence must be a real PDF, PNG or JPG.");

		return (extension, buffer.ToArray());
	}
}
