---
id: TASK-128
title: Allocate receipt, void receipt, void invoice and update invoice retry on concurrency conflicts
priority: P1
status: ready-for-review
schema: no
depends_on: []
area: sales
audit_ref: Claude analysis, Order to cash (second pass)
commit: 710861a
owner: claude (worktree work-claude; Antigravity skip)
---

## Problem
`CreateReceipt`, `IssueInvoice` and `ApplyCreditNote` implement `IRetryOnConcurrency`, but `AllocateReceipt` and `VoidReceipt` (backend/src/ERP.Application/Finance/ReceiptFeatures.cs:34-36), `VoidInvoice` and `UpdateInvoice` (InvoiceFeatures.cs:36,40) do not. They all change `Invoice.Paid`, `Settlement.Unallocated`, status or journals.

## Impact
Two clerks allocating receipts to the same invoice, or voiding while another allocates, get a raw conflict error instead of a clean retry. If `Invoice` lacks a concurrency token, `Paid` can be overwritten (lost update). Same defect family as TASK-021 and TASK-023.

## Change wanted
- Add `IRetryOnConcurrency` to the four commands.
- Confirm `Invoice` and `Settlement` carry the `RowVersion` token (backend/src/ERP.Domain/Common/Entity.cs:61) and are configured as concurrency tokens in EF. If not, add them and set `schema: yes` with a SQL script under `database/scripts`.
- Handlers already load inside `Handle`, so a retry re-reads fresh state; keep it that way.

## Acceptance criteria
- [x] The four commands implement `IRetryOnConcurrency`.
- [x] Two parallel allocations to one invoice never leave `Paid` greater than `Total`; one succeeds and the other is rejected cleanly or succeeds after retry.
- [x] A void racing an allocation ends consistent (receipt voided and invoice `Paid` reduced, or the allocation rejected).

## Tests required
Concurrency tests on the SQL Server host from TASK-022 (skip cleanly when unavailable), plus a test that the four commands are marked retryable.

## Do not touch
Posting service and journal shapes.

## What I did (Claude, implemented in separate worktree)
- Added `IRetryOnConcurrency` to `AllocateReceipt`, `VoidReceipt`, `UpdateInvoice`, `VoidInvoice`. Handlers still load state inside `Handle`, so a retry re-reads fresh data.
- Confirmed `Invoice` and `Settlement` already carry the `RowVersion` token (it sits on `OrgEntity` and EF configures it for every entity on SQL Server), so no schema change.
- Tests: marker test for the four commands (and the earlier four); SQL Server collection `ReceiptConcurrencyTests`: two parallel 800 allocations to a 1000 invoice leave Paid = 800 exactly; a void racing an allocation ends consistent. Run with `ERP_TEST_SQL` set (I ran them on LocalDB: pass; without it they skip).
- Side effect: the 20-parallel-receipts test from TASK-021/022 now also ran and passed on SQL Server, which closes the gap noted on TASK-021.

## Review (Claude)
