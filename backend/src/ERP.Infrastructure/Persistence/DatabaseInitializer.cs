using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence;

/// <summary>Brings the database schema up to date at startup (when Database:AutoMigrate is on).</summary>
public interface IDatabaseInitializer
{
    Task InitializeAsync(AppDbContext db, CancellationToken ct = default);
}

/// <summary>Applies the EF Core migrations to SQL Server, the application's only database.</summary>
public sealed class SqlServerDatabaseInitializer : IDatabaseInitializer
{
    public Task InitializeAsync(AppDbContext db, CancellationToken ct = default) => db.Database.MigrateAsync(ct);
}
