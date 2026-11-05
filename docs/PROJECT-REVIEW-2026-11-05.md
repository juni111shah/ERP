# Project review, 5 November 2026

What is missing, incomplete, wrong or worth improving, across the whole ERP (backend, web, mobile, operations).
Everything marked **Verified** was checked in the repository or the running dev database on this date. Everything marked
**Judgement** is my assessment of what a business like the target users would expect; it is not a defect in the code.

---

## 1. Where the project stands

| Area | Size today |
|---|---|
| Backend modules | 12 (Real Estate, HR, Inventory, Sales, Purchasing, POS, Retail, Distribution, Manufacturing, CRM, Projects, Services) plus the core (finance, parties, security, workflow, portals) |
| Country packs | 7 (AE, GB, IN, PK, QA, SA, US) |
| Backend tests | 458 unit, 491 integration (8 skipped: SQL Server race tests) |
| Web screens | about 168 routes; modules: HR 23, Real Estate 16, Inventory 5, finance/admin 29 and one folder per other module (4 to 5 each) |
| Web tests | 10 spec files in total (2 are HR) |
| Mobile | 18 feature folders, 1,045 tests, `flutter analyze` clean |
| Backlog | 82 cards done, 9 open (TASK-131 to 139, all Purchasing / bills / demo data) |
| Demo database | every table now has rows (see `docs/demo-data/`) |

Verdict: the core (finance, real estate, HR) is deep and well tested. The other nine modules are **breadth-first**: each
has the main documents and screens but few of the next-level features, almost no web tests, and several have no mobile
screens. The biggest risks are process and operations (section 2), not missing buttons.

---

## 2. Cross-cutting problems (affect every module)

### 2.1 Release and process (Verified)
1. **Nothing is on `main`.** The work lives on `antigravity/dashboard-and-modules` and `claude/hr-wave1`. `STATE.md` says the
   first branch "was never merged" (main rejects direct pushes). Until a pull request is merged, the product on `main` is old.
   The HR PR for `claude/hr-wave1` is not opened yet (no GitHub CLI here).
2. **Database changes are applied two ways.** EF migrations (auto-applied in dev with `Database__AutoMigrate`) and hand-kept SQL
   scripts in `database/scripts/` (0026, 0027, 0065 and others "not applied to any database", per `STATE.md`). Two sources of
   truth will drift. Pick one: generate the scripts from the migrations in CI and fail the build when they differ.
3. **Script 0027 can fail on real data** (it fails if a register has two open sessions) and 0065 locks tables while building
   indexes. Needs a pre-check script and a maintenance window note.
4. **Eight SQL Server race tests are skipped locally**, so the retry-on-conflict logic (TASK-021/023) is unproven on the real engine.
5. **Commit messages missing the `TASK-nnn:` prefix** on a few cards (006, 009) break the card-to-commit trail.
6. **Disk space:** `STATE.md` records C: at 94% full. Build output (`bin`/`obj`) of 12 modules plus Flutter and Node is large.

### 2.2 Testing (Verified)
- **Web has almost no tests.** 10 spec files for about 168 routes. Zero specs for CRM, Sales, Purchasing, POS, Retail,
  Distribution, Manufacturing, Projects, Services, Real Estate, Inventory pages. A broken template or a renamed API field is only
  found by hand. Add at least one render-and-click spec per list/detail page, or a Playwright smoke run in CI (the `tools/qa-smoke.mjs`
  exists but is manual).
- **No end-to-end test in CI** that logs in, creates a document and checks the ledger.
- **Mobile tests use a fake HTTP adapter**, so they prove the screens, not the real API contract. Add contract tests (generate the
  mobile models or a schema check from the OpenAPI document).
- Property/concurrency tests exist only for a few finance paths.

### 2.3 Security and compliance (Verified unless noted)
- Good: lockout, refresh rotation, 2FA (TOTP with recovery codes), rate limits, per-endpoint authorization guard test, portal token isolation.
- Missing: **SSO / OpenID Connect / SAML** (none found), **API keys or service accounts** for integrations (device punches today need a
  normal user account), **outbound webhooks**, **password policy settings** per workspace, **session list / remote sign-out** screen,
  **IP allow-list**, **data export and erasure** (GDPR-style subject request; no code found), **field-level encryption** for IBAN,
  labour card and passport numbers (stored as plain columns), **audit of read access** to salary data.
