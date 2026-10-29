# ERP deep audit: page-by-page and feature-by-feature

Date: 2026-10-25. Code audited: HEAD `e31f147` (includes the stock-posting work added after the first audit, `docs/AUDIT-2026-10-25.md`).
Coverage and confidence:
- **Read in detail (code read, conclusions confirmed):** POS (all), Sales, Purchasing, Projects billing, Services milestones/retainers, Manufacturing work orders, Distribution trips, CRM lead conversion, Inventory stock-posting service, exception middleware, route/permission cross-check.
- **Heuristic scan only (counted loading/empty/error/permission/validation/CRUD usage per file):** every other page. Findings for those are marked *(scan)* and should be confirmed by running the page.
- **Not run:** the app was not started, so nothing here was observed in a browser. Items marked *(unverified)* need a runtime check.

Legend: **BUG** = wrong behaviour; **MISSING** = feature absent; **INCOMPLETE** = partly built; **RISK** = works but unsafe or inconsistent.

---

## A. Cross-cutting problems (affect many pages)

| # | Type | Problem | Evidence |
|---|---|---|---|
| A1 | BUG | **Business "not found / invalid state" errors return HTTP 500.** The exception middleware only maps `NotFoundException`, `ConflictException`, `DomainException`, validation etc. Modules throw plain `KeyNotFoundException` / `InvalidOperationException`, which fall to the `_ => 500` branch. Counts: POS 5 InvalidOp + 2 KeyNotFound, Services 2 + 5, CRM 5 KeyNotFound, Manufacturing 4, Distribution 2, Retail 2. Examples: "Cannot place POS orders in a closed session", "Order not found", "Work Order not found". Sales, Purchasing and Projects use the right exception types. | `backend/src/ERP.Api/Infrastructure/Middleware.cs:46-57`; module handlers |
| A2 | MISSING | **No UI permission gating in the nine new modules.** Core pages hide buttons with `*erpHasPermission`; CRM, Distribution, Manufacturing, POS, Retail and Services pages have zero checks (Sales, Purchasing and Projects have a few). Users without write rights see Create/Delete/Void buttons and get a 403 toast. *(scan)* | scan: `perm0` |
| A3 | MISSING | **Zero i18n** (no translate pipe, no dictionary keys) in Sales, Purchasing, POS, Retail, CRM, Projects, Services, Distribution, Manufacturing. Arabic/RTL users see English only. | scan: `tr0` on all 36 pages |
| A4 | BUG | **Hardcoded currency and tax.** "AED" is baked into POS (about 30 places), Sales/Purchasing/Quote forms (default `AED`), Retail price lists/promotions; Manufacturing and Costing show a hardcoded `$`. POS uses a hardcoded **5% VAT** in the cart total (`* 0.05`) and `taxRate: 5`; Sales/Quote/PO line forms default `taxRate` to 5. Ignores workspace currency, country pack and tax codes. | `pos-terminal.page.ts:612,780`, `orders-list.page.ts:206,337`, `costing.page.ts:145` |
| A5 | BUG | **Placeholder GUIDs sent to the API as real ids.** Services: engagement `clientId` and milestone/retainer `engagementId` default to `00000000-0000-0000-0000-000000000001`; services analytics `currentLegalEntityId` the same. POS and Distribution send `…0001` as cashier/driver/user id. Manufacturing BOM sends the Swagger sample GUID `3fa85f64-5717-4562-b3fc-2c963f66afa6` as both `outputItemId` and `componentItemId`. | `engagements-list.page.ts:321`, `retainers.page.ts:238-239`, `milestone-billing.page.ts:188`, `boms-list.page.ts:351,414`, `van-trips.page.ts:507,559`, `pos-terminal.page.ts:798,827`, `pos-sessions.page.ts:195` |
| A6 | BUG | **No document state machine.** `ChangeOrderStatus`, `ChangePurchaseOrderStatus`, `ChangeQuoteStatus`, `ChangeRequisitionStatus` set any status from any status (e.g. Draft to Invoiced/Billed with no invoice or bill; Delivered back to Draft). Manufacturing `Release` and `Complete` have no status guard at all. Across the modules there are no transition checks except "cannot edit/delete once Invoiced/Billed/Converted". | `OrderFeatures.cs:236-245` (Sales), `:234` (Purchasing), `WorkOrderFeatures.cs:200-260` |
| A7 | RISK | **Stock/ledger posting is not transactional with the document and fails silently.** Documents are saved first, then `IStockPostingService` runs in its own transaction. If stock posting throws, the document is already committed. The service also returns `null` and does nothing when Inventory is disabled, the item is not a Product, or no warehouse exists. Users get a success message with no stock or journal. | `StockPostingService.cs`; POS/Sales/Purchasing/Manufacturing/Distribution handlers |
| A8 | MISSING | **No ETag / If-Match** anywhere (AGENTS.md rule 9). No 412 handling. | grep over `src`, `modules` |
| A9 | RISK | **Client-supplied values trusted by the server:** POS `unitPrice`, `taxRate`, `discountPercent`, `cashierUserId`, `cashierName`; Manufacturing `ActualTotalCost`; Services/Projects dates. The server never recomputes price from the item or price list, and never takes the cashier from the logged-in user. | `OrderFeatures.cs` (POS), `CompleteWorkOrderCommand` |
| A10 | RISK | **Heavy `any` / `as any`** in module pages (`createPagedList<T>((q: any) => …)`, `payload as any`, `res: any`). API drift will not be caught at compile time. | scan: `any` column |
| A11 | RISK | **Error swallowing.** Several pages have no error handler or `catchError(() => of([]))` that hides failures: `project-detail.page.ts` (0 error handlers), `distribution-analytics`, `retail-analytics` (0), POS catalog and registers load (no `error:`), onboarding lists, legal-entities lists. *(scan)* | scan: `err0` |
| A12 | MISSING | **No loading indicators** on many pages (POS orders/sessions/registers, project detail/tasks/timesheets, goods receipts, PO detail, Sales/Quote detail, manufacturing). Blank panels while loading violate the UI standard. *(scan)* | scan: `load0` |
| A13 | RISK | **Numbering/date handling:** many handlers use `DateTime.UtcNow` / `DateTimeOffset.UtcNow` directly (invoice dates, due dates, stock dates) rather than the workspace time zone or a clock abstraction. A sale at 01:00 local time in UAE dates the previous day. | module handlers |

