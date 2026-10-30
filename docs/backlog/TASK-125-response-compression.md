---
id: TASK-125
title: Compress JSON and the served SPA with Brotli/gzip
priority: P2
status: done
schema: no
depends_on: []
area: performance
audit_ref: Claude analysis, Performance
commit:
---

## Problem
`ERP.Api/Program.cs` registers no response compression. The API serves JSON lists and, in deploy packages, the compiled Angular app from wwwroot (about 1.75 MB of initial JS and 765 kB of CSS raw, 289 kB / 66 kB compressed).

## Impact
Every deployment that is not behind a compressing proxy sends the full uncompressed size on first load and for every list response.

## Change wanted
`AddResponseCompression(o => o.EnableForHttps = true)` and `UseResponseCompression()` early in the pipeline (after forwarded headers). Default MIME types only (text, JSON, JS, CSS, SVG); event streams and SignalR are not in the list. Authentication is by bearer token, so there is no cookie-based secret for a BREACH-style attack to target; revisit if cookie auth is added.

## Acceptance criteria
- [ ] A request with `Accept-Encoding: gzip` gets `Content-Encoding: gzip`; without it, no encoding.

## Tests required
`ResponseCompressionTests`.

## Do not touch
Endpoints, SignalR.

## What I did (Claude, on user request)
Added the service and middleware. Frontend lazy loading was reviewed: 93 routes already use `loadComponent`, so no change there.

## Review (Claude)
