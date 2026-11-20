using Umbraco.Community.MediaGovernance.Core;
using Umbraco.Community.MediaGovernance.Core.Models;

namespace Umbraco.Community.MediaGovernance.Tests;

public class FolderRollupTests
{
	[Fact]
	public void A_folder_with_no_images_or_only_compliant_ones_needs_nothing() =>
		Assert.Equal(FolderAttention.None, FolderRollup.Of([GovernanceStatus.Compliant, GovernanceStatus.Compliant]));

	[Fact]
	public void An_empty_folder_needs_nothing() =>
		Assert.Equal(FolderAttention.None, FolderRollup.Of([]));

	[Theory]
	[InlineData(GovernanceStatus.Expired)]
	[InlineData(GovernanceStatus.NoConsent)]
	public void Any_withheld_image_makes_the_folder_Withheld(GovernanceStatus status) =>
		Assert.Equal(FolderAttention.Withheld, FolderRollup.Of([GovernanceStatus.Compliant, status]));

	[Theory]
	[InlineData(GovernanceStatus.ExpiringSoon)]
	[InlineData(GovernanceStatus.NeedsTagging)]
	public void Warnings_alone_make_the_folder_Warning(GovernanceStatus status) =>
		Assert.Equal(FolderAttention.Warning, FolderRollup.Of([GovernanceStatus.Compliant, status]));

	[Fact]
	public void Withheld_beats_warning() =>
		Assert.Equal(FolderAttention.Withheld, FolderRollup.Of([GovernanceStatus.NeedsTagging, GovernanceStatus.Expired, GovernanceStatus.ExpiringSoon]));
}
