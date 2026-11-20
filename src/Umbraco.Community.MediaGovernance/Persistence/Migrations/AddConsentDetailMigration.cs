using NPoco;
using Umbraco.Cms.Infrastructure.Migrations;
using Umbraco.Community.MediaGovernance.Persistence.Schema;

namespace Umbraco.Community.MediaGovernance.Persistence.Migrations;

/// <summary>
/// Adds the optional consent detail to people: given on, given by, expiry, permitted uses and the evidence file.
/// All nullable, so every existing consent stays exactly as it was ("given", with no extra detail).
/// </summary>
internal class AddConsentDetailMigration : AsyncMigrationBase
{
	public AddConsentDetailMigration(IMigrationContext context) : base(context) { }

	protected override Task MigrateAsync()
	{
		var isSqlite = DatabaseType.GetType().Name.Contains("Sqlite", StringComparison.OrdinalIgnoreCase);
		var date = isSqlite ? "TEXT NULL" : "DATETIME NULL";
		Add("ConsentGivenOn", date, isSqlite);
		Add("ConsentGivenBy", isSqlite ? "TEXT NULL" : "NVARCHAR(30) NULL", isSqlite);
		Add("ConsentExpiresOn", date, isSqlite);
		Add("ConsentPermittedUses", isSqlite ? "TEXT NULL" : "NVARCHAR(500) NULL", isSqlite);
		Add("ConsentEvidencePath", isSqlite ? "TEXT NULL" : "NVARCHAR(500) NULL", isSqlite);
		return Task.CompletedTask;
	}

	private void Add(string column, string definition, bool isSqlite)
	{
		if (ColumnExists(TableNames.Person, column))
			return;
		Execute.Sql(isSqlite
			? $"ALTER TABLE {TableNames.Person} ADD COLUMN {column} {definition}"
			: $"ALTER TABLE {TableNames.Person} ADD {column} {definition}").Do();
	}
}
