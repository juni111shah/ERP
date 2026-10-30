---
id: TASK-122
title: Member list builds roles with lookups; history and role changes load only the users they need
priority: P2
status: done
schema: no
depends_on: []
area: performance
audit_ref: Claude analysis, Performance
commit:
---

## Problem
`UserDirectory.ListMembersAsync` (`ERP.Infrastructure/Identity/UserDirectory.cs`) loads every account entity and every user-role row, then for each account runs `members.First(...)` and two `userRoles.Where(...)` scans (O(users x rows)). It is also called for a single record: `RecordHistoryHandler` (`Audit/RecordHistory.cs:45`) lists the whole workspace to resolve the handful of names on one page, and `SecurityFeatures.SummaryAsync` lists everyone to return one user after a role/active change.

## Impact
Every record-history page view and every role change costs a full workspace member scan; it grows with users x roles.

## Change wanted
- Build the result from dictionaries and a lookup, project only needed columns.
- Add `IUserDirectory.ListMembersAsync(IReadOnlyCollection<Guid> userIds, ct)` and use it in `RecordHistoryHandler` (ids on the page) and `SummaryAsync` (one id).

## Acceptance criteria
- [ ] `GET /users` returns the same members, roles, ordering.
- [ ] History names resolve as before; role change returns only that user.

## Tests required
`UserDirectoryTests`; existing history and users tests pass.

## Do not touch
Identity, memberships schema.

## What I did (Claude, on user request)
Rewrote the directory query and added the filtered overload.

## Review (Claude)