---

## B. Integration logic added since the first audit (review these first)

These are the stock/ledger posting changes the other agent just made. They compile and tests pass, but several are accounting-incorrect.

| # | Where | Problem |
|---|---|---|
| B1 | `StockPostingService.ReceiveStockAsync` | **Credits AP Control for every stock receipt** when no offset account is passed (falls back to "inventory" role only if the AP role is missing). A goods receipt therefore books Dr Inventory / Cr Accounts Payable. When the vendor bill is then posted (`ConvertPurchaseOrderToBill`) AP is credited a second time. Needs a GR/IR (goods-received-not-invoiced) clearing account. |
| B2 | Manufacturing `CompleteWorkOrder` | Finished goods are received at `ActualTotalCost / qty` (Dr Inventory, Cr **AP Control**). Components are then issued as **COGS** (Dr COGS, Cr Inventory). Result: raw materials expensed in P&L while finished goods also sit in inventory at cost, and a false liability is created in AP. Correct flow is Dr FG inventory, Cr RM inventory (and WIP/labour absorption). |
| B3 | Manufacturing `CompleteWorkOrder` | No status guard. Calling Complete twice posts stock and journals twice. Also `ActualTotalCost` is client-supplied, so the inventory value of production is user-typed. |
| B4 | POS `VoidPosOrder` | Returned stock is received at the **sale price** (`l.UnitPrice`), not the original cost, so inventory is valued at selling price after a void. Session `ExpectedCash` is not reduced, so a cash void leaves the drawer expectation overstated. Voiding an order in a **closed** session still alters that session's totals. |
| B5 | POS `CreatePosOrder` | Order, session totals and stock issue are three separate saves. No check that payments sum to the total (an under-paid order is still `Paid`). No check that tendered covers the amount. **No revenue/receipt/invoice journal** is posted; only COGS/inventory. Revenue and VAT never reach the ledger. |
| B6 | `IssueStockAsync` | No available-stock check, so negative stock is possible; average cost 0 gives a zero-value issue that posts no journal. Warehouse fallback `warehouses.FirstOrDefault(active)` can pick **another legal entity's** warehouse if the entity has none. |
| B7 | Sales `UpdateOrderFulfillment` and `ConvertOrderToInvoice` | Fulfillment can only increase delivered quantity (no returns). Converting to invoice auto-issues all undelivered lines, but if the invoice step then fails the stock is already out. Cancelling or reducing a delivered order does not return stock. Editing an order that has deliveries (`UpdateOrder` rebuilds lines) can lose `DeliveredQuantity` *(unverified)*. |
| B8 | Distribution trip reconcile | Issues only `Sold + Damaged` from stock; loaded stock is not moved warehouse to van, and returned stock is not received back. No invoice, receipt or cash entry is created for `TotalSalesAmount` / `TotalCollectionsAmount`. |
| B9 | Projects `BillProject` | If the project has no customer, timesheets are still set `IsBilled = true` and milestones `Invoiced` but **no invoice is created** (silent data loss of billable work). Invoice date and due date are hardcoded (`UtcNow`, +30 days) instead of customer terms. Invoice lines carry no tax code or revenue account. Flags are saved after the invoice, so a failure between the two double-bills on retry. |
| B10 | Services `InvoiceMilestoneCommand` | New endpoint exists, but the **UI does not use it**: the milestone page's "Mark as Invoiced" button calls the plain status update, so no invoice is generated. No guard that the milestone is `ReadyToInvoice`; due date can be earlier than invoice date; engagement client is the placeholder GUID from A5, so the invoice call would fail or bill a non-existent party. "Paid" is set by hand and is not linked to receipts. |
| B11 | Purchasing GRN (`ReceiptFeatures`) | Stock is received at PO unit price; no landed cost; no tolerance check against PO quantity; UI-created PO lines have no `ItemId` (see C-Purchasing), so this code path never runs from the UI. |

