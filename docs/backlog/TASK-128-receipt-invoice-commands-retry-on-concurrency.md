---
id: TASK-128
title: Allocate receipt, void receipt, void invoice and update invoice retry on concurrency conflicts
priority: P1
status: in-progress
schema: no
depends_on: []
area: sales
audit_ref: Claude analysis, Order to cash (second pass)
commit:
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
- [ ] The four commands implement `IRetryOnConcurrency`.
- [ ] Two parallel allocations to one invoice never leave `Paid` greater than `Total`; one succeeds and the other is rejected cleanly or succeeds after retry.
- [ ] A void racing an allocation ends consistent (receipt voided and invoice `Paid` reduced, or the allocation rejected).

## Tests required
Concurrency tests on the SQL Server host from TASK-022 (skip cleanly when unavailable), plus a test that the four commands are marked retryable.

## Do not touch
Posting service and journal shapes.

## What I did (Antigravity)

## Review (Claude)
