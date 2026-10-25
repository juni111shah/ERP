---
id: TASK-001
title: Authorize attachments by the owning record's permission
priority: P0
status: open
schema: no
depends_on: []
area: platform
audit_ref: AUD-011
commit:
---

## Problem
`core.attachments.read` lets a user download or list any attachment in the workspace regardless of the owning record (`AttachmentFeatures.cs:63-83`, `WorkflowEndpoints.cs:70`). `OwnerType`/`OwnerId` are not validated against a real entity. Download returns the client-supplied `ContentType`.

## Impact
A user without `hr.employees.read` or `realestate.leases.read` can read those records' documents. Wrong or hostile content types are served back.

## Change wanted
- Add an `IAttachmentOwnerResolver` (Application abstraction) with one registration per owner type (core entities and each module's owner types). It checks the owner exists in the current workspace and returns the read and write permission keys.
- In upload, list, download and delete handlers, require the owner's permission as well as `core.attachments.*`, and reject unknown owner types or ids with `FieldValidationException`/`NotFoundException`.
- Derive the content type from the stored file extension (fixed allow-list map) when serving; ignore the client value.
- Fix `LocalFileStore.PathFor` to compare against `Root + Path.DirectorySeparatorChar`.

## Acceptance criteria
- [ ] A user with `core.attachments.read` but without the owner's read permission gets 403 on list and download.
- [ ] Upload with an unknown owner type or a non-existent owner id returns 400/404.
- [ ] Served `Content-Type` matches the extension, not the uploaded header.
- [ ] A sibling-directory key (for example `..\storage2\x`) is rejected by the file store.

## Tests required
Integration tests for each of the four cases above; unit test for the path check.

## Do not touch
Attachment table schema; existing upload size and extension rules.

## What I did (Antigravity)

## Review (Claude)
