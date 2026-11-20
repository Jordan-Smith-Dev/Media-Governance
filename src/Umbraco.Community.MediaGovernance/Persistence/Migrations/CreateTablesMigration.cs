using Umbraco.Cms.Infrastructure.Migrations;
using Umbraco.Community.MediaGovernance.Persistence.Schema;

namespace Umbraco.Community.MediaGovernance.Persistence.Migrations;

internal class CreateTablesMigration : AsyncMigrationBase
{
	public CreateTablesMigration(IMigrationContext context) : base(context) { }

	protected override Task MigrateAsync()
	{
		CreateIfMissing<PersonRow>(TableNames.Person);
		CreateIfMissing<ImageRow>(TableNames.Image);
		CreateIfMissing<ImagePersonRow>(TableNames.ImagePerson);
		CreateIfMissing<AuditRow>(TableNames.Audit);
		CreateIfMissing<NotifiedRow>(TableNames.Notified);
		return Task.CompletedTask;
	}

	private void CreateIfMissing<T>(string tableName)
	{
		if (!TableExists(tableName))
			Create.Table<T>().Do();
	}
}