---

## C. Module-by-module, page-by-page

### C1. POS (`pages/pos`, backend `ERP.Modules.Pos`)

**pos-terminal.page.ts** (route `/pos/terminal`)
- BUG: cashier is fake. `submitOpenShift` sends `cashierUserId: 00000000-…-0001` and `cashierName: 'POS Cashier'`; close shift sends `closedByUserId: …0001`. Real logged-in user is never used (A5, A9).
- BUG: price fallback `salesPrice || 25.0`. Any item without a price is sold at 25 with no warning.
- BUG: catalog loads only the first 100 items (`pageSize: 100`), no paging/scroll; search is client-side on those 100 only. No barcode field despite "Scan barcode" placeholder (matches code/name only). Catalog load failure is silent.
- BUG: VAT is hardcoded 5% (A4); per-line `lineTotal` multiplies by `1.05` separately from the cart total.
- BUG: Cash/Card payment is a single tender; no split payment, mixed cash/card, or refund. Card reference optional. Tendered cash lower than total is not blocked in the UI. (Backend accepts it, B5.)
- MISSING: no hold/recall order, no discount entry in the cart (discountPercent always 0), no customer lookup (free-text `customerName`, `CustomerPartyId` never sent), no promotions or price lists applied (Retail module not wired), no return/exchange, no keyboard shortcuts, no offline mode, no receipt reprint from terminal.
- MISSING: no loading state; empty-cart state only. No permission gating (needs `pos.orders.create` in UI too).

**pos-orders-list.page.ts**: void-only (no view of payments breakdown beyond the receipt, no refund tender, no partial return, no filters by register/cashier/date). "AED" hardcoded. No loading state.

**pos-sessions.page.ts**: can record drawer transactions with a fake user id (A5); there is no way to **open or close** a shift here (only in the terminal); no session detail/Z-report; "Amount (AED)" hardcoded.

**pos-registers.page.ts**: create/edit only; no delete/deactivate from UI though the backend has `DELETE`; no link to warehouse/store (Retail stores not connected). Loads entities with `any`.

**pos-analytics.page.ts**: KPIs only, hardcoded AED; no date range.

Backend POS: `GET /sessions` list exists; no `GET /sessions/{id}`, no `PUT /orders` (UI wrapper expects it). Fixes needed: A1, A9, B4, B5.

### C2. Sales (`pages/sales`, `ERP.Modules.Sales`)

