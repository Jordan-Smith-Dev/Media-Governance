using Umbraco.Cms.Infrastructure.Migrations;
using Umbraco.Community.MediaGovernance.Persistence.Schema;

namespace Umbraco.Community.MediaGovernance.Persistence.Migrations;

/// <summary>Adds the table that maps a governed image's file path to its media item (used to block withheld files).</summary>
internal class CreateMediaFileTableMigration : AsyncMigrationBase
{
	public CreateMediaFileTableMigration(IMigrationContext context) : base(context) { }

	protected override Task MigrateAsync()
	{
		if (!TableExists(TableNames.MediaFile))
			Create.Table<MediaFileRow>().Do();
		return Task.CompletedTask;
	}
}
