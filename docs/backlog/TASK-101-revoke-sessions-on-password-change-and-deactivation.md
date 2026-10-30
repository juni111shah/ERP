---
id: TASK-101
title: Password change, reset and user deactivation invalidate existing access tokens
priority: P1
status: done
schema: no
depends_on: []
area: auth
audit_ref: Claude analysis, Security and tenancy
commit: 8845580
---

## Problem
The JWT bearer pipeline (Program.cs `OnTokenValidated`, ~83) only rejects tokens whose jti is on the deny list. `ChangePasswordAsync`/`ResetPasswordAsync` revoke refresh tokens (`RevokeAllAsync`) and update the security stamp, but the stamp is never checked per request and other access tokens are not denied. `SetUserActive(false)` (SecurityFeatures.cs ~146) revokes nothing at all.

## Impact
A stolen or old access token keeps working for up to `AccessTokenMinutes` (5) after the owner changes their password, and a deactivated user keeps API access for the same window; any longer configured lifetime widens it.

## Change wanted
- Add a per-user cutoff in the distributed cache: key `jwt:user-cutoff:{userId}` = UTC unix seconds, TTL = access-token lifetime + 1 minute. `OnTokenValidated` fails a token whose `iat` is earlier than the cutoff.
- Set the cutoff in `RevokeAllAsync` (password change/reset) and, through a new `IAuthService`/session service method, when `SetUserActive(false)` runs; also revoke that user's refresh tokens for the workspace on deactivation.
- Tokens issued after the cutoff (the new session) must keep working.

## Acceptance criteria
- [x] After change-password, an access token issued before it gets 401 and the new session's token works.
- [x] After an admin deactivates a user, that user's existing access token gets 401 and refresh fails.
- [x] Existing auth tests pass.

## Tests required
`AuthTests`: old token rejected after password change; deactivated user rejected.

## Do not touch
Token format, claims, refresh rotation.

## What I did (Claude, on user request)
Access tokens carry an issue time in ms (claim iatms); RevokeSessionsAsync revokes refresh tokens and stores a per-user (optionally per-workspace) cutoff in the distributed cache for access-token lifetime; OnTokenValidated rejects older tokens. Called on password change/reset (all workspaces) and SetUserActive(false) (that workspace). Note: the caller's own access token also ends after change-password.


## Review (Claude)