- Judgement: the dev seed puts an admin password in `appsettings.Development.json`. Fine for dev, but make sure the Staging and
  Production files cannot seed (they set `Seed:Enabled` to false; keep a test for it).

### 2.4 Operations (Verified)
- Present: Serilog, one health check, Dockerfile, compose file, Cloudflare tunnel notes, two CI workflows.
- Missing: **metrics and tracing** (no OpenTelemetry or Prometheus), **backup and restore procedure** (no code or script found;
  only a note might exist in `docs/devops`), **database migration job** for production instead of start-up auto-migrate,
  **blue/green or zero-downtime** note, **alerting** on failed scheduled jobs (the job runner exists but failures only log),
  **log retention** policy, **load test** results, **disaster-recovery objectives**.
- The API start-up takes about 30 seconds in dev because it migrates and seeds every time; production needs a ready/live probe split.

### 2.5 Integrations (Verified: none found for these)
Bank feeds / open banking, **payment gateway** (online invoice payment), **SMS / WhatsApp** channel (the message-channel interface
exists; only in-app and email channels are implemented), **e-invoicing** (ZATCA, UAE e-invoicing, India GST e-invoice; the VAT
return exists for AE and QA only), **accounting export** (CSV/XLSX report export exists but no designer), **calendar sync**, **e-signature** for leases and contracts.

### 2.6 User experience (Judgement, with verified facts)
- **Arabic works by translating the page text at run time**, keyed by the exact English string (`phrases.ar.ts`). Any new or
  reworded English string silently stays English; there is no build check. Replace with proper i18n keys, or add a CI check that
  fails when a template string has no Arabic entry.
- **Accessibility:** the a11y lint caught two unlabeled inputs today; add it to CI (it is a script, confirm it runs there).
- **Mobile parity:** see section 4. Web and mobile screens are separate code, so every feature is built twice.
- **Onboarding:** an industry template picks modules, but there is no guided "set up your company" checklist (chart of accounts
  review, tax codes, first user, opening balances).
- **Opening balances / data migration:** a CSV import exists for parties, items, properties and units only. No import for opening
  trial balance, open invoices and bills, stock on hand, employees, leave balances.

### 2.7 Performance and scale (Judgement)
- Several lists return arrays, not pages (shifts, roster, open tasks, history). Fine at demo size; add paging before real data.
- `Settings`, permissions and menus are read on many requests; check caching under load.
- Reporting runs on the live tables; add a read replica or summary tables before many years of ledger lines (reporting indexes
  exist in script 0065 but are not applied anywhere).

---

## 3. Finance core (the engine under every module)

Built: journals, posting service, period lock, invoices and credit notes, receipts and allocation, bills and payments, cheques (PDC),
bank reconciliation by paste import, budgets, trial balance, P&L, balance sheet, ageing, tax summary, VAT return (AE, QA), approvals.

| Gap | Type | Detail |
|---|---|---|
| Foreign-currency revaluation | Missing | Documents carry an exchange rate, but no month-end revaluation of open foreign balances and no unrealised gain/loss entry |
| Fixed-asset register and depreciation | Missing | No asset entity or depreciation run (real estate has "acquisitions", which is different) |
| Recurring invoices / subscriptions | Missing | Only the rent billing run is recurring; no general repeating invoice or bill |
| Purchase returns / debit notes | Missing | Credit notes exist for sales; none found for suppliers |
| Cost centers / analytic dimensions | Missing | Business unit is the only dimension; no project, department or free dimensions on journal lines |
| Inter-company | Missing | Several legal entities exist, but no inter-company invoices, eliminations or consolidated statements |
| Bank feeds | Missing | Reconciliation is paste-import only |
| Year-end close | Partial | Period lock exists; no guided closing entry that moves profit to retained earnings |
| Multi-book / IFRS vs local | Missing | Single ledger |
| Withholding tax, TDS, reverse-charge | Missing | Only a simple tax code model |
| Cash-flow statement | Missing | P&L and balance sheet exist; no cash-flow report |
| Payment runs | Missing | Bills are paid one by one; no batch payment proposal and bank file for suppliers |
| Dunning | Partial | Reminders exist; no escalating dunning letters and late fees |
| Document approvals | Partial | Approval rules gate bills; check that they also gate payments, journals and payroll (payroll does) |
| Open correctness cards | Incorrect | TASK-131 to 138 (Verified): goods receipts accepted for any PO status, PO editable after approval, PO-to-bill loses tax in some paths, dates and precision not validated, bill/payment edit not retried on conflict |

