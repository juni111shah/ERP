using ERP.Application.Abstractions;
using Microsoft.Data.SqlClient;

namespace ERP.Infrastructure.Persistence;

/// <summary>Maps provider errors: SQL Server 2601 (unique index) / 2627 (unique constraint) name the index in the message; SQLite (tests) reports the table.</summary>
public sealed class DbErrors : IDbErrors
{
    public bool IsUniqueViolation(Exception ex, string indexName)
    {
        for (var e = ex; e is not null; e = e.InnerException)
        {
            if (e is SqlException { Number: 2601 or 2627 } sql) return sql.Message.Contains(indexName, StringComparison.OrdinalIgnoreCase);
            // SQLite error 19: "UNIQUE constraint failed: Bills.OrgId, Bills.PartyId, ..." (index names follow IX_<Table>_<Columns>).
            if (e.GetType().Name == "SqliteException" && e.Message.Contains("UNIQUE constraint failed", StringComparison.Ordinal))
            {
                var parts = indexName.Split('_');
                return parts.Length > 2 && parts[2..].All(c => e.Message.Contains($"{parts[1]}.{c}", StringComparison.Ordinal));
            }
        }
        return false;
    }
}
