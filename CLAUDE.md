# CLAUDE.md

Claude Code follows the shared rules in [AGENTS.md](AGENTS.md); read it first, every session.

Claude-specific:
- Work on a `claude/<topic>` branch, never commit directly to `main`.
- Run `dotnet build backend/ERP.sln`, `dotnet test backend/ERP.sln` and `cd frontend && npx ng build` before opening a PR.
- Keep changes small and focused; one feature per PR.
