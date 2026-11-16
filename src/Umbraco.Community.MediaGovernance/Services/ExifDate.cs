using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace Umbraco.Community.MediaGovernance.Services;

/// <summary>Reads the date a photo was taken from a JPEG's EXIF data. Returns null for anything it cannot read.</summary>
public static class ExifDate
{
    private const int MaxHeaderBytes = 256 * 1024;   // EXIF lives at the start of the file
    private static readonly byte[] ExifHeader = [(byte)'E', (byte)'x', (byte)'i', (byte)'f', 0, 0];

    public static DateOnly? Read(Stream jpeg)
    {
        try
        {
            var buffer = new byte[MaxHeaderBytes];
            var length = 0;
            int n;
            while (length < buffer.Length && (n = jpeg.Read(buffer, length, buffer.Length - length)) > 0)
                length += n;
            return Read(buffer.AsSpan(0, length));
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or ObjectDisposedException)
        {
            return null;
        }
    }

    public static DateOnly? Read(ReadOnlySpan<byte> data)
    {
        if (data.Length < 4 || data[0] != 0xFF || data[1] != 0xD8) return null;

        var pos = 2;
        while (pos + 4 <= data.Length)
        {
            if (data[pos] != 0xFF) return null;
            var marker = data[pos + 1];
            if (marker == 0xFF) { pos++; continue; }                 // padding
            if (marker == 0xDA || marker == 0xD9) return null;       // image data or end: no EXIF found
            var size = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(pos + 2, 2));
            if (size < 2) return null;
            var segmentStart = pos + 4;
            var segmentEnd = pos + 2 + size;
            if (segmentEnd > data.Length) return null;

            if (marker == 0xE1 && segmentEnd - segmentStart > 6 && data.Slice(segmentStart, 6).SequenceEqual(ExifHeader))
                return ReadTiff(data[(segmentStart + 6)..segmentEnd].ToArray());

            pos = segmentEnd;
        }
        return null;
    }

    private static DateOnly? ReadTiff(byte[] tiff)
    {
        if (tiff.Length < 8) return null;
        bool little;
        if (tiff[0] == 'I' && tiff[1] == 'I') little = true;
        else if (tiff[0] == 'M' && tiff[1] == 'M') little = false;
        else return null;

        ushort U16(int o) => little ? BinaryPrimitives.ReadUInt16LittleEndian(tiff.AsSpan(o, 2)) : BinaryPrimitives.ReadUInt16BigEndian(tiff.AsSpan(o, 2));
        uint U32(int o) => little ? BinaryPrimitives.ReadUInt32LittleEndian(tiff.AsSpan(o, 4)) : BinaryPrimitives.ReadUInt32BigEndian(tiff.AsSpan(o, 4));

        if (U16(2) != 42) return null;

        string? Ascii(int entry)
        {
            if (U16(entry + 2) != 2) return null;               // not an ASCII field
            var len = (int)U32(entry + 4);
            if (len < 10 || len > 64) return null;
            var offset = len <= 4 ? entry + 8 : (int)U32(entry + 8);
            if (offset < 0 || offset + len > tiff.Length) return null;
            return Encoding.ASCII.GetString(tiff.AsSpan(offset, len)).TrimEnd('\0');
        }

        // Reads one directory: the offset of the Exif sub-directory plus any dates it holds directly.
        (int? ExifIfd, string? Original, string? Digitized, string? Modified) Walk(int ifd)
        {
            int? exifIfd = null;
            string? original = null, digitized = null, modified = null;
            if (ifd < 8 || ifd + 2 > tiff.Length) return default;
            var count = U16(ifd);
            for (var i = 0; i < count; i++)
            {
                var e = ifd + 2 + i * 12;
                if (e + 12 > tiff.Length) break;
                switch (U16(e))
                {
                    case 0x8769: exifIfd = (int)U32(e + 8); break;
                    case 0x9003: original = Ascii(e); break;
                    case 0x9004: digitized = Ascii(e); break;
                    case 0x0132: modified = Ascii(e); break;
                }
            }
            return (exifIfd, original, digitized, modified);
        }

        var root = Walk((int)U32(4));
        var exif = root.ExifIfd is { } sub ? Walk(sub) : default;
        // Prefer when the shutter fired, then when it was digitised, then the camera's modified stamp (weaker).
        return Parse(exif.Original) ?? Parse(exif.Digitized) ?? Parse(root.Original) ?? Parse(root.Modified);
    }

    private static DateOnly? Parse(string? value)
    {
        if (value is null || value.Length < 10) return null;
        // "2021:05:12 14:03:09"
        if (!DateOnly.TryParseExact(value[..10], "yyyy:MM:dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) return null;
        return date.Year < 1990 || date > DateOnly.FromDateTime(DateTime.Today.AddDays(1)) ? null : date;
    }
}