**quotes-list / orders-list**
- BUG (blocking): the create form has **no customer picker and no item picker**. It posts free-text `customerName` and free-text `itemCode`/`description`; `customerPartyId` and `itemId` are never sent. Consequences: `ConvertOrderToInvoice` always throws "Sales order must have an assigned Customer Party" for UI-created orders; stock issue (B7) never runs (no `ItemId`). The Sales to Finance link works only via direct API calls.
- MISSING: no edit from the list or detail (backend `PUT` exists, UI has create/delete only); no duplicate, print/PDF, email, attachments, custom-fields rendering, or sales-person/discount header fields; no price list or customer-specific pricing; currency typed as free text, defaults AED.
- MISSING: quote expiry/valid-until handling; no revision history.
- Order number can be typed manually (`number` field), bypassing numbering (conflict check exists).
- Pagination/sort/filter via shared table OK.

**order-detail / quote-detail**: status buttons expose Confirm/Cancel/Sent/Accepted/Declined only; "Processing", "Delivered" reachable only through fulfillment. No stock availability display, no linked invoice/shipment panel (the invoice id is stored but not shown *(scan)*), no loading state.

**sales-analytics**: KPIs only.

### C3. Purchasing (`pages/purchasing`, `ERP.Modules.Purchasing`)

**purchase-orders-list**: same defect as Sales: no vendor picker, no item picker (`purchasing` has 0 party refs and 2 item refs), free-text currency (AED), 5% default tax. `ConvertPurchaseOrderToBill` therefore fails ("must have an assigned Vendor Party") for UI-created POs; GRN stock (B11) never triggers.
**purchase-order-detail**: Approve and Cancel only; no edit, no print/PDF/email to vendor, no receive-all shortcut, no three-way match (PO/GRN/Bill), no approval-rule integration with the core approvals engine (core has configurable approval policies; POs ignore them).
**purchase-requisitions**: create, approve, convert (to PO), delete. No reject action, no edit, no budget check, no requester auto-fill.
**goods-receipts**: view and delete only. Deleting a receipt that already posted stock and a journal does **not** reverse the stock movement/journal (RISK; backend delete only adjusts PO status). No create-from-PO list entry (only from PO detail), no quality inspection or partial-reject.
**purchasing-analytics**: KPIs only.

### C4. CRM (`pages/crm`, `ERP.Modules.Crm`)
- **leads-list**: create, view, convert. No edit, no delete, no status change (qualify/disqualify/contacted), no assignment UI (assigned user is typed or empty), no import, no duplicate check, no activity timeline per lead.
- Lead **convert** creates only an Opportunity (free-text customer name). It does **not** create a Party/customer (`ConvertedPartyId` exists on the DTO but is never set) and does not link to Sales quotes. So won deals cannot flow to Sales or to invoicing.
- **opportunities-pipeline**: stage buttons are hardcoded (`advanceStage(row,'Negotiation',80)` etc.) with fixed probabilities; no drag-and-drop Kanban; close modal (won/lost) but no follow-on (create quote/order/customer); no edit (PUT/DELETE routes missing in backend), no reopen.
- **interactions**: create and "resolve" only; no edit/delete (backend routes missing, A-list in first audit); linking to a lead/opportunity/customer is by id via `by-entity` query *(unverified in UI)*.
- **crm-analytics**: funnel and win-rate KPIs; `GET /analytics/kpis` backend route is not called by the UI (UI uses a different path) *(unverified)*.
- No permission gating, no i18n, 0 confirmation dialogs (A2, A3).

### C5. Projects (`pages/projects`, `ERP.Modules.Projects`)
- **projects-list**: create/edit through the shared record-drawer (best-built module page). No delete/archive from UI.
- **project-detail** (870 lines): tabs Overview, tasks, timesheets, milestones, billing. No error handlers or loading state at all (`err0`, `load0`) *(scan)*; if any call fails the tab stays blank. Achieve-milestone and Generate-billing buttons act without confirmation.
- **project-billing**: lists unbilled amounts; "Bill" now returns an invoice reference (new) but see B9 (silent flag-without-invoice when no customer; hardcoded terms).
- **project-tasks / timesheets**: create only, no edit/delete/assign/status drag; timesheet approval workflow absent (anything logged and `IsBillable` is billable, no approval step); hourly rate typed per entry, not resolved from rate cards; no budget vs actual hours alert; no expense tracking.
- No resource planning, Gantt/dependencies, or project profitability (cost side missing: no link to payroll/bills).

