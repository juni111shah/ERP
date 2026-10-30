---
id: TASK-100
title: Failed password or 2FA confirmations count toward lockout and are rate limited
priority: P1
status: done
schema: no
depends_on: []
area: auth
audit_ref: Claude analysis, Security and tenancy
commit: dab33f2
---

## Problem
`AuthService.ChangePasswordAsync` (ERP.Infrastructure/Auth/AuthService.cs ~249) and `DisableTwoFactorAsync` / `EnableTwoFactorAsync` (~108-125) check the current password or TOTP code but never call `AccessFailedAsync`, so wrong guesses do not lock the account. The endpoints `/auth/change-password` and `/auth/2fa/*` (AuthEndpoints.cs) are `RequireAuthorization()` only, with no `RequireRateLimiting`.

## Impact
Anyone holding a stolen access token (or an unattended session) can brute-force the current password, or the 6-digit TOTP needed to turn two-factor off, with unlimited attempts, bypassing the lockout that protects `/auth/login`.

## Change wanted
- In ChangePasswordAsync, DisableTwoFactorAsync and EnableTwoFactorAsync call `users.AccessFailedAsync(user)` on a wrong password/code and throw `Locked(user)` when the account becomes locked out; reset the counter on success. Also refuse the call with 423 while the user is locked out.
- Add `.RequireRateLimiting("login")` to change-password and the 2fa setup/enable/disable endpoints.

## Acceptance criteria
- [x] Five wrong current passwords on change-password lock the account (login then returns 423).
- [x] A wrong 2FA code on disable counts as a failed access.
- [x] The endpoints are rate limited with the login policy.
- [x] A correct change still works and resets the counter.

## Tests required
`AuthTests` (or new `AuthLockoutTests`): wrong password x5 locks; correct change after fewer failures succeeds.

## Do not touch
Login flow, token lifetimes.

## What I did (Claude, on user request)
ChangePassword, DisableTwoFactor and EnableTwoFactor now call AccessFailedAsync on a wrong password/code, refuse with 423 while locked and reset the counter on success; change-password and 2fa setup/enable/disable carry the login rate-limit policy. Tests in SessionSecurityTests.


## Review (Claude)
