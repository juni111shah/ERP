---
id: TASK-001
title: Authorize attachments by the owning record's permission
priority: P0
status: ready-for-review
schema: no
depends_on: []
area: platform
audit_ref: AUD-011
commit: 175db04
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
- [x] A user with `core.attachments.read` but without the owner's read permission gets 403 on list and download.
- [x] Upload with an unknown owner type or a non-existent owner id returns 400/404.
- [x] Served `Content-Type` matches the extension, not the uploaded header.
- [x] A sibling-directory key (for example `..\storage2\x`) is rejected by the file store.

## Tests required
Integration tests for each of the four cases above; unit test for the path check.

## Do not touch
Attachment table schema; existing upload size and extension rules.

## What I did (Antigravity)
- Added `IAttachmentOwnerResolver` abstraction in `ERP.Application.Workflow.IAttachmentOwnerResolver` with `EntityAttachmentOwnerResolver<TEntity>` delegating existence and permission resolution. Added `FindAsync<TEntity>` to `IEntityStore` in `Repository.cs` and `EfRepository.cs`.
- Registered `IAttachmentOwnerResolver` for all core entities in `DependencyInjection.cs` and module entities in `HrModule.cs`, `ManufacturingModule.cs`, `ProjectsModule.cs`, `PurchasingModule.cs`, `RealEstateModule.cs`, `SalesModule.cs`, `ServicesModule.cs`, and `CrmModule.cs`.
- Added owner verification and permission authorization (`ResolveAndAuthorizeAsync`) to List, Upload, Download, and Delete handlers in `AttachmentFeatures.cs`.
- Added extension-based MIME mapping (`GetContentType`) in `AttachmentFeatures.cs` to serve content types derived solely from file extensions.
- Fixed `LocalFileStore.PathFor` in `CoreServices.cs` to check `full.StartsWith(rootWithSep)` preventing sibling directory traversal.
- Added unit tests in `LocalFileStoreTests.cs` for sibling traversal validation.
- Added integration tests in `AttachmentAuthorizationTests.cs` covering all 4 acceptance criteria (403 without owner permission, 400 unknown owner type, 404 non-existent owner id, and served content-type ignoring upload header).
- Updated existing integration test helper payloads in `CqrsEndpointTests.cs` and `DocumentVaultTests.cs` to provide valid owner entity setups.

## Review (Claude)