---

## 4. Module by module

### 4.1 HR and payroll (most complete; my own work this session)
Built: employees (rich profile), documents with expiry, departments, positions, org chart, leave (types, tiers, pay bands, half days,
balances, approvals, year-split), attendance (monthly and daily with late/overtime, shifts, dated roster, device punch API), payroll
(regular, off-cycle, final settlement, void, partial pay), loans and advances, statutory rules (flat and slab, per entity), end of
service (AE, QA), WPS SIF (UAE), history, onboarding and offboarding checklists, recruitment pipeline, reviews, self-service, reports.

Incomplete or incorrect (Verified unless noted):
1. **Statutory rules match nationality by free text.** `Employee.Nationality` is typed text ("Qatar" in the demo, rule says "Qatari"), so
   a rule for Qatari nationals silently does not apply. Store a country code and let the rule list countries.
2. **Employee has both `Department` (text) and `DepartmentId`.** Two sources of truth; reports and payroll cost split use the id,
   screens still show the text. Migrate the text to the id and drop it.
3. **Weekend default is Saturday/Sunday**, wrong for Gulf entities (Friday/Saturday) unless the setting is changed. Country packs should set it.
4. **End-of-service rules exist only for AE and QA**; WPS file only for UAE. SA (GOSI, Mudad), IN (PF, ESI), PK (EOBI), GB (PAYE, NI), US
   (FICA, withholding) have none. Statutory rules can express flat and slab rates, but there are no pre-filled country sets.
5. **No year-end leave carry-forward job** (the cap field exists, nothing applies it automatically) and **no leave accrual job** for monthly accrual types.
6. **Half-day and roster details:** an employee has one default shift plus dated overrides; no rotating patterns (week on/off), no shift swap,
   no overtime approval flow, no public-holiday pay rules, no night differential.
7. **Device integration** is an API only (`POST /hr/attendance/punch`); no vendor connectors, no photo or face capture, no geofenced mobile
   clock-in, no API-key auth for devices.
8. **The Nov 2026 demo draft would not post** (journal off by 2,000, Dr 183,500 vs Cr 181,500) until recalculated. The cause was not found;
   treat it as an open question: add a guard test that posts a payroll built from the seed and recalculated variants, and make the post
   handler recompute totals itself so a stale draft cannot reach the ledger unbalanced.
9. Missing HR features: **expense claims / reimbursements**, **training and certifications**, **assets issued to staff** with return at exit,
   **probation confirmation workflow**, **contract renewal workflow** (reminders exist), **disciplinary records**, **grievances**,
   **succession / skills matrix**, **announcements**, **employee survey**, **payroll journals by cost centre**, **bonus / commission / overtime
   rules engine**, **salary revision letters**, **multi-currency salaries**, **tax certificates and year-end statements**, **bank file formats beyond WPS**.
10. Payslip is available; **PDF generation quality and branding** not reviewed, and there is no password-protected email of payslips.

### 4.2 Real Estate (deepest module after HR)
Built: properties, units, leases, rent schedule, renewals, deposits, brokerage and commissions, work orders, preventive maintenance,
inspections, owner statements, acquisitions and funding, tenant and vendor portals, budgets, reports (16).
Gaps (Judgement): **Ejari / registry integration** per country, **utility metering and billing** (water, electricity, service charge
allocation), **late fees and rent escalation clauses** automation, **tenant screening**, **vacancy marketing** syndication, **lease document
generation and e-signature**, **property valuation and impairment**, **owners' portal**, **unit amenities and floor plans/images**, **bulk cheque scan**,
**parking and asset registers**, **multi-property consolidated owner statements**.
Web has 16 pages and no specs; mobile covers 8 files.

### 4.3 Sales
Built: quotes, orders, order-to-invoice (tax and currency kept), dashboard and KPIs, search.
Missing: **delivery notes / shipments** and partial deliveries, **sales returns and RMA**, **price lists per customer** (retail has them, sales does not seem to
use them), **discount approval**, **sales commission**, **sales targets**, **contracts and recurring orders**, **customer credit limit enforcement at order time**
(credit limit field presence not verified), **quote PDF with terms and e-sign**, **drop-ship**, **back-orders**, **customer statements by email on a schedule**.
Web: 5 pages, 0 specs. Mobile: 6 files.

