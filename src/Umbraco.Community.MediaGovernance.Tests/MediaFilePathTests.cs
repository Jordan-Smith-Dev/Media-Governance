using Umbraco.Community.MediaGovernance.Core;

namespace Umbraco.Community.MediaGovernance.Tests;

public class MediaFilePathTests
{
	[Theory]
	[InlineData("/media/abc123/Photo.JPG", "media/abc123/photo.jpg")]
	[InlineData("/media/abc123/photo%20one.jpg", "media/abc123/photo one.jpg")]
	[InlineData("/media/abc123/photo.jpg?width=200&height=100", "media/abc123/photo.jpg")]
	[InlineData("/media/abc123/photo.jpg#frag", "media/abc123/photo.jpg")]
	[InlineData("\\media\\abc123\\photo.jpg", "media/abc123/photo.jpg")]
	[InlineData("media/abc123/photo.jpg", "media/abc123/photo.jpg")]
	public void Normalize_makes_request_and_stored_paths_comparable(string input, string expected) =>
		Assert.Equal(expected, MediaFilePath.Normalize(input));

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("?width=100")]
	public void Normalize_returns_null_when_there_is_no_path(string? input) =>
		Assert.Null(MediaFilePath.Normalize(input));

	[Fact]
	public void Normalize_does_not_let_dot_segments_escape_the_media_folder() =>
		Assert.Equal("etc/passwd", MediaFilePath.Normalize("/media/../etc/passwd"));

	[Theory]
	[InlineData("{\"src\":\"/media/abc/photo.png\",\"crops\":[]}", "/media/abc/photo.png")]
	[InlineData("{\"src\":\"/media/abc/photo.png\"}", "/media/abc/photo.png")]
	[InlineData("/media/abc/photo.png", "/media/abc/photo.png")]
	public void ExtractSrc_reads_both_json_and_plain_umbracoFile_values(string value, string expected) =>
		Assert.Equal(expected, MediaFilePath.ExtractSrc(value));

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("{not json")]
	[InlineData("{\"crops\":[]}")]
	[InlineData("just text")]
	public void ExtractSrc_returns_null_for_missing_or_unusable_values(string? value) =>
		Assert.Null(MediaFilePath.ExtractSrc(value));
}
