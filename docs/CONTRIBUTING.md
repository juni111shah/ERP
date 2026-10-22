# Contributing

1. Branch from `main`: `<tool>/<topic>` (tool = antigravity, claude, cursor, codex) or `feature/<topic>`.
2. Follow [AGENTS.md](../AGENTS.md).
3. Build and test locally:
   - `dotnet build backend/ERP.sln` and `dotnet test backend/ERP.sln`
   - `cd frontend && npm ci && npx ng build`
4. Open a pull request into `main`; it merges only when CI is green and reviewed.
5. Database changes go through EF Core migrations in `ERP.Infrastructure/Persistence/Migrations` plus the matching script in `database/scripts`.
