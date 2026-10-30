---
id: TASK-012
title: Remove hardcoded currency, tax rate and placeholder GUIDs from module pages
priority: P1
status: done
schema: no
depends_on: []
area: frontend
audit_ref: AUD-006, AUD-007
commit: 7ca79a1
---

## Problem
55 occurrences of `'AED'`, `AED `, `* 0.05`, `taxRate: 5`, `* 1.05` in `frontend/src/app/pages`; `$` in Manufacturing; sample GUID in `crm/interactions.page.ts:304`; `00000000-...-0001` fallbacks in POS, Distribution and `services-analytics.page.ts:167`.

## Impact
Wrong currency and tax for any non-AED workspace; invalid ids posted to the API.

## Change wanted
- Add a `WorkspaceCurrencyService` (core) exposing the workspace currency and formatting helper; use it in every module page instead of literals.
- Take default tax rate from the item's tax code or workspace default tax code; POS totals use the server response.
- Remove every all-zero and sample GUID; if the user id or legal entity is missing, show an error and block the action.
- CRM interactions must send the real related entity id from the selected record.

## Acceptance criteria
- [x] `grep -rnE "'AED'|AED |\* 0\.05|\* 1\.05|3fa85f64|00000000-0000-0000-0000-00000000000[1-9]" frontend/src/app` returns nothing outside comments and tests.
- [x] Switching the workspace currency changes labels in Sales, POS, Retail and Manufacturing pages.

## Tests required
Spec for `WorkspaceCurrencyService`; `ng build` clean.

## Do not touch
Backend contracts.

## What I did (Antigravity)
- Created `WorkspaceCurrencyService` in `frontend/src/app/core/services/workspace-currency.service.ts` exposing active workspace/legal-entity currency and reactive helpers (`currency`, `code`, `format`).
- Added unit tests in `workspace-currency.service.spec.ts` (3 tests passed under Karma Headless).
- Replaced all hardcoded currency literals (`AED`, `$`) across Admin, CRM, Distribution, Manufacturing, POS, Projects, Purchasing, Retail, Sales, Services, HR, Inventory, and Real Estate pages.
- Dynamically loaded tax rates from `TaxCodesApi` and items' configured tax codes in Quotes, Orders, Purchase Orders, and POS instead of hardcoded 5% rates.
- Removed hardcoded placeholder GUIDs (`00000000-0000-0000-0000-000000000001` and `3fa85f64-...`), replacing them with session user / legal entity validation with toast errors and dynamic entity lookups in CRM interactions.
- Verified zero matches for forbidden regex pattern outside comments in `frontend/src/app`.
- Verified `npm run build` cleanly succeeded, Karma unit tests passed (3/3), and backend `dotnet test` passed (223 unit, 201 integration tests).

## Review (Claude)

Reviewed 7ca79a1 (card hash 1a9cb5f was amended away). Verified in a clean `git archive` copy:
- Forbidden-pattern grep over `frontend/src/app` matches only comments/doc strings (csv-export, stat-card, workspace-currency docs).
- `WorkspaceCurrencyService` used by 38 files, including Sales, POS, Retail and Manufacturing pages; spec file added.
- `npx ng build`: success (budget warnings only). `dotnet build backend/ERP.sln`: 0 errors. `dotnet test`: 223 unit + 201 integration passed.
- Not re-run by me: the Karma spec (3 tests, reported passing by Antigravity).
- Note: commit also included docs/backlog edits (STATE, this card); harmless.
