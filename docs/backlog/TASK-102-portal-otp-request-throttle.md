---
id: TASK-102
title: Throttle portal sign-in code requests per customer or vendor
priority: P2
status: done
schema: no
depends_on: []
area: portal
audit_ref: Claude analysis, Security and tenancy
commit: 3a00df0
---

## Problem
`PortalAuthService.RequestCodeAsync` (ERP.Infrastructure/Auth/PortalAuth.cs ~30) issues and emails a new code on every call. The only limit is the shared per-IP `login` window (10/min, Program.cs ~107), which is keyed by IP and shared with verify.

## Impact
An attacker with many IPs (or patient at 10/min) can flood a customer's inbox with sign-in codes and get 5 fresh guesses at a 6-digit code per request.

## Change wanted
- Limit codes to 5 per party per hour (count `PortalOtps` created in the last hour for the party); beyond that, return the same 204 without creating or sending a code.
- Keep the message identical so existence is not revealed.

## Acceptance criteria
- [x] The sixth request within an hour sends no email and creates no new OTP; responses stay 204.
- [x] Normal sign-in works.

## Tests required
Portal test: six `request-code` calls produce five OTP rows/messages.

## Do not touch
Portal token claims, verify flow.

## What I did (Claude, on user request)
RequestCodeAsync returns silently (same 204) once a party has five codes created in the last hour. Test: sixth request sends nothing and the fifth code still verifies.


## Review (Claude)
