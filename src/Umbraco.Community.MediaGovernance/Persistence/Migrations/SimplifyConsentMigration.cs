using NPoco;
using Umbraco.Cms.Infrastructure.Migrations;
using Umbraco.Community.MediaGovernance.Persistence.Schema;

namespace Umbraco.Community.MediaGovernance.Persistence.Migrations;

/// <summary>
/// Consent becomes a yes/no on the person. Adds the two columns and drops the old per-image consent table
/// (nothing was in use yet, so existing consent rows are not carried over).
/// </summary>
internal class SimplifyConsentMigration : AsyncMigrationBase
{
	public SimplifyConsentMigration(IMigrationContext context) : base(context) { }

	protected override Task MigrateAsync()
	{
		var isSqlite = DatabaseType.GetType().Name.Contains("Sqlite", StringComparison.OrdinalIgnoreCase);

		if (!ColumnExists(TableNames.Person, "ConsentGiven"))
			Execute.Sql(isSqlite
				? $"ALTER TABLE {TableNames.Person} ADD COLUMN ConsentGiven INTEGER NOT NULL DEFAULT 0"
				: $"ALTER TABLE {TableNames.Person} ADD ConsentGiven BIT NOT NULL DEFAULT 0").Do();

		if (!ColumnExists(TableNames.Person, "ConsentUpdatedUtc"))
			Execute.Sql(isSqlite
				? $"ALTER TABLE {TableNames.Person} ADD COLUMN ConsentUpdatedUtc TEXT NULL"
				: $"ALTER TABLE {TableNames.Person} ADD ConsentUpdatedUtc DATETIME NULL").Do();

		if (TableExists(TableNames.LegacyConsent))
			Delete.Table(TableNames.LegacyConsent).Do();

		return Task.CompletedTask;
	}
}
