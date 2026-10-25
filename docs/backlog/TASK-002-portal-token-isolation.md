---
id: TASK-002
title: Block portal tokens from staff endpoints with a fallback policy
priority: P0
status: open
schema: no
depends_on: []
area: platform
audit_ref: AUD-012
commit:
---

## Problem
Portal JWTs carry the `Org` claim and pass plain `.RequireAuthorization()`. Endpoints guarded only by authentication (`POST /approvals`, `/notifications`, `/dashboard`, `/search`, `/custom-fields/form/*`, `GET /imports`, `/auth/change-password`, `/auth/2fa/*`, `/me`) are reachable with a tenant or vendor token (`Program.cs:96-98,193-202`, `PortalAuth.cs:66`).

## Impact
A tenant or vendor could create approval requests or hit staff-only paths.

## Change wanted
- Define a default and fallback authorization policy that requires an authenticated user **without** the `portal_party` claim.
- Keep the existing `portal`, `portal.customer`, `portal.vendor` policies on `/portal/*` routes only.
- Make sure `/auth/*` endpoints that portal users need (none) are not affected.

## Acceptance criteria
- [ ] Portal token gets 403 on `POST /api/v1/approvals`, `GET /api/v1/dashboard`, `GET /api/v1/search`, `GET /api/v1/me`.
- [ ] Portal token still works on `/api/v1/portal/*`.
- [ ] Staff tokens behave as before.

## Tests required
Integration tests in `TenantPortalTests`/`VendorPortalTests` for the denied and allowed cases.

## Do not touch
Token contents and portal login flow.

## What I did (Antigravity)

## Review (Claude)
