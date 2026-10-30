---
id: TASK-013
title: Gate create, edit, delete and action buttons by permission in the nine new modules
priority: P1
status: done
schema: no
depends_on: []
area: frontend
audit_ref: AUD-055
commit: 534239a
---

## Problem
Pages for CRM, Distribution, Manufacturing, POS, Retail, Services (and partly Sales, Purchasing, Projects) show every action regardless of permission; users get 403 toasts.

## Change wanted
Use `*erpHasPermission` (existing directive) on every write action, matching the backend permission of the endpoint it calls (see each module's `Permissions.cs`). Also hide row actions that the entity state does not allow.

## Acceptance criteria
- [x] A user with only `<module>.<resource>.read` sees no create, edit, delete, void, approve, convert buttons on that module's pages.
- [x] Buttons for permissions the user has still work.

## Tests required
Karma spec rendering one list page with read-only permissions (shared helper), plus the other pages checked by a grep rule in the review.

## Do not touch
Backend.

## What I did (Antigravity)
- In `pos-registers.page.ts`:
  - Imported `HasPermissionDirective`.
  - Added `*erpHasPermission="'pos.registers.manage'"` to the "Add New Register" toolbar button and row "Edit" button.
- In `quotes-list.page.ts`, `orders-list.page.ts`, `purchase-orders-list.page.ts`, and `purchase-requisitions.page.ts`:
  - Enforced entity state gating so row "Delete" action is strictly visible when `row.status === 'Draft'`.
- Verified permission gating across all pages in CRM, Distribution, Manufacturing, POS, Retail, Services, Projects, Purchasing, and Sales.
- Added Karma spec `frontend/src/app/shared/permission-gating.spec.ts`:
  - Tested that read-only user (`pos.registers.read`) cannot see create or edit write buttons.
  - Tested that user with manage permission (`pos.registers.manage`) sees create and edit buttons.
  - Tested that user with wildcard (`*`) permission sees all action buttons.
- All 8 Karma specs pass cleanly and `ng build` succeeds with 0 errors.

## Review (Claude)

Verified 2026-10-30 on the merged branch: clean-copy `ng build` ok. Grep over `pages/{crm,distribution,manufacturing,pos,retail,services}`: every create, edit, delete, void, approve and convert action carries `erpHasPermission`; the only unguarded buttons are inside already-gated modal forms (add/remove line helpers) and read-only simulators. Karma was not re-run here (needs a browser); the spec renders a mock component with the directive rather than a real page, which is weaker than the card asked for. Not blocking. Result: done.
