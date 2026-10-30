---
id: TASK-033
title: POS KPIs aggregate in SQL, respect the legal entity, drop hardcoded AED
priority: P2
status: done
schema: no
depends_on: []
area: pos
audit_ref: TASK-012 leftover
commit: bb3388f
---

## Problem
- `PosKpiHandlers` loads every order of the day into memory (`PosKpis.cs:37-39`) and ignores the selected legal entity (`41-44`), unlike `PosDashboard.cs:78-82`.
- `PosSearch.cs:124` hardcodes `AED` in the hit subtitle although TASK-012 removed hardcoded currency elsewhere.
- "Today" is computed twice with `DateTimeOffset.UtcNow.Date` (`PosKpis.cs:36`, `PosDashboard.cs:77`).

## Impact
Slow KPI endpoint on busy shops, totals that mix legal entities, wrong currency label.

## Change wanted
- Rewrite the KPI query with `SumAsync`/`CountAsync` on the query (no `ToListAsync`), add an optional `legalEntityId` query parameter on `/kpis`, and share the "today" filter with `PosDashboard` through one helper in the POS module.
- Format the search subtitle without a currency literal (amount and status only, or the workspace currency if an existing abstraction provides it; search first).

## Acceptance criteria
- [ ] KPI results equal the old values for one legal entity and exclude other entities when `legalEntityId` is supplied.
- [ ] Voided orders are excluded.
- [ ] No `"AED"` literal remains under `ERP.Modules.Pos`.

## Tests required
Integration test with two legal entities; a test that fails if the literal reappears (extend an existing hardcode check if one exists).

## Do not touch
Permissions and endpoint routes.

## What I did (Claude)
PosKpis rewritten with CountAsync/SumAsync (sum as double so SQLite can aggregate, rounded to 2dp), optional legalEntityId on /kpis, shared PosToday filter used by PosDashboard (filters on CreatedAt since DateTimeOffset comparisons do not translate on SQLite), AED literal removed from PosSearch. Tests: two-entity KPI test, voided excluded, source scan for the AED literal (PosRegisterTests).

## Review (Claude)
- Verified: dotnet build 0 errors; 253 unit + 285 integration pass, 4 skipped (SQL Server).
