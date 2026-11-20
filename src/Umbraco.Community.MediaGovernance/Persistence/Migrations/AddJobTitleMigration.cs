using NPoco;
using Umbraco.Cms.Infrastructure.Migrations;
using Umbraco.Community.MediaGovernance.Persistence.Schema;

namespace Umbraco.Community.MediaGovernance.Persistence.Migrations;

internal class AddJobTitleMigration : AsyncMigrationBase
{
	public AddJobTitleMigration(IMigrationContext context) : base(context) { }

	protected override Task MigrateAsync()
	{
		if (!ColumnExists(TableNames.Person, "JobTitle"))
		{
			// Umbraco's migration builder does not support ALTER TABLE on SQLite, so use plain SQL for each database.
			var isSqlite = DatabaseType.GetType().Name.Contains("Sqlite", StringComparison.OrdinalIgnoreCase);
			var sql = isSqlite
				? $"ALTER TABLE {TableNames.Person} ADD COLUMN JobTitle TEXT NULL"
				: $"ALTER TABLE {TableNames.Person} ADD JobTitle NVARCHAR(100) NULL";
			Execute.Sql(sql).Do();
		}
		return Task.CompletedTask;
	}
}