### C6. Retail (`pages/retail`, `ERP.Modules.Retail`)
- **stores-list**: create/edit; no delete/deactivate; not linked to POS registers, warehouses or cost centres.
- **pricelists**: create and view items; no edit, no import/bulk update, no effective-date overlap check, **not consumed by POS or Sales** (A-integration).
- **promotions**: create and a "simulator" (test code); no edit/pause; promotions are never applied at the POS terminal or on Sales orders; promo code usage limits/redemption counts not tracked *(unverified)*.
- **retail-analytics**: no error handler (`err0`).
- Hardcoded AED in labels (Fixed Amount (AED), Min Order (AED)).

### C7. Distribution (`pages/distribution`, `ERP.Modules.Distribution`)
- **routes-list**: create, add stops, view; route update exists in code but delete is not exposed; stop re-sequencing/drag absent; customer picker uses the real Parties API (good).
- **van-trips**: dispatch trip (loaded items), reconcile (sold/damaged/returned), view manifest. No edit/cancel trip (backend PUT/DELETE missing), driver and vehicle are free-text with placeholder driver id (A5), no stock availability check against the warehouse when loading, no per-customer sales lines (only totals), no cash/invoice creation (B8). Setting `distribution.strictInventoryLoading` is displayed but the capacity check is not enforced against real stock *(unverified)*.
- **wholesale-pricing**: matrix create + calculator; no edit/delete; not used by Sales when pricing an order.
- **distribution-analytics**: no error handling or loading (`err0`, `load0`).

### C8. Manufacturing (`pages/manufacturing`, `ERP.Modules.Manufacturing`)
- **boms-list**: create BOM, view, delete (no confirmation). The create form sends the Swagger sample GUID as output and every component item (A5), so BOMs cannot reference real items; no item picker, no unit-of-measure conversion, no version/active flag, no edit, no routing/operations editor.
- **work-orders**: schedule, release, complete, cancel. Release/Cancel buttons have no confirmation; completion form takes `ActualTotalCost` from the user (A9, B3); no component availability check on release, no material reservation, no scrap reason, no partial completions. Because BOM item ids are placeholders, `ProducedItemId`/`ComponentItemId` checks in the stock-posting block (`!= Guid.Empty`) are always true with a non-existent item, so receive/issue silently no-op (A7).
- **costing**: rollup and variances; shows `$`; variance backend routes (`/costing/variance`, `/rollup/{id}`) are partly unused by the UI; no cost-element setup (labour/overhead rates).
- **manufacturing-analytics**: KPIs only.

### C9. Services (`pages/services`, `ERP.Modules.Services`)
- **engagements-list**: create, change status, details. `clientId` defaults to a fake GUID (A5) with no customer picker, so every engagement points to a non-existent client; no edit/delete (backend PUT/DELETE missing); hours/financial panels rely on time entries which have **no UI at all** (no time-entry page exists though the API and `time-entries` routes do).
- **retainers**: create, pause/activate. **No billing run** to turn a retainer into recurring invoices (no endpoint/handler); `RecurringFee` only feeds an MRR KPI. Fake `engagementId`/`clientId` defaults.
- **milestone-billing**: see B10 (status-only invoicing in the UI).
- **services-analytics**: uses the fake legal-entity id `…0001` (`currentLegalEntityId`), so KPIs may query the wrong or empty entity *(unverified)*.

