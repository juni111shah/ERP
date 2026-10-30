using ERP.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ERP.IntegrationTests;

/// <summary>
/// API host on a real SQL Server database (fresh per factory, created by applying the EF migrations), so tests can exercise
/// concurrent requests, row versions and SQL Server behaviour. Enabled only when the environment variable ERP_TEST_SQL holds a
/// connection string, for example: Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=true
/// </summary>
public sealed class SqlServerApiFactory : ApiFactory
{
    public const string EnvVar = "ERP_TEST_SQL";
    public static string? ConnectionString => Environment.GetEnvironmentVariable(EnvVar) is { Length: > 0 } v ? v : null;
    public const string SkipReason = "Set ERP_TEST_SQL to a SQL Server connection string to run the SQL Server collection.";

    private readonly string _database = "erp_test_" + Guid.NewGuid().ToString("N")[..12];

    private string TestConnectionString => new SqlConnectionStringBuilder(ConnectionString) { InitialCatalog = _database }.ConnectionString;

    protected override void ConfigureDatabase(IServiceCollection s)
    {
        var cs = TestConnectionString;
        s.AddDbContext<AppDbContext>((sp, o) =>
        {
            o.UseSqlServer(cs);
            o.ReplaceService<IModelCacheKeyFactory, ModuleAwareModelCacheKeyFactory>();
            o.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
        });
        s.RemoveAll<IDatabaseInitializer>();
        s.AddScoped<IDatabaseInitializer, SqlServerDatabaseInitializer>();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing || ConnectionString is null) return;
        try
        {
            SqlConnection.ClearAllPools();
            var master = new SqlConnectionStringBuilder(ConnectionString) { InitialCatalog = "master" }.ConnectionString;
            using var c = new SqlConnection(master);
            c.Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = $"IF DB_ID(N'{_database}') IS NOT NULL BEGIN ALTER DATABASE [{_database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_database}]; END";
            cmd.ExecuteNonQuery();
        }
        catch { /* best effort cleanup */ }
    }
}

/// <summary>A fact that runs only when ERP_TEST_SQL is set.</summary>
public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute() { if (SqlServerApiFactory.ConnectionString is null) Skip = SqlServerApiFactory.SkipReason; }
}
