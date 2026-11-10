using Umbraco.Cms.Core.Packaging;
using Umbraco.Cms.Infrastructure.Migrations;
using Umbraco.Community.MediaGovernance.Persistence.Schema;

namespace Umbraco.Community.MediaGovernance.Persistence.Migrations;

/// <summary>Discovered and run automatically by Umbraco's package migration runner on startup.</summary>
public class MediaGovernanceMigrationPlan : PackageMigrationPlan
{
    public MediaGovernanceMigrationPlan() : base("Umbraco.Community.MediaGovernance") { }

    protected override void DefinePlan() =>
        To<CreateTablesMigration>(new Guid("0b0f4b1e-7c1a-4f0e-9c35-3f6a2d0e8a11"))
            .To<CreateMediaFileTableMigration>(new Guid("6c2a1f84-3b7d-4e59-8a10-9d4f5b2e7c33"))
            .To<AddJobTitleMigration>(new Guid("a41d7c2e-58b3-4f6a-9e12-3c8b0d5f7a64"));
}

public class CreateTablesMigration : AsyncMigrationBase
{
    public CreateTablesMigration(IMigrationContext context) : base(context) { }

    protected override Task MigrateAsync()
    {
        CreateIfMissing<PersonRow>(TableNames.Person);
        CreateIfMissing<ImageRow>(TableNames.Image);
        CreateIfMissing<ImagePersonRow>(TableNames.ImagePerson);
        CreateIfMissing<ConsentRow>(TableNames.Consent);
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

/// <summary>Adds the table that maps a governed image's file path to its media item (used to block withheld files).</summary>
public class CreateMediaFileTableMigration : AsyncMigrationBase
{
    public CreateMediaFileTableMigration(IMigrationContext context) : base(context) { }

    protected override Task MigrateAsync()
    {
        if (!TableExists(TableNames.MediaFile))
            Create.Table<MediaFileRow>().Do();
        return Task.CompletedTask;
    }
}

/// <summary>Adds an optional job title to people.</summary>
public class AddJobTitleMigration : AsyncMigrationBase
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
