using Umbraco.Cms.Core.Packaging;

namespace Umbraco.Community.MediaGovernance.Persistence.Migrations;

/// <summary>Discovered and run automatically by Umbraco's package migration runner on startup.</summary>
internal class MediaGovernanceMigrationPlan : PackageMigrationPlan
{
	public MediaGovernanceMigrationPlan() : base("Umbraco.Community.MediaGovernance") { }

	protected override void DefinePlan() =>
		To<CreateTablesMigration>(new Guid("0b0f4b1e-7c1a-4f0e-9c35-3f6a2d0e8a11"))
			.To<CreateMediaFileTableMigration>(new Guid("6c2a1f84-3b7d-4e59-8a10-9d4f5b2e7c33"))
			.To<AddJobTitleMigration>(new Guid("a41d7c2e-58b3-4f6a-9e12-3c8b0d5f7a64"))
			.To<SimplifyConsentMigration>(new Guid("d3e8a5b1-9c47-4a2f-b6d0-17f4c8e92a05"))
			.To<AddConsentDetailMigration>(new Guid("5b7e2c90-1d4a-4f83-a6c5-9e0b3d718f42"));
}
