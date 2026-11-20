using Microsoft.Extensions.Logging;
using Umbraco.Community.MediaGovernance.Core.Models;
using Umbraco.Community.MediaGovernance.Services.MediaFiles;

namespace Umbraco.Community.MediaGovernance.Tests;

public class WithheldMediaReporterTests
{
	private readonly ListLogger<WithheldMediaReporter> _log = new();
	private readonly MutableTimeProvider _time = new(new DateTimeOffset(2026, 10, 29, 9, 0, 0, TimeSpan.Zero));
	private readonly Guid _media = Guid.NewGuid();

	private WithheldMediaReporter Sut() => new(_log, _time);

	[Fact]
	public void Logs_a_warning_naming_the_media_status_and_page()
	{
		Sut().Report(_media, GovernanceStatus.Expired, "/news/article");
		var entry = Assert.Single(_log.Entries);
		Assert.Equal(LogLevel.Warning, entry.Level);
		Assert.Contains(_media.ToString(), entry.Message);
		Assert.Contains("Expired", entry.Message);
		Assert.Contains("/news/article", entry.Message);
	}

	[Fact]
	public void Repeat_views_of_the_same_page_are_not_logged_again_within_the_window()
	{
		var sut = Sut();
		sut.Report(_media, GovernanceStatus.Expired, "/news/article");
		_time.Now = _time.Now.AddHours(1);
		sut.Report(_media, GovernanceStatus.Expired, "/news/article");
		Assert.Single(_log.Entries);
	}

	[Fact]
	public void It_is_logged_again_once_the_window_has_passed_so_it_is_not_forgotten()
	{
		var sut = Sut();
		sut.Report(_media, GovernanceStatus.Expired, "/news/article");
		_time.Now = _time.Now.AddHours(7);
		sut.Report(_media, GovernanceStatus.Expired, "/news/article");
		Assert.Equal(2, _log.Entries.Count);
	}

	[Fact]
	public void The_same_media_on_a_different_page_is_logged_separately()
	{
		var sut = Sut();
		sut.Report(_media, GovernanceStatus.Expired, "/news/a");
		sut.Report(_media, GovernanceStatus.Expired, "/news/b");
		Assert.Equal(2, _log.Entries.Count);
	}

	[Fact]
	public void A_change_of_status_on_the_same_page_is_logged_again()
	{
		var sut = Sut();
		sut.Report(_media, GovernanceStatus.NoConsent, "/news/a");
		sut.Report(_media, GovernanceStatus.Expired, "/news/a");
		Assert.Equal(2, _log.Entries.Count);
	}

	[Fact]
	public void Without_a_page_path_the_media_is_still_reported()
	{
		Sut().Report(_media, GovernanceStatus.Expired, null);
		Assert.Single(_log.Entries);
	}
}