### 4.4 Purchasing
Built: requisitions, purchase orders, goods receipts with GR/IR clearing, PO-to-bill, analytics.
Incorrect/incomplete (Verified): TASK-131 to 138 are open (receipts accepted for non-open POs, PO edit rules, tax lost on PO-to-bill, dates and precision, atomicity).
Missing: **supplier quotations / RFQ comparison**, **three-way match tolerance rules**, **purchase returns and debit notes**, **landed cost**
(freight, duty allocated to stock cost), **blanket orders / contracts**, **supplier scorecards**, **approved supplier lists**, **PO change history**,
**vendor onboarding with documents**, **supplier prepayments**.
Web 5 pages; mobile 5 files.

### 4.5 Inventory
Built: warehouses, receipts, issues at moving-average cost, adjustments, transfers, counts, reorder alerts, valuation, movements.
Missing (no code found): **lot / batch / serial tracking and expiry**, **bin / location inside a warehouse**, **FIFO or standard costing option**
(only moving average), **barcode scanning on web/mobile** (only a seed mention), **unit-of-measure conversions**, **item variants / matrix**, **kits / bundles**,
**inter-entity transfers**, **stock reservation / allocation against orders**, **cycle-count scheduling**, **consignment**, **product images and attachments**,
**ABC analysis and slow-mover report**, **in-transit stock**.
Web 5 pages (has the most detailed count screens); mobile in `ops` with 1 file.

### 4.6 POS and Retail
Built: registers, sessions with cash drawer, orders with server pricing, void with reversal, revenue posting, KPIs; retail stores, price lists, promotions.
Missing: **offline mode** (the POS needs the network; no offline queue found), **split and mixed payments review**, **receipt printing** (ESC/POS), **barcode and
weighing scale**, **customer display**, **gift cards and loyalty** (one model mentioned, no flows), **returns and exchanges at the till**, **table / kitchen
flows** (restaurants), **shift X/Z reports**, **cash-up variance workflow**, **hold / recall orders**, **tips**, **price override approvals**, **store-to-store transfer requests**.
Retail has no mobile screens; POS mobile is one file.

### 4.7 Distribution (van sales, routes, wholesale pricing)
Built: routes, van trips with load and reconciliation, wholesale pricing.
Missing: **route planning and customer visit scheduling**, **GPS tracking and proof of delivery**, **van stock replenishment**, **trip cash settlement** with the driver,
**delivery orders from sales orders**, **vehicle and driver registers**, **offline van app** (the mobile app has no Distribution screens at all, which is the natural place for it).

### 4.8 Manufacturing
Built: BOMs, work orders (release, complete, correct inventory journals), costing, analytics.
Missing (no code found): **routing and work centres with capacity**, **MRP / planning**, **production scheduling**, **quality control checks**, **scrap and yield**,
**by-products and co-products**, **subcontracting**, **shop-floor terminal**, **machine maintenance**, **lot traceability** (depends on Inventory), **standard vs actual cost variance posting**.
Mobile: one file in `ops`.

### 4.9 CRM
Built: leads, opportunities pipeline, interactions, conversion to deal, follow-up reminders, analytics.
Missing: **email and calendar integration**, **campaigns and lead sources ROI**, **web-to-lead form**, **lead scoring and assignment rules**, **quotes from opportunities**
(link to Sales), **activities with tasks and meetings**, **customer 360 view** combining invoices, tickets, leases, **forecasting**, **duplicate detection**, **mass email**.
Mobile has 3 files; no push notification flows verified.

### 4.10 Projects and Services
Built: projects, tasks, milestones, timesheets, billing by type; services engagements, retainers, milestone billing, time entries.
Missing: **Gantt / dependencies and critical path**, **resource planning and utilisation**, **budget vs actual by cost category**, **expense claims billed to the
client**, **approval of timesheets** (timesheets exist; approval flow not verified), **change orders**, **project profitability report**, **recurring retainer invoicing automation**
(retainers exist; check scheduling), **contract / SLA tracking**, **ticketing / helpdesk** (Services has none).
Projects and Services have 4 and 5 web pages and 0 specs. Mobile has both in `projects` (2 files).

