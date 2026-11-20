using Umbraco.Community.MediaGovernance.Services.Settings;

namespace Umbraco.Community.MediaGovernance.Tests;

public class GovernanceOptionsValidatorTests
{
	private static SettingsUpdate Update(params string[] emails) => new(3, 90, emails);

	[Fact]
	public void Accepts_valid_settings() => GovernanceOptionsValidator.Validate(Update("ops@example.com"));

	[Theory]
	[InlineData("not-an-email")]
	[InlineData("a@b.com, c@d.com")]
	[InlineData("")]
	public void Rejects_invalid_email_addresses(string email) =>
		Assert.Throws<ArgumentException>(() => GovernanceOptionsValidator.Validate(Update("ops@example.com", email)));

	[Fact]
	public void Normalize_trims_and_removes_duplicates_ignoring_case()
	{
		var cleaned = GovernanceOptionsValidator.Normalize(Update(" Ops@Example.com ", "ops@example.com", "", "  "));
		Assert.Equal(["Ops@Example.com"], cleaned.NotificationEmails);
	}

	[Fact]
	public void Rejects_more_than_25_recipients()
	{
		var many = Enumerable.Range(1, 26).Select(i => $"user{i}@example.com").ToArray();
		Assert.Throws<ArgumentException>(() => GovernanceOptionsValidator.Validate(Update(many)));
	}

	[Fact]
	public void Accepts_exactly_25_recipients()
	{
		var many = Enumerable.Range(1, 25).Select(i => $"user{i}@example.com").ToArray();
		GovernanceOptionsValidator.Validate(Update(many));
	}

	[Theory]
	[InlineData(0, 90)]
	[InlineData(101, 90)]
	[InlineData(3, -1)]
	[InlineData(3, 3651)]
	public void Rejects_out_of_range_retention_or_warning(int years, int days) =>
		Assert.Throws<ArgumentException>(() => GovernanceOptionsValidator.Validate(new SettingsUpdate(years, days, [])));
}
