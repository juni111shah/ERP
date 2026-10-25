---
id: TASK-003
title: Replace blanket InvalidOperation/KeyNotFound mapping with domain exceptions
priority: P1
status: open
schema: no
depends_on: []
area: platform
audit_ref: AUD-010
commit:
---

## Problem
`Middleware.cs` now maps every `InvalidOperationException` to 409, `KeyNotFoundException` to 404 and `ArgumentException` to 400 and returns the message. Framework or programmer errors become 4xx with internal text and are never logged as errors. The modules POS, Services, CRM, Manufacturing, Distribution and Retail throw these raw types.

## Impact
Real bugs are hidden; internal messages leak; clients cannot tell a rule violation from a defect.

## Change wanted
- In every module handler under `backend/modules/*/Features`, replace `throw new KeyNotFoundException` with `NotFoundException`, `InvalidOperationException` rule checks with `ConflictException` (state) or `DomainException` (rule), `ArgumentException` with `FieldValidationException`.
- Remove the three catch-all mappings from `ProblemExceptionHandler` so they fall to the 500 branch (logged).

## Acceptance criteria
- [ ] `grep -rE "throw new (KeyNotFound|InvalidOperation|Argument)Exception" backend/modules` returns nothing in handlers.
- [ ] "Order not found" returns 404; "closed session" returns 409.
- [ ] An unexpected `InvalidOperationException` returns 500 and is logged.

## Tests required
Integration tests for one not-found and one conflict per affected module; a test that an unmapped exception gives 500.

## Do not touch
Message wording users already see, unless needed.

## What I did (Antigravity)

## Review (Claude)