### 4.11 Core admin, platform and portals
Built: workspaces, legal entities, business units, users, roles, permissions, audit log, custom fields, approvals and rules, import wizard, search, notifications,
reminders, scheduled jobs, tenant and vendor portals, print templates, document vault, number sequences.
Missing: **workflow designer** (rules cover amount thresholds only), **custom reports / query builder / dashboards per user**, **scheduled report email**,
**document templates editor** (print templates exist, check editing UI), **notification preferences per user and channel**, **delegation of approvals while away**
(delegation appears in code; confirm UI), **tenant (workspace) billing and plan limits** if this will be sold as SaaS, **white-label branding**, **multi-language beyond English/Arabic**,
**time zone per user**, **audit export**, **change history on master data screens** (audit diff exists in the admin area).
Customer portal: payments online, document upload, ticket history are missing; vendor portal lacks invoice submission.

---

## 5. Mobile app (Verified from folders)
- Has screens for: auth, dashboard, home, HR (12 files), Real Estate (8), Payables (9), Receivables (5), Sales (6), Purchasing (5), CRM (3), Projects and Services (3), Inventory/Manufacturing/POS (`ops`, 4 files), settings, notifications, onboarding, intro.
- **No screens for Retail or Distribution**, and no folder for Services separate from Projects.
- Finance on mobile is bills, payments, invoices, receipts, journals, reports and approvals; bank reconciliation, cheques, budgets and ledger-wide reports were not found in the folder names.
- No offline mode, no push notifications flow verified, no biometric unlock verified, no camera features (document scan, receipt capture, barcode).
- Real-device testing has never been done (only unit/widget tests and web build).
- Each web feature is built twice; consider generating forms from a shared field spec as already done for the record drawer on web.

---

## 6. Things that are probably wrong or fragile (summary list)
1. `main` is behind; two branches hold the work (2.1).
2. Statutory nationality matching by free text (4.1).
3. Employee department stored twice (4.1).
4. A stale payroll draft posted an unbalanced journal (4.1 item 8). Cause unknown.
5. Dual DB change tracking (migrations and SQL scripts) can drift (2.1).
6. Arabic translation relies on exact English strings (2.6).
7. Open Purchasing correctness cards TASK-131 to 138 (3).
8. Demo data uses relative dates ("30 days ago"); screenshots and tests that rely on them will change tomorrow. `DemoTopUp` also writes `Guid.Empty` as the user for cash drawer lines.
9. Backup and restore are undocumented in code (2.4).
10. Web test coverage is close to zero outside HR (2.2).

---

## 7. Suggested order of work

**Now (a week or two)**
1. Open and merge the pull requests; apply or retire the SQL scripts; decide migrations vs scripts as the single source.
2. Fix TASK-131 to 139 (Purchasing second pass).
3. Make payroll posting recompute its own totals; add the regression test.
4. Fix nationality and department duplicates in HR.
5. Add web smoke tests (Playwright) for the top 20 screens; run `lint:a11y` and the Arabic completeness check in CI.

**Next (a month)**
6. Backups, restore drill, production migration job, metrics and alerts.
7. API keys / service accounts and outbound webhooks (needed for devices, payment gateways, integrations).
8. Finance: FX revaluation, recurring invoices, purchase returns, year-end close, cash-flow statement, payment runs.
9. Inventory: lots/serials, bins, FIFO option, UoM conversions.
10. Sales: delivery notes, returns, credit-limit enforcement.

**Later (quarter)**
11. Country payroll packs (SA, IN, PK, GB, US): statutory rule sets, bank files, end of service.
12. Manufacturing routing/MRP/quality, POS offline and receipt printing, Distribution mobile app.
13. E-invoicing per country, bank feeds, payment gateway, SSO.
14. Fixed assets, inter-company and consolidation, cost centres.
15. Customer-facing: portal payments, e-signature, helpdesk.

---

## 8. How this was checked
Counted modules, features, web pages, specs and mobile files from the repository; searched the code for the named capabilities (a
"none found" above means no matching class or file, which can miss a feature built under another name); read `STATE.md`, `GAP-ANALYSIS.md`,
`ROADMAP.md`; ran the HR flows against the dev API during this session. Nothing outside HR was exercised in a browser for this review.
