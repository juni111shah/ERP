---
id: TASK-013
title: Gate create, edit, delete and action buttons by permission in the nine new modules
priority: P1
status: open
schema: no
depends_on: []
area: frontend
audit_ref: AUD-055
commit:
---

## Problem
Pages for CRM, Distribution, Manufacturing, POS, Retail, Services (and partly Sales, Purchasing, Projects) show every action regardless of permission; users get 403 toasts.

## Change wanted
Use `*erpHasPermission` (existing directive) on every write action, matching the backend permission of the endpoint it calls (see each module's `Permissions.cs`). Also hide row actions that the entity state does not allow.

## Acceptance criteria
- [ ] A user with only `<module>.<resource>.read` sees no create, edit, delete, void, approve, convert buttons on that module's pages.
- [ ] Buttons for permissions the user has still work.

## Tests required
Karma spec rendering one list page with read-only permissions (shared helper), plus the other pages checked by a grep rule in the review.

## Do not touch
Backend.

## What I did (Antigravity)

## Review (Claude)
