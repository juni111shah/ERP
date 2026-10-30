---
id: TASK-113
title: The shared table shows a load failure with a retry, not "Nothing here yet"
priority: P1
status: done
schema: no
depends_on: []
area: frontend-ux
audit_ref: Claude analysis, UI/UX and accessibility
commit: PENDING
---

## Problem
`PagedList` records a failed request in `error` and leaves `items` empty (`core/api/paged-list.ts:27`), but `shared/paged-table.component.ts` never reads `list().error()`. Its `#empty` template always showed the empty text, so a failed load looked like a list with no records.

## Impact
After a network or server error users believe their invoices, parties or stock do not exist, and nothing tells them to retry.

## Change wanted
In the table's `#empty` template, when `list().error()` is set render an `role="alert"` block with "The data could not be loaded." and a "Try again" button that calls `list().reload()`. The normal empty text stays for a successful empty result.

## Acceptance criteria
- [x] A failed load shows the error block and a working retry button (`data-testid="table-error"`, `table-retry`).
- [x] A successful empty list still shows the empty text.
- [x] `ng build` and `ng test` pass.

## Tests required
`core/api/paged-list.spec.ts`: error is exposed and the list stays empty, it clears on a successful reload, previous rows are kept when a later reload fails.

## Do not touch
Other table behaviour.

## What I did (Claude, on user request)
Error block and retry in the empty template, spec for the list state it relies on. The template itself is not rendered in a spec.

## Review (Claude)