### C10. Inventory, HR, Real Estate (older modules, *(scan)* unless noted)
- **inventory/warehouses**: create only (45 lines): no edit, deactivate, default-warehouse flag. **stock**/**movements**: read-only lists; no filters (item, warehouse, date) beyond search *(scan)*; stock actions listed in the menu (`receive`, `issue`, `adjust`, `transfer`) exist as backend routes, but the UI has no pages for them (`modules/inventory` has only 3 pages) *(to confirm in the UI)*. No lot/serial, UoM, reorder-level UI.
- **hr/employees**: create only (`upd0`), no edit/terminate/documents; **payroll**: list + detail with approve/pay; **attendance**, **review-cycles**, **reviews**: basic forms with no validation (`valid0`). No leave management, shifts, or employee self-service.
- **realestate/**: the most complete module. Gaps seen in the scan: `properties.page.ts`, `property-detail`, `unit-types`, `units` have no validators (`valid0`) and no confirmation on destructive actions (`conf0`); `leases.page.ts` and `properties.page.ts` have no error handler (`err0`); `status-tag` and several pages use `any`.

### C11. Core / finance / admin pages (scan)
- **No delete or deactivate action** on Parties, Items, Tax codes, Legal entities, Business units, Accounts (`del0`), even though AGENTS.md requires soft delete for master data. Users page has activate/deactivate only.
- **Items** page (82 lines): create/edit only; no UoM, barcode, price list, category, inventory flags, or image; hence the POS "SKU" fallback `name.substring(0,4)`.
- **audit, documents, tax-codes, items, business-units, roles, settings** pages have **no i18n** (`tr0`).
- **audit / documents** pages have no error handling (`err0`).
- **bills.page / invoices.page / receipts / payments**: validation present; `invoice-detail`, `bill-detail`, `journal-detail`, `receipt-detail`, `payment-detail` have no permission checks on action buttons (`perm0`) *(scan)* and rely on server 403.
- **dashboard.page.ts** (1,610 lines) and **print.page.ts** (1,618 lines) and **shell.component.ts** (1,366 lines) are monolithic; dashboard has one error handler for all widgets and no per-widget retry *(scan)*.
- **login.page.ts** (841 lines): large; **portal** pages have empty-state gaps (`empty0`).
- **Email**: invoice/receipt "send email" asks for a recipient but SMTP is off by default; earlier docs mark some email buttons as stubs (SQA L-2). Not re-verified.

---

## D. Backend / API-level findings (beyond A and B)

1. **Missing routes the UI wrapper exposes** (inherited CRUD that will 404/405): CRM `DELETE /leads`, `PUT|DELETE /opportunities`, `GET|PUT|DELETE /interactions/{id}`; Distribution `PUT|DELETE /trips`; Manufacturing `PUT|DELETE /workorders`; POS `/sessions` CRUD and `PUT|DELETE /orders`; Purchasing `PUT /receipts`; Services `PUT|DELETE` for engagements, retainers, milestones, time-entries and `GET` for milestones/time-entries.
2. **Time entries (Services)**: backend API exists, no UI page, no menu entry.
3. **Idempotency:** none of the create-from-document commands (POS order, GRN, trip reconcile, WO complete, milestone invoice) accept an idempotency key; double-click or retry can duplicate postings (milestone invoice and `ConvertOrderToInvoice` are idempotent via stored ids; the rest are not).
4. **Concurrency:** no row version / ETag; two cashiers updating the same session total overwrite each other (`session.TotalSales += …` read-modify-write).
5. **Validation gaps:** POS payment amounts, `TenderedAmount >= Amount`, currency codes against the currency list, dates in the past/future, discount > 100 on POS lines (validator only checks quantity and price), Services/Projects rate ≥ 0.
6. **Tests:** POS has 34 test references; others 2 to 4. No test covers the new stock-posting integrations, A1 (status codes), or B1 to B10.
7. **Migrations:** one migration (`ModulesExpansion`) holds all nine modules' tables; no seed/demo data for them.
8. **Housekeeping:** `Inventory` still holds a stub entity; `erp-dev.db*` SQLite files sit in `ERP.Api` (git-ignored but against the SQL-Server-only rule).

---

## E. Suggested fix order for the implementing agent

1. A1: map `KeyNotFoundException`/`InvalidOperationException` or (better) replace with `NotFoundException`/`ConflictException`/`DomainException` in the six modules.
2. C2/C3 blockers: add customer/vendor and item pickers to Sales, Quote and PO forms and send `customerPartyId`/`vendorPartyId`/`itemId`.
3. A5: remove every placeholder GUID; add real pickers (client, engagement, BOM items, driver, cashier from the JWT on the server).
4. B1/B2: introduce a GR/IR clearing account and correct the manufacturing journals; add status guards to work-order Release/Complete (B3) and to all `Change*Status` handlers (A6).
5. B4/B5: POS: server-side pricing/tax, payments sum validation, cashier from the token, revenue/receipt posting, void must reverse cash and use original cost.
6. B9/B10: Projects billing must fail (not flag) without a customer; Services UI must call `/milestones/{id}/invoice`.
7. A4/A3/A2: workspace currency and tax codes; i18n sweep; permission directives on all module pages.
8. C-lists: add edit/delete UI + missing backend routes; a Services time-entry page; a retainer billing run; stock receive/issue/adjust/transfer screens.
9. A7/A8/D3: make postings transactional with their document, add idempotency, add ETag.
10. Tests for each of the above, plus the vocabulary/architecture tests already in the repo.
