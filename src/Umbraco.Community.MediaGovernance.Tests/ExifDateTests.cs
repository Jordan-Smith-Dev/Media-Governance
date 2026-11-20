using System.Text;
using Umbraco.Community.MediaGovernance.Services.Images;

namespace Umbraco.Community.MediaGovernance.Tests;

public class ExifDateTests
{
	private static byte[] U16(int v, bool be) => be ? [(byte)(v >> 8), (byte)v] : [(byte)v, (byte)(v >> 8)];
	private static byte[] U32(int v, bool be) => be ? [(byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v] : [(byte)v, (byte)(v >> 8), (byte)(v >> 16), (byte)(v >> 24)];
	private static byte[] Ascii(string s) => [.. Encoding.ASCII.GetBytes(s), 0];

	private static void Entry(List<byte> to, bool be, int tag, int type, int count, int valueOrOffset)
	{
		to.AddRange(U16(tag, be));
		to.AddRange(U16(type, be));
		to.AddRange(U32(count, be));
		to.AddRange(U32(valueOrOffset, be));
	}

	/// <summary>
	/// A minimal JPEG with an EXIF segment. Layout: TIFF header, IFD0, IFD0 data, then (optionally) the Exif IFD with its data.
	/// <paramref name="original"/> goes in the Exif IFD (or in IFD0 when <paramref name="inExifIfd"/> is false).
	/// </summary>
	private static byte[] Jpeg(string? original, string? modified = null, bool be = false, bool inExifIfd = true)
	{
		var inRoot = new List<(int Tag, string Text)>();
		if (modified is not null)
			inRoot.Add((0x0132, modified));
		if (!inExifIfd && original is not null)
			inRoot.Add((0x9003, original));
		var hasExifIfd = inExifIfd && original is not null;

		var rootCount = inRoot.Count + (hasExifIfd ? 1 : 0);
		var ifd0Offset = 8;
		var ifd0Size = 2 + rootCount * 12 + 4;
		var rootDataOffset = ifd0Offset + ifd0Size;
		var rootDataSize = inRoot.Count * 20;
		var exifIfdOffset = rootDataOffset + rootDataSize;

		var tiff = new List<byte>();
		tiff.AddRange(be ? "MM"u8.ToArray() : "II"u8.ToArray());
		tiff.AddRange(U16(42, be));
		tiff.AddRange(U32(ifd0Offset, be));

		tiff.AddRange(U16(rootCount, be));
		// Entries must be in tag order: 0x0132, 0x8769 / 0x9003
		var ordered = inRoot.OrderBy(e => e.Tag).ToList();
		var dataCursor = rootDataOffset;
		var rootData = new List<byte>();
		foreach (var e in ordered)
		{
			Entry(tiff, be, e.Tag, 2, 20, dataCursor);
			rootData.AddRange(Ascii(e.Text));
			dataCursor += 20;
		}
		if (hasExifIfd)
			Entry(tiff, be, 0x8769, 4, 1, exifIfdOffset);
		tiff.AddRange(U32(0, be));
		tiff.AddRange(rootData);

		if (hasExifIfd)
		{
			tiff.AddRange(U16(1, be));
			Entry(tiff, be, 0x9003, 2, 20, exifIfdOffset + 2 + 12 + 4);
			tiff.AddRange(U32(0, be));
			tiff.AddRange(Ascii(original!));
		}

		var segment = new List<byte>();
		segment.AddRange("Exif\0\0"u8.ToArray());
		segment.AddRange(tiff);
		var jpeg = new List<byte> { 0xFF, 0xD8, 0xFF, 0xE1 };
		jpeg.AddRange(U16(segment.Count + 2, true));
		jpeg.AddRange(segment);
		jpeg.AddRange([0xFF, 0xD9]);
		return [.. jpeg];
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void Reads_date_taken(bool bigEndian) =>
		Assert.Equal(new DateOnly(2021, 5, 12), ExifDate.Read(Jpeg("2021:05:12 14:03:09", be: bigEndian)));

	[Fact]
	public void Reads_from_a_stream() =>
		Assert.Equal(new DateOnly(2019, 1, 2), ExifDate.Read(new MemoryStream(Jpeg("2019:01:02 00:00:00"))));

	[Fact]
	public void Prefers_date_taken_over_modified() =>
		Assert.Equal(new DateOnly(2020, 3, 4), ExifDate.Read(Jpeg("2020:03:04 10:00:00", modified: "2022:09:09 09:09:09")));

	[Fact]
	public void Falls_back_to_modified_when_there_is_no_date_taken() =>
		Assert.Equal(new DateOnly(2022, 9, 9), ExifDate.Read(Jpeg(null, modified: "2022:09:09 09:09:09")));

	[Fact]
	public void Reads_date_held_directly_in_the_first_directory() =>
		Assert.Equal(new DateOnly(2018, 6, 7), ExifDate.Read(Jpeg("2018:06:07 01:02:03", inExifIfd: false)));

	[Fact]
	public void Returns_null_when_there_is_no_date() => Assert.Null(ExifDate.Read(Jpeg(null)));

	[Theory]
	[InlineData("0000:00:00 00:00:00")]
	[InlineData("1970:01:01 00:00:00")]   // camera clock never set
	[InlineData("2999:01:01 00:00:00")]   // in the future
	[InlineData("not a date at all!!")]
	public void Ignores_implausible_dates(string value) => Assert.Null(ExifDate.Read(Jpeg(value)));

	[Fact]
	public void Returns_null_for_non_jpeg_and_damaged_data()
	{
		Assert.Null(ExifDate.Read(new byte[] { 0x89, 0x50, 0x4E, 0x47 }));
		Assert.Null(ExifDate.Read(Array.Empty<byte>()));
		var good = Jpeg("2021:05:12 14:03:09");
		for (var cut = 2; cut < good.Length; cut += 3)
			ExifDate.Read(good.AsSpan(0, cut));   // must not throw
		var corrupt = (byte[])good.Clone();
		for (var i = 4; i < corrupt.Length; i++)
			corrupt[i] = 0xFF;
		Assert.Null(ExifDate.Read(corrupt));
	}
}
