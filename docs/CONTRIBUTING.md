# Contributing

1. Branch from `main`: `<tool>/<topic>` (tool = antigravity, claude, cursor, codex) or `feature/<topic>`.
2. Follow [AGENTS.md](../AGENTS.md).
3. Build and test locally:
   - `dotnet build backend/ERP.sln` and `dotnet test backend/ERP.sln`
   - `cd frontend && npm ci && npx ng build`
4. Open a pull request into `main`; it merges only when CI is green and reviewed.
5. Database changes go through EF Core migrations in `ERP.Infrastructure/Persistence/Migrations` plus the matching script in `database/scripts`.

## SQL Server test collection

Integration tests normally run on in-memory SQLite. Tests that need real concurrency or SQL Server behaviour
(`ParallelIssuingTests`: migrations apply, 20 parallel receipts, 10 parallel invoice issues) use `SqlServerApiFactory`, which creates a
fresh `erp_test_*` database per test and drops it afterwards. They are skipped unless `ERP_TEST_SQL` is set to a connection string
(without a database name), for example with LocalDB:

```powershell
$env:ERP_TEST_SQL = 'Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=true'
dotnet test backend/tests/ERP.IntegrationTests --filter "FullyQualifiedName~ParallelIssuingTests"
```

CI runs the same collection in the `Backend SQL Server tests` job against a SQL Server service container.
