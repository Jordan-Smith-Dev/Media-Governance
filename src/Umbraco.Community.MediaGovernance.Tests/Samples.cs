namespace Umbraco.Community.MediaGovernance.Tests;

internal static class Samples
{
	public static byte[] Pdf(int extra = 0) => [.. "%PDF-1.4 test"u8.ToArray(), .. new byte[extra]];
	public static byte[] Png => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];
	public static byte[] Jpg => [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3];
}
