---
id: TASK-103
title: Guard test: every API endpoint declares authorization
priority: P2
status: done
schema: no
depends_on: []
area: security
audit_ref: Claude analysis, Security and tenancy
commit: 73fcdb0
---

## Problem
Authorization is declared per endpoint (`RequireAuthorization`/`RequirePermission`/`AllowAnonymous`); nothing prevents a future endpoint from being mapped without any. Analysis at HEAD found all 510 api endpoints declared correctly; state-changing endpoints that use plain `RequireAuthorization()` (settings PUT, imports POST, custom-field values PUT, real-estate unit/work-order status, lease create, deal stage) do their permission check in the handler or command (`IRequirePermission`, per-key checks in UpdateSettings and RunImport.FindAsync, LoadAsync in custom fields).

## Impact
A new endpoint added without authorization metadata would fall back to the staff default policy only if the FallbackPolicy applies; a guard test makes the contract explicit and catches regressions.

## Change wanted
- Add `EndpointAuthorizationTests` that enumerates the `EndpointDataSource` and fails for any `api/` endpoint with neither `IAuthorizeData` nor `IAllowAnonymous` metadata, and requires more than 100 endpoints to be found.

## Acceptance criteria
- [x] Test passes today and fails when an endpoint is mapped without authorization.

## Tests required
`EndpointAuthorizationTests.Every_api_endpoint_is_either_authorized_or_explicitly_anonymous`.

## Do not touch
Endpoint behaviour.

## What I did (Claude, on user request)
Added the test only; no endpoint changes were needed.

## Review (Claude)
