# Multi-industry ERP: audit and implementation report

Audit date: 2026-11-06. Base: `main` at 2c631b6. Branch: `claude/multi-industry-erp`.

## Summary

The product started as a modular core with 13 modules. Finance, real estate and HR were deep; the rest were broad but thin, and six of the twelve industries you asked about had no functionality at all. Industry was a label used once at sign-up.

**What is true now**

- One codebase serves all twelve industries, with no per-industry fork. Choosing one of 63 business types decides modules, roles, units, categories, optional core areas, menu wording and dashboard order.
- Twelve labelled DEMO organizations are in `ERP_Dev` with up to six months of connected history. Each passes every check: balanced ledger, subledgers tied to control accounts, stock tied to the ledger, no cross-tenant links. A re-run adds nothing.
- The workflow defects found by the audit in Procure to Pay, Order to Cash, Plan to Produce, projects, payroll and POS are fixed, except those listed in section 7.
- Four high security findings are fixed. **One critical finding is not**: see section 11.

**What is not true**

- **No industry is "complete".** Section 3 says what "fully supported" means here, and only three qualify. Six industries run on a shared record engine, which is thinner than purpose-built modules.
- **Healthcare is administrative only, and not ready for real patient data.** It has no clinical records and no encryption at rest.
- Single-currency documents, no consolidation, no batch or expiry tracking. These block real pharmaceutical, food and multi-national groups. See section 15.

**Decisions waiting for you** are in section 17.

---

## 1. Complete audit

The audit traced handlers rather than reading module names. The full findings, with file and line evidence at the base commit, are in the appendices.

| Appendix | Covers |
|---|---|
| A | Universal core: finance, approvals, settings, configuration |
| B | Procure to Pay, Order to Cash, inventory, distribution |
| C | Plan to Produce, retail and POS, projects, services, CRM |
| D | Hire to Retire, real estate |
| E | Tenant isolation, authentication, authorization |
| F | Frontend, navigation, dashboards, settings screens |
| G | AI provider architecture |

Line numbers in the appendices refer to 2c631b6, and many of those defects are now fixed. Section 7 gives the current state.

### Classification at the base commit

| Feature | Class |
|---|---|
| Tenant filter, ledger kernel, posting gateway, numbering, audit trail, custom fields | Generic and reusable |
| Parties, items, invoices, bills, receipts, payments, cheques, bank reconciliation | Generic and reusable |
| Users, roles, permissions, module enablement, dashboard contributors | Generic and reusable |
| Budgets, tax returns, attachments, CRM pipeline, project shell and timesheets | Partly reusable |
| Sales orders, purchase orders, stock ledger, transfers, counts, POS till, BOM, work orders | Partly reusable, with defects |
| Real estate (leasing, brokerage, maintenance), HR and payroll, van routes | Industry-specific and working |
| Requisitions, goods receipt, PO billing, cost variance, sales cancel, WIP accounting, approvals beyond bills | Incomplete or wrong |
| Year-end close, cash-flow statement, opening balances, fixed assets, expense claims, contracts, sales and purchase returns, credit limits | Missing |
| Reservations, batch, serial, expiry, unit conversion, variants, loyalty, MRP, quality, routing | Missing |
| Healthcare, education, logistics, hospitality, agriculture, construction | Missing |
| Industry engine, business types, optional core areas, numbering, units and categories screens | Missing |

### A defect found while taking the baseline

`main` had 65 failing integration tests, from one cause. `LangVersion=latest` under a .NET 10 SDK selects C# 14, which binds `array.Contains` in expression trees to a span overload that EF Core 8 cannot evaluate, so real endpoints returned 500. CI did not show it because it builds with the .NET 8 SDK in `global.json`. The version is now pinned to 12.0, which both SDKs accept.

---

## 2. Industry compatibility matrix

Key: **Y** built and exercised by demo data. **P** partial, see section 4. **N** missing. **-** not normally needed.

| Capability | Trade | Retail | Mfg | Real est. | Constr. | Health | Logist. | Services | Educ. | Hosp. | Agri. | Corp. |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Ledger, AR, AP, cash, tax | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y |
| Year-end close, cash flow | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y |
| Fixed assets | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y |
| HR and payroll | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y |
| Approvals, audit, documents | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y |
| Owner dashboard | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y |
| Purchasing to payment | Y | Y | Y | - | Y | Y | - | - | - | Y | Y | P |
| Order to cash | Y | Y | Y | - | - | - | - | - | - | - | Y | - |
| Stock, warehouses | Y | Y | Y | - | Y | Y | - | - | - | Y | Y | - |
| Batch, expiry, serial | N | N | N | - | - | N | - | - | - | N | N | - |
| Unit conversion | N | N | N | - | N | N | - | - | - | N | N | - |
| Price lists, promotions | P | Y | - | - | - | - | - | - | - | P | - | - |
| Credit limits | Y | - | Y | - | - | - | - | Y | - | - | Y | - |
| Multi-currency documents | N | N | N | N | N | N | N | N | N | N | N | N |
| Consolidation | - | - | - | - | - | - | - | - | - | - | - | N |
| Industry records | P | P | P | Y | Y | P | Y | Y | Y | P | Y | - |

| Industry | Specific capability | State |
|---|---|---|
| Trading | Routes, van trips with invoices, wholesale tiers, letters of credit, import consignments, export orders | Y |
| Trading | Landed cost into stock value, FEFO | N |
| Retail | POS, barcode, shifts, discounts, promo codes, price lists, returns, transfers | Y |
| Retail | Variants, loyalty, gift cards, exchanges as one document | N |
| Manufacturing | BOM (multi-level through requirements), work orders, material issue and return, scrap, reversal, costing, requirements, plans, inspections, non-conformance | Y |
| Manufacturing | Routing and work centre master, capacity, by-products | N |
| Real estate | Portfolio, leases, rent billing, deposits, maintenance, commissions, owner payouts, sale contracts with instalments, allotments, valuations | Y |
| Real estate | Revenue recognition on off-plan sales, handover journal | N |
| Construction | Sites, BOQ with progress, valuations, subcontracts, certificates, material issue, equipment, site reports | Y |
| Construction | Retention held in the ledger, percent-of-completion revenue | N |
| Healthcare | Patients, appointments, medical billing, dispensing, diagnostic orders, claims, read trace | Y |
| Healthcare | Clinical records, consent, encryption at rest, e-claims | N |
| Logistics | Shipments, vehicles, drivers, fuel, maintenance, delivery costs, freight invoices | Y |
| Logistics | Route optimisation, GPS, driver settlement | N |
| Services | Projects, timesheets, retainers, milestones, time billing, CRM, profitability | Y |
| Education | Sessions, classes, admissions, students, fee instalments, exams, results | Y |
| Education | Timetable, attendance, transcripts, university credits | N |
| Hospitality | Rooms and tables, reservations, folios, events, waste, recipes and till | Y |
| Hospitality | Channel booking, housekeeping, kitchen display | N |
| Agriculture | Herds, production, feed, vet care, crops, harvests | Y |
| Agriculture | Biological asset valuation, individual animal records | N |
| Corporate | Several legal entities and access by entity | Y |
| Corporate | Intercompany, consolidation, investment portfolios | N |

---

## 3. Industries fully supported

"Fully supported" means a business could run its core operating cycle end to end today, with the gaps in section 15 being limits and not blockers.

- **Real estate:** agencies, property management, commercial leasing. Developers and societies are in section 4.
- **Professional services:** software, IT, consulting, marketing, legal and accounting practices.
- **General wholesale and trading**, for goods that need no batch or expiry control.

---

## 4. Industries partly supported

| Industry | Works | Stops short at |
|---|---|---|
| Retail and commerce | Tills, barcode, promotions, returns, branches | Variants for clothing, loyalty, e-commerce connector |
| Manufacturing | Discrete and batch production with costing and quality | Work centre capacity, by-products; batch traceability for food and pharma |
| Real estate developers and societies | Sale contracts, instalments, allotments | Revenue recognition, handover accounting |
| Construction | Measured work, valuations, subcontractors, materials | Retention ledger, percent-of-completion revenue, cost codes |
| Healthcare administration | Registration, billing, pharmacy stock, claims | Clinical care; real patient data is blocked by missing controls |
| Logistics | Shipments and fleet costs through to invoices | Tracking, route planning, driver settlement |
| Education | Schools and training centres end to end | Timetables, attendance, university structures |
| Hospitality | Restaurants through tills and recipes; hotel folios | Channel booking, housekeeping |
| Agriculture | Dairy and crop operations | Animal-level records, biological asset accounting |
| Import and export | Trade documents and costs | Foreign-currency documents, landed cost into stock |
| Pharma and food distribution | Stock, routes, quality checks | Batch and expiry control, a legal requirement in many places |
| Financial and corporate | Several legal entities | Consolidation and intercompany |

---

## 5. Not supported

- Clinical systems: medical records, orders, results and e-prescribing.
- Regulated financial products: banking, lending, insurance policy administration.
- University systems: credits, degree audit, transcripts.
- Group accounting: statutory consolidation, intercompany eliminations.
- Foreign-currency trading books: documents in a currency other than the entity's base.

---

## 6. Missing modules and capabilities

Ranked by how many industries each one unblocks.

1. **Batch, lot, serial and expiry tracking.** Pharma, food, electronics, healthcare, agriculture.
2. **Foreign-currency documents and revaluation.** Import and export, groups, anyone with foreign suppliers.
3. **Unit conversion.** Buying in cartons and selling in pieces.
4. **Stock reservations.** Order-to-cash promises.
5. **Consolidation and intercompany.** Holding companies.
6. **Landed cost into stock value.** Import businesses: costs are captured but expensed.
7. **Variants, loyalty, gift cards.** Retail.
8. **Work centres, routing, capacity.** Manufacturing.
9. **Retention and percent-of-completion revenue.** Construction, developers.
10. **Document template designer and report designer.** All.
11. **Integrations:** payment gateway, e-invoicing, messaging, single sign-on, webhooks.

---

## 7. Incomplete or incorrect logic

### Fixed

| Area | Defect fixed |
|---|---|
| Purchasing | A PO could only be billed once, expensing unreceived goods and refusing later receipts. Now part-billed, with three-way control |
| Purchasing | A supplier price below the PO price always failed; status could be forced to received; warehouse fallback crossed legal entities |
| Sales | Overselling corrupted valuation; cancelling or deleting a delivered order stranded cost; over-delivery was silently clamped |
| Finance | An invoice under a live credit note could be voided, leaving phantom receivables; a cheque receipt could be voided with the cheque left in hand |
| Finance | Voiding in a locked period changed tax reports for that period; reports summed mixed currencies |
| Payables | Payables ageing ignored purchase returns and disagreed with the ledger (found by the demo data checks) |
| Manufacturing | The journal credited WIP with no matching debit; materials could go negative; scrap was not costed; completed orders could not be undone |
| POS | Card sales could be refunded in cash; damaged returns left cost of sales wrong; promotions and price lists never reached the till |
| Projects | Fixed-fee and non-billable cost stayed in WIP forever; cost journals were dated by the click, not the work |
| Services | Approver identity came from the request; retainers never billed; approved time was never invoiced |
| HR | Payroll credits came from a stale total, which caused an unbalanced journal; loans were recovered without being paid out; gratuity ignored the days setting |
| Real estate | Managed-property rent was booked as company income; owner statements were overstated by tax; rent roll double-counted balances |
| Modules | Enabling a module did not bring the accounts its postings need (found by the demo data checks) |

### Open

| Severity | Defect |
|---|---|
| Medium | Non-recoverable tax on a clearing line leaves a residual; rounding differs between receipts and bills by cents, with no write-off |
| Medium | A receipt reversal allowed by total on-hand can leave the ledger and stock different (`StockPostingService`) |
| Medium | A manual bill can double-charge a PO; manual journals can post to control accounts |
| Medium | `CreateReceipt` has no transaction around allocation and posting |
| Medium | Backdated movements change moving-average history without revaluing posted cost |
| Low | Quote and order conversions do not retry on concurrency; tax code is chosen by rate only |
| Low | The record engine marks a record "billed" while its bill may still await approval |
| Low | Angular logs `NG0101` during tests. It fails nothing and was left alone |

---

## 8. Core architecture improvements

- **Pinned compiler version**, so a newer SDK cannot change query behaviour.
- **`AppClock`:** business handlers read one pinnable clock instead of `DateTime.UtcNow`.
- **Year-end close, cash-flow statement, opening balances, multi-level approvals** in core finance. Approvals now also gate journals, payments, requisitions and POs when a policy matches.
- **`IOwnerDashboardSection`, `IBillAdjustmentSource`, `IAuditFieldPolicy`, `IPriceResolver`:** small interfaces so modules contribute without modules or core depending on each other. Architecture tests still pass.
- **Branch in the ledger:** invoices and bills carry their business unit into journal lines, so branches can be compared.
- **`ProgramSeeder`** now touches only its named demo workspaces. It previously wrote demo history into every workspace, which caused the 2026-11-06 incident.

---

## 9. Industry module implementation

New modules, each switchable per workspace with its own permissions, menu, roles and numbering:

| Module | Record types |
|---|---|
| `healthcare` | Patients, appointments, medical bills, dispensing, diagnostic orders, claims |
| `education` | Sessions, classes, admissions, students, fee plans, exams, results |
| `logistics` | Shipments, vehicles, drivers, fuel, maintenance, delivery expenses |
| `hospitality` | Rooms and tables, reservations, events, waste |
| `construction` | Sites, BOQ, valuations, subcontracts, payment certificates, material issues, equipment, site reports |
| `agriculture` | Herds, production, feed, vet care, crops, harvests |
| `trade` | Letters of credit, import consignments, export orders |
| `propertysales` | Sale contracts, allotments, valuations |
| `quality` | Production plans, inspections, non-conformance |
| `contracts`, `expenses` | Contracts, expense claims |
| `assets` | Categories, register, depreciation, disposal (typed tables, not the record engine) |

**The record engine is the main design decision.** Six hand-written modules would have repeated the same lists, forms and rules, which you asked to avoid. A record type declares its fields, statuses, transitions, approvals, billing and stock behaviour, and one set of handlers and screens serves them all.

It is thinner on purpose. Fields live in JSON, so reporting on them is weaker and there are no foreign keys inside them. A record type can move to typed tables later without changing its routes. Real invoices, bills, stock issues and journals are always created through the core documents, never by the engine.

---

## 10. Configuration implementation

| Requirement | Implementation |
|---|---|
| Industry selection at onboarding | 63 business types in 12 groups, grouped at sign-up |
| Module activation | A business type enables its modules with their dependencies, accounts, roles and numbering |
| Industry navigation | The menu follows enabled modules and permissions, and uses the business's own words, such as "Fee payers" or "Tenants" |
| Change later | `POST /setup/industry` only adds. Nothing is switched off or removed, because it may hold data |
| Optional core areas | 13 capabilities can be switched off. This hides menu items, and the API returns 403 for budgets, cheques, banking and import |
| Workflows and approvals | Multi-level policies by document type and amount; record types declare their own transitions |
| Custom fields | Existing engine. Record types are registered, but **tenant custom fields are not yet rendered on record forms** |
| Numbering | Prefix, pattern and yearly reset per document type |
| Units, categories | Lists per workspace, offered as suggestions on item forms. Not enforced, and no conversion |
| Dashboards | Owner dashboard order follows the business type, and each module adds its own measures |
| Document templates | **Not built.** Print layouts are fixed |
| Tax, languages, currencies | Country packs and existing screens; Arabic added for new fixed strings. Currency stays single per legal entity |

**Limit:** the ledger cannot be switched off, because other documents post to it. Hiding receivables or payables removes them from the menu, but their APIs stay open since modules use them.

---

## 11. Multi-tenant security assessment

**Tenant read isolation is sound.** The filter is central, automatic for module entities and fails closed. No raw SQL exists in production code, and no cross-tenant read of business rows was found. The demo data checks found no links between organizations.

| ID | Severity | Finding | State |
|---|---|---|---|
| **C1** | **Critical** | **Cross-tenant account takeover: an admin in any workspace, even a trial, can create a global account with a known password for someone else's email. If that person is later invited elsewhere, the attacker can sign in there** | **Open. Only the name leak in the invite response is fixed** |
| H1 | High | Two-factor secrets copied into the audit log | Fixed for new rows, and old rows are hidden on read. **Existing rows remain**, see section 17 |
| H2 | High | A delegated admin could grant themselves full access | Fixed |
| H3 | High | Audit log showed salaries to people without salary access | Fixed |
| H4 | High | Company boundary not enforced on writes or approvals | Fixed centrally |
| M1 | Medium | References to another tenant's records stored unchecked | Fixed for the sampled commands. Not every command was audited |
| M2, M5 | Medium | Reset links logged outside development; document vault listed all owners | Fixed |
| M3, M4, M6-M11 | Medium | See appendix E | Open |
| L1-L8 | Low | See appendix E | Open |

**C1 needs its own change.** The correct fix is invitation by emailed link, with acceptance before an existing account joins a workspace. That changes sign-up and depends on mail being on in production, so it should not be rushed inside this branch. **Do not open self-service sign-up to the public until it is fixed.**

**Patient data:** appendix E section 6 lists the controls missing before real clinical data could be stored. The engine adds restricted fields and a read trace for patient records, which is a start and no more than that.

---

## 12. Demo organization and seed summary

Seeded in `ERP_Dev` by `tools/seed-demo-orgs`, through the real API with real sign-ins, so every permission, validation and posting ran for real. Dates come from a clock header honoured only in Development with an explicit flag.

Figures are in each organization's own currency, from its own ledger, over the seeded period (June 2026 to date, opening entries 1 May).

| Organization | Business type | Currency | Revenue | Gross profit | Net profit | Closing cash | Owed by customers | Owed to vendors | Stock value | Journals | Branches |
|---|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Gulf Horizon Properties | re-developer | QAR | 3,194,300 | 3,194,300 | 2,613,063 | 3,814,958 | 1,688,550 | 80,365 | 0 | 273 | 2 |
| Al Baraka Wholesale Distributors | wholesale-fmcg | PKR | 30,969,165 | 3,210,755 | -1,163,801 | 32,729,658 | 12,316,621 | 2,255,983 | 20,086,000 | 731 | 3 |
| Indus Textile Mills | mfg-textile | PKR | 13,793,710 | 2,791,606 | -762,371 | 18,519,125 | 6,487,207 | 2,301,894 | 9,365,201 | 468 | 2 |
| FreshMart Supermarkets | retail-supermarket | AED | 505,305 | 124,947 | -81,572 | 864,179 | 163,513 | 57,715 | 583,098 | 7417 | 3 |
| Al Waha Contracting | con-contractor | SAR | 7,491,000 | 2,695,986 | 2,218,124 | 2,880,738 | 3,751,275 | 1,624,853 | 1,909,500 | 307 | 2 |
| CareBridge Medical Centre | health-hospital | AED | 782,012 | 781,416 | 303,091 | 1,764,723 | 276,804 | 62,933 | 211,270 | 767 | 2 |
| SwiftHaul Cargo | log-cargo | QAR | 319,323 | 267,346 | -40,034 | 968,963 | 145,289 | 56,639 | 0 | 417 | 3 |
| Nimbus Software Services | svc-software | GBP | 376,470 | 406,086 | 119,287 | 951,112 | 265,524 | 31,790 | 0 | 128 | 2 |
| Bright Future School | edu-school | PKR | 3,670,000 | 3,670,000 | 1,016,499 | 10,033,777 | 1,401,100 | 314,850 | 0 | 208 | 2 |
| Saffron Table Restaurants | food-restaurant | AED | 191,544 | 152,564 | 1,020 | 581,750 | 41,357 | 22,195 | 405,364 | 2845 | 2 |
| Green Valley Dairy Farm | agri-dairy | INR | 7,446,423 | 4,796,675 | 2,643,192 | 8,881,910 | 4,607,679 | 949,393 | 5,321,096 | 789 | 2 |
| Meridian Import Export | trade-import-export | AED | 1,550,263 | 951,687 | 633,915 | 1,517,199 | 721,538 | 381,045 | 4,521,566 | 269 | 2 |

| Organization | Records |
|---|---|
| Gulf Horizon Properties | 147 invoices, 26 bills, 32 leases, 26 industry records |
| Al Baraka Wholesale Distributors | 127 invoices, 52 bills, 144 sales orders, 54 POs, 20 industry records |
| Indus Textile Mills | 88 invoices, 45 bills, 114 sales orders, 42 POs, 42 work orders, 41 industry records |
| FreshMart Supermarkets | 57 invoices, 40 bills, 72 sales orders, 24 POs, 2098 till sales, 20 industry records |
| Al Waha Contracting | 18 invoices, 87 bills, 30 POs, 120 industry records |
| CareBridge Medical Centre | 313 invoices, 46 bills, 24 POs, 630 industry records |
| SwiftHaul Cargo | 131 invoices, 113 bills, 280 industry records |
| Nimbus Software Services | 25 invoices, 26 bills, 5 projects, 20 industry records |
| Bright Future School | 88 invoices, 26 bills, 233 industry records |
| Saffron Table Restaurants | 9 invoices, 46 bills, 24 POs, 30 work orders, 840 till sales, 36 industry records |
| Green Valley Dairy Farm | 100 invoices, 55 bills, 132 sales orders, 42 POs, 154 industry records |
| Meridian Import Export | 38 invoices, 48 bills, 43 sales orders, 36 POs, 37 industry records |

**Every organization also has:** branches, departments, 9 users, customers, suppliers, opening balances, a budget, approval policy, payroll with a leaver, attendance, leave, reviews, hiring, expense claims, contracts, fixed assets with depreciation.

**Status mix across all twelve:** invoices 630 paid, 142 part-paid, 325 open, 44 voided (375 past due); bills 331 paid, 52 part-paid, 216 open, 11 waiting for approval (182 past due); approvals 68 approved, 5 rejected, 6 pending; 48 sales returns, 25 purchase returns, 20 till returns. Records and documents also include rejected, cancelled and returned cases.

**Checks run on every organization, all passing:** labelled as demo; trial balance and balance sheet balance; cash flow ties to the ledger; every journal balances; AR and AP ageing equal their control accounts; no invoice or bill paid beyond its total; invoice status follows payments; stock value equals the inventory account; nothing below zero; no cross-organization links; every issued invoice has a journal; no duplicate parties or numbers; at least four months of history; nothing future-dated.

**Idempotency, tested on `ERP_Dev`:** a second full run made 274 calls, and every count and figure was identical afterwards.

**What it did not touch:** no existing workspace received business data, nothing was deleted, and the database was not switched. Backup taken first: `ERP_Dev_before_demo_orgs_20261107.bak` in the SQL Server backup folder. The 12 owner accounts were created under the founder's session, so 12 `AppUser` audit rows sit in `platform-hq`.

**How real the figures look:**

- Margins are illustrative rather than industry benchmarks. Four organizations show a period loss (wholesale, manufacturing, supermarket, logistics) and the restaurant breaks about even. That was left in because not every business is profitable, but a demo meant to impress may want it tuned.
- Most history comes from the seeder's own logic, so patterns are more regular than real trade.
- Audit timestamps follow the pinned document dates.

---

## 13. Demo login credentials

These are synthetic accounts on the development database. The password for all of them is `DemoOnly#2026`. Sign in as `<person>@<code>.example.test`.

**People:** `owner`, `admin`, `finance`, `accountant`, `sales`, `purchase`, `inventory`, `hr`, `ops`.

| Organization | Code | Example owner | Currency | `ops` is a |
|---|---|---|---|---|
| DEMO - Gulf Horizon Properties | `demo-realestate` | `owner@demo-realestate.example.test` | QAR | Leasing officer |
| DEMO - Al Baraka Wholesale Distributors | `demo-wholesale` | `owner@demo-wholesale.example.test` | PKR | Route sales officer |
| DEMO - Indus Textile Mills | `demo-manufacturing` | `owner@demo-manufacturing.example.test` | PKR | Production supervisor |
| DEMO - FreshMart Supermarkets | `demo-supermarket` | `owner@demo-supermarket.example.test` | AED | Cashier |
| DEMO - Al Waha Contracting | `demo-construction` | `owner@demo-construction.example.test` | SAR | Site engineer |
| DEMO - CareBridge Medical Centre | `demo-healthcare` | `owner@demo-healthcare.example.test` | AED | Front desk officer |
| DEMO - SwiftHaul Cargo | `demo-logistics` | `owner@demo-logistics.example.test` | QAR | Dispatcher |
| DEMO - Nimbus Software Services | `demo-software` | `owner@demo-software.example.test` | GBP | Project manager |
| DEMO - Bright Future School | `demo-education` | `owner@demo-education.example.test` | PKR | Admissions officer |
| DEMO - Saffron Table Restaurants | `demo-restaurant` | `owner@demo-restaurant.example.test` | AED | Restaurant manager |
| DEMO - Green Valley Dairy Farm | `demo-dairy` | `owner@demo-dairy.example.test` | INR | Herd supervisor |
| DEMO - Meridian Import Export | `demo-tradeco` | `owner@demo-tradeco.example.test` | AED | Documentation officer |

Example: `finance@demo-logistics.example.test`. Do not reuse these accounts or `Seed__AllowDemoClock` on a shared or production host.

---

## 14. Test results

PENDING

### Major workflows

| Workflow | State | Evidence |
|---|---|---|
| Procure to Pay | Works: requisition, approval, PO, receipt, part-billed bill, payment, purchase return | `PurchasingFlowTests`, `PurchasingLedgerTests`, `PurchasingApprovalTests`; hundreds of demo cycles |
| Order to Cash | Works: quote, order, delivery, invoice, receipt, sales return, void. **No stock reservation** | `SalesFlowTests`, `SalesOrderTests`, `SalesReturnTests`; demo cycles |
| Record to Report | Works: journals to statements, close, opening balances, cash flow. **Single currency, no consolidation** | `FinanceCoreReportsTests`, `LedgerTests`; trial balance checks on all twelve |
| Plan to Produce | Works: plan, requirements, issue, production, inspection, finished goods, reversal. **No capacity planning; plans are records not linked to work orders** | `ManufacturingPlanToProduceTests`; demo production |
| Hire to Retire | Works: opening, candidate, hire, attendance, leave, payroll, review, separation. **Final settlement is empty where no end-of-service rule exists, so the seeder pays final salary off cycle** | `HrApiTests`, `HrLedgerRulesTests`; demo payroll |
| Multi-tenant isolation | Works | `RoleEscalationTests`, `ReferenceValidationTests`, `IndustryPackTests` |
| Industry records | Works: field rules, approvals, invoice, bill, stock, restricted fields | `IndustryPackTests` (11 tests) |

**Not verified:** a person has not clicked through the new screens against the seeded data in a browser, on a phone, or in Arabic. Specs mock the API. The eight SQL Server race tests are skipped locally, as before. The mobile app was not changed or tested against these changes.

---

## 15. Remaining limitations

- Everything in sections 5, 6 and 7 (open).
- **Critical C1** is open.
- **Foreign key checks** were sampled, not exhaustive.
- **Record engine:** JSON fields, no reports over them beyond counts and totals, and tenant custom fields are not shown on record forms. Retention is a number on a valuation, not a ledger balance.
- **Owner dashboard:** branch comparison depends on documents carrying a branch. POS sales, payroll and stock cost do not, so they appear as "without a branch", and the screen says so.
- **Purchase return, sales return and owner payout accounting** are new and tested, but have no real-world use yet.
- **`main`'s model snapshot was stale** before this branch: it lacked the assistant tables. This branch's migration leaves those out and its snapshot includes them. Merging `claude/platform-console` will conflict in the snapshot, and its two extra migrations need ordering. Regenerate the snapshot when merging.
- **Hand-kept SQL scripts** in `database/scripts` were not updated. EF migrations are the source for this work.
- **AI:** see section 19.

---

## 16. Recommended next phases

1. **Fix C1**, with invitation acceptance. Do this before any public sign-up.
2. **Browser pass** over the new screens with the demo logins, including Arabic and mobile widths.
3. **Batch, expiry and unit conversion.** The largest group of blocked industries.
4. **Foreign-currency documents and revaluation.**
5. **Close the open defects in section 7.**
6. **AI gaps in section 19**, on top of `claude/platform-console`.
7. **Consolidation and intercompany.**
8. **Retention, percent-of-completion, landed cost.**
9. **Typed tables** for whichever record types customers use most.
10. **Patient data controls**, only if real healthcare customers are a goal.

---

## 17. Decisions for you

1. **Managed-property rent.** Rent newly billed on existing `managedForOwner` properties now credits an owner payable instead of company income. This is correct accounting, but it does change existing behaviour. Setting `creditRentToOwner=false` on a property keeps the old treatment. The management fee is recognised at payout.
2. **Old audit rows.** Existing audit rows hold two-factor secrets in the database, though they are hidden when read. They should be deleted, and affected users should enrol again. I did not delete them because you did not ask for that.
3. **Demo profitability.** Whether loss-making demo organizations are acceptable.

---

## 18. Benchmark

`AGENTS.md` forbids third-party product names in the repo, so this report uses labels: Suites A, B, C, F (large commercial suites), D and E (open-source suites), G (small and mid-market accounting-led). The key is kept outside the repo in the session's notes folder. **This comes from general product knowledge and was not checked against current releases or pricing.**

| Area | Large suites (A, B, C, F) | Open-source (D, E) | Accounting-led (G) | This product |
|---|---|---|---|---|
| Core finance | Very deep, multi-book, multi-currency | Solid | Solid | Sound ledger; **single currency, one book** |
| Multi-company | Full consolidation | Good | Varies | Entities and access, **no consolidation** |
| Inventory and supply chain | Lots, serials, planning, landed cost | Lots, serials | Basic to good | Moving average; **no lots or reservations** |
| Manufacturing | MRP, capacity, quality | MRP, quality | Add-ons | Requirements, quality records, **no capacity** |
| Workflow | Designers | Configurable | Limited | Policies by amount and level, record transitions; **no designer** |
| Industry depth | Vertical editions and partners | Community apps | Partners | One engine and thin modules, built in |
| Customization | Platforms and studios | Studio or code | Limited | Custom fields and definitions in code; **no tenant designer** |
| Reporting | Designers and analytics | Pivot and builder | Good | Fixed reports, owner dashboard; **no designer** |
| UX | Heavy, improving | Modern | Simple | Modern, Arabic and right-to-left, mobile app |
| Local fit | Global, costly | Community | Regional | Gulf and South Asia payroll and tax packs |

**Worth copying:** batch and expiry (all), unit conversion (all), consolidation (A, B, C, F), reservation and available-to-promise (A, C), a studio-style field and view editor (D), a report builder (D, E).

**Not worth copying:** multi-book and deep planning engines before there is customer demand, and marketplace-style app ecosystems. The fit for this product is regional small and mid-market businesses, where being simple and complete in one codebase is the advantage.

---

## 19. Founder-controlled AI

Full evidence is in appendix G. **I changed nothing here**, because the newer work sits on the unmerged `claude/platform-console` and changing the same files from `main` would conflict.

| Requirement | State |
|---|---|
| Enable or disable; add, update, remove providers; default order; change model without business changes | Present |
| Hide a provider | Partial: unticking its uses. No separate flag |
| Your four named vendors and others | **Partial: one adapter, for the common chat-completions protocol. Vendors offering only a native API need their own adapter** |
| Keys server side, not in logs, queries or the frontend | Present. No live leak found |
| Availability by tenant or subscription | Partial on `main`; the branch adds plan allowance and per-client limits. **No per-tenant provider choice** |
| Usage monitoring and limits | Present, in tokens |
| Cost monitoring and spend cap | **Missing** |
| ERP works with AI off | Present |

**Two risks:** on Linux the key ring is not encrypted, so a storage backup plus the database decrypts every key. On `main`, workspace tools can send HR and payroll data to the provider; the fix is only on the branch.

---

## 20. Schema changes

One additive migration, `MultiIndustryErp`, applied to `ERP_Dev`. It adds new tables and nullable or defaulted columns only, so older code keeps working. The APIs on ports 5080 and 5317 stayed healthy after it ran.

**New tables:** `ind_Records`, `ind_RecordLines`, `ind_RecordEvents`, `ApprovalSteps`, `FiscalYearCloses`, `ast_Assets`, `ast_Categories`, `ast_DepreciationRuns`, `ast_DepreciationEntries`, `mfg_WorkOrderMaterialIssues`, `pur_OrderBillLines`, `pur_Returns`, `pur_ReturnLines`, `pur_ReturnBillShares`, `sls_Returns`, `sls_ReturnLines`, `svc_RetainerPeriods`, `re_OwnerPayouts`.

---

# Appendices

Findings as read at the base commit 2c631b6. Fixes since then are in section 7 and 11; the seeder notes describe the API before those fixes.

---

# Appendix A

### Universal core audit: finance, workflow, configuration engine

Scope: everything outside sales, purchasing, inventory and HR. Method: code read at commit 2c631b6 (all paths below are under `backend/` unless stated). Nothing was run; behaviour statements come from reading handlers. Items marked **(not verified)** were not traced to the end. Classification terms: **G** generic and reusable, **I** industry-specific, **P** partly reusable, **M** missing, **X** incomplete or wrong.

#### 1. Overall verdict

The ledger kernel (double entry, single posting gateway, period lock, soft-void by reversal, automatic audit trail, gap-free numbering) is sound and genuinely industry-neutral. Around it the finance core is a single-currency, single-book, calendar-year system. Anything a mid-size multi-entity or multi-currency business needs at period end (revaluation, year-end close, consolidation, cash-flow statement, fixed assets, expense claims) does not exist. Configuration is real for chart of accounts, tax codes, numbering, custom fields and roles; it is thin for approvals and settings, and several declared settings are dead.

#### 2. Record to Report, traced

| Step | What the code does (evidence) | Class | What is missing or wrong |
|---|---|---|---|
| Journal draft | `CreateJournal` saves a draft with number `DRAFT-xxxxxxxx`; real number is allocated on post so there are no gaps (`Application/Finance/JournalFeatures.cs:72-78`). Lines need account, one-sided positive amount, min 2 lines (`:31-47`). | G | Manual journals are always in the entity base currency (`:72`). No recurring or template journals. No attachments rule (journal attachments exist: `Application/DependencyInjection.cs:81`). |
| Journal post | `PostJournal` re-checks account active and postable, balance, lock date, then numbers (`JournalFeatures.cs:102-113`; `Domain/Finance/Finance.cs:104-125`). | G | Setting `core.journalRequiresApproval` is declared (`Application/Settings/SettingsFeatures.cs:21`) but nothing reads it (grep over src and modules: no consumer). Manual journals are never approval-gated. |
| Reverse | Only manual journals may be reversed here; system-posted ones must be voided at source (`JournalFeatures.cs:124-130`). `PostingService.ReverseAsync` posts mirrored lines, marks original `Reversed`, refuses re-reversal (`Posting.cs:68-77`). | G | Good control. Reversal date for voids is "today" (`Posting.cs:79-83`), see lock defect D3. |
| IPostingService | Single gateway: checks entity access, account postable/active, balance, lock, then numbers and adds to the caller's unit of work (`Posting.cs:48-66`). System roles resolve accounts by `SystemRole` (`:85-89`). | G | `JournalLine.Dimensions` column exists (`Domain/Finance/Finance.cs:139`) but no code writes or reads it (grep: no hits in Application or modules). `ExchangeRate` is stored on the header only; lines carry no foreign amount. |
| Period lock | One date per legal entity: `LegalEntity.BooksLockedTo` (`Domain/Organizations/Organizations.cs:47`). Post fails if `Date <= lock` (`Finance.cs:114`). `LockBooks` sets it, refuses a future date (`JournalFeatures.cs:138-146`). | P | No period or fiscal-year entity, no per-module lock (sales vs purchasing vs payroll), no soft-close with exceptions, no reopen log beyond generic row audit. Lock can be moved backwards by anyone with `core.entities.write` (`JournalFeatures.cs:29`): no dedicated permission, no approval, no reason. |
| General ledger | `GetGeneralLedger` lists lines with running balance, opening before `From` (`Application/Finance/LedgerReports.cs:84-100`). | G | Loads all lines up to `To` into memory and sums on the client (`:88-90`, `:126-129`). Fine at demo volume; will not scale to hundreds of thousands of lines **(performance not measured)**. |
| Trial balance | Opening, movement, closing per account, `Balanced` = sum of closings is zero (`LedgerReports.cs:78-82`, `:123-143`). | P | With no `LegalEntityId` it adds all legal entities together, including entities with different base currencies (`:126`). Chart of accounts is org-wide (`Account : OrgEntity`, `Finance.cs:48`), so every entity shares one chart. |
| Income statement | Income and expense by account for a range, grouped by `ReportingGroup` (`LedgerReports.cs:102-110`). | G | No comparatives, no budget column, no cost-centre or business-unit slicing. |
| Balance sheet | Assets, liabilities, equity plus "current earnings" computed as all-time income minus expense to the as-of date (`:112-121`). | X | Because no year-end close exists, "current earnings" is cumulative over every year since inception, and retained earnings never receives anything. Prior-year comparatives and statutory equity roll-forward are impossible. |
| Cash-flow statement | Not present. `GetCashMovements` is a bank/cash account movement table by source type (`Application/Finance/CashReports.cs:73-91`); `GetCashForecast` is a forward 52-week projection (`:93-116`). | M | No operating/investing/financing statement (direct or indirect). |
| Retained earnings / year-end close | Accounts `3100` (role `equity.retained`) and `3900` (`equity.opening`) are seeded (`Application/Templates/Templates.cs:55-56`). No handler references them (grep of `equity.retained`/`equity.opening` outside seed data: only `ProgramSeeder.cs:514`). | M | No closing entry, no lock-then-close wizard, no P&L reset. Trial balance in year 2 carries P&L balances as "opening". |
| Opening balances | Only party and item CSV-style imports exist (`Application/Imports/CoreImports.cs:14,36`). `equity.opening` role is unused outside the demo seeder. | M | No opening-balance journal importer, no opening AR/AP by document, no opening stock-value tie-out. Go-live of an existing business means hand-keyed manual journals. |
| Multi-currency | `Currency` table (seeded 24 codes, `Templates.cs:143-151`) and `ExchangeRate` entity exist (`Finance.cs:16-22`). But invoices, bills, receipts, payments and cheques all force `CurrencyCode = entity.BaseCurrency`, rate 1 (`Application/Finance/Invoicing.cs:88`, `BillFeatures.cs:89`, `ReceiptFeatures.cs:85`, `PaymentFeatures.cs:99`, `ChequeFeatures.cs:202,209`). No handler or endpoint reads or writes `ExchangeRate` (grep over `Application` and `Api/Endpoints`: no hits; seeded only by `ProgramDemoTopUp.cs:284`). `Account.CurrencyCode` and `Party.CurrencyCode` are stored but never used when posting. | X | Effectively single-currency. A business that invoices or buys in a foreign currency cannot do so. |
| FX revaluation | `SystemRoles.Fx = "fx.gainloss"` and account `7900` exist (`Posting.cs:37`, `Templates.cs:70`). No revaluation job, no realised-gain posting on settlement. | M | Consequence of the above. |
| Intercompany | `LegalEntity` is the accounting boundary (`Organizations.cs:30-49`); `PostingRequest` takes exactly one `LegalEntityId` (`Posting.cs:12-14`). | M | No due-to/due-from, no mirrored postings, no intercompany invoice or settlement, no elimination. |
| Consolidation | Reports with `LegalEntityId = null` simply sum rows across entities the user may see (`LedgerReports.cs:126`, query filter in `Infrastructure/Persistence/AppDbContext.cs:536-539`). | X | This is aggregation, not consolidation: no currency translation, no eliminations, no ownership percentages. Mixed-currency totals are silently wrong. |
| Chart of accounts edit safety | `UpdateAccount` blocks code/type change only for `IsSystem` accounts (`Application/Finance/FinanceFeatures.cs:139-142`). Any other account can change `Type` or `IsPostable` even with posted lines **(no usage check seen)**. | X | Changing an expense account to an asset reclassifies all history across closed periods. |

#### 3. Sub-ledgers, cash, tax, assets and other finance features

| Feature | Evidence and behaviour | Class | Gap that breaks a real process |
|---|---|---|---|
| Accounts receivable (invoice, credit note, receipt, allocation) | `InvoicingService` builds, issues (Dr AR, Cr income and tax), voids by reversal (`Invoicing.cs:95-123`). Receipts allocate oldest-due-first (`SettlementService.cs:66-79`). Ageing, party statement, credit-note application exist (`ArAgeing.cs`, `PartyLedger.cs`, `Domain/Finance/Documents.cs:162`). | G | No customer credit limit (grep `CreditLimit`: none). No recurring invoices, no dunning letters (reminder source only: `Reminders/CoreReminderSource.cs`). Tax line fixed to one output account regardless of tax code (`Invoicing.cs:107`). |
| Accounts payable (bill, payment, void) | Bill post: Dr expense (plus non-recoverable tax), Dr input tax, Cr AP (`BillFeatures.cs:151-165`). Void by reversal. | P | No supplier debit note / bill credit (`Bill` has no credit-note flag, `Documents.cs:69-104`). No payment run or bank file. Payments are not approval-gated (see approvals). |
| Cash and bank | Cash/bank accounts by `IsBank` or role (`BankFeatures.cs:176`), `TransferFunds` posts Dr/Cr between two accounts (`:150-157`). | P | Transfer has no currency handling and no cross-entity guard beyond the posting call. |
| Bank reconciliation | Paste import, de-duplication, auto-match same amount within 5 days, manual match, book unmatched line (`BankFeatures.cs:55-143`). | P | `Reconciled` means "nothing unmatched" (`:156`). There is no statement closing balance, no reconciled-through date, no sign-off, and no lock on matched items. One-to-one matching only (no split or batch deposits). Unmatching a booked line leaves its journal (`:105-107` comment admits it). |
| Cheques / post-dated instruments | Lifecycle Received, Deposited, Cleared, Bounced, Replaced, Cancelled; bounce reverses the settlement and reopens invoices (`ChequeFeatures.cs:99-170`, `SettlementService.cs:51-64`). Issued cheques cleared separately (`Api/Endpoints/PayablesEndpoints.cs:41`). | P | Generic enough (instrument with date and bank), but built around a regional cheque habit. Cleared cheques cannot be returned afterwards (`:123,137`). Core COA seeds "Cheques in hand/issued" accounts for every tenant (`Templates.cs:37,48`). |
| Expense claims / petty cash | No entity. Only `ProjectExpense` inside the Projects module (`modules/ERP.Modules.Projects/Features/ExpenseFeatures.cs`) and a "petty cash" mention in a bank-account rule comment (`BankAccountRules.cs:14`). Employee advances account `1250` seeded (`Templates.cs:39`). | M | Staff cannot claim expenses; no float/imprest, no receipt-to-claim-to-reimbursement flow. Workaround is to enter vendor bills. |
| Tax codes | `TaxCode` with kind (VAT, GST, sales tax, withholding, excise, other), rate, recoverable, default, optional legal entity (`Finance.cs:25-43`). Rates come from country packs (`CountryPack.cs:43`). | P | Simple add-on tax only: `Total = Net + Tax` (`Invoicing.cs:91`). `TaxKind.Withholding` exists but has no behaviour (grep in Application: none), so withholding or deduct-at-source taxes post with the wrong sign. No inclusive pricing, no compound tax, no reverse charge flag (convention: code starts with "RC", `Templates/TaxReturns.cs:19`). Tax rounding is per line at 2dp. |
| Tax returns (per country) | Pack-defined boxes computed from document tax lines (`Templates/TaxReturns.cs:38-56`, `Finance/TaxReturnFeatures.cs:31-68`). Packs exist for AE, GB, IN, PK, QA, SA, US and all expose a return definition (grep `TaxReturn`). | P | Report only: no filing status, no period close, no payment posting, no e-file. Computed from documents, not the ledger, so manual journals to tax accounts are invisible. Content of each pack's boxes **(not verified)**. |
| Budgets | Annual budget per legal entity, account by calendar month, budget-vs-actual by month range (`BudgetFeatures.cs:97-119`). | P | Calendar year only: `core.fiscalYearStartMonth` (`SettingsFeatures.cs:17`) is declared and unused. No commitment/encumbrance, no budget check on bill or journal posting, no revision history. |
| Fixed assets | No register entity, no depreciation run. Accounts `1500`/`1590`/`6400` are plain COA rows (`Templates.cs:43-44,68`); depreciation appears only as hand-made journals in the demo seeder (`Api/ProgramSeeder.cs:2841-2844`). | M | No asset acquisition-to-disposal, no schedule, no gain/loss on sale, no asset tags. |
| Contracts | No generic contract entity anywhere in core. `Lease` is the only contract-like record and lives in the real-estate module (`modules/ERP.Modules.RealEstate/Domain/Leasing.cs:10`). Services has `ServiceEngagement`. | M | Every industry that bills from agreements (subscriptions, rentals, maintenance, retainers) must build its own. No generic recurring-billing engine. |
| Documents / attachments | Generic owner-resolver pattern: any entity registers a type, read and write permission (`Workflow/IAttachmentOwnerResolver.cs:6-35`); 8 core types registered (`DependencyInjection.cs:76-83`), modules add theirs. Size limit from setting (`AttachmentFeatures.cs:131`), extension allow-list, expiry date with reminder (`CoreReminderSource.cs:34-40`). | G | No versioning, no document numbering/classification workflow, no attachments on legal entities, bank statements or budgets **(owner types not registered)**. |
| Notifications | `Notifier` stores an in-app notification and pushes it (`Workflow/WorkflowServices.cs:48-56`); email log and reminders exist. | G | Fixed code calls only (cheque bounce, approval decision). No user-configurable rules or digest/preferences **(not verified in UI)**. |
| Audit log | `AuditSaveChangesInterceptor` writes before/after JSON for every insert, update, delete of org data except a short exclusion list (`Infrastructure/Persistence/AuditSaveChangesInterceptor.cs:27,45-118`); void reasons written explicitly (`Invoicing.cs:135-180`). | G | Strong. Not verified: tamper resistance (no DB-level immutability seen), retention policy. |
| Numbering sequences | Per document type, optional per legal entity, tokenised pattern, yearly reset (`Common/NumberingService.cs:18-44`; model `Domain/Platform/Platform.cs:70-79`). Concurrency by row version. | G | Yearly reset uses the calendar year `d.Year` (`:31`), not the fiscal year. Pattern editing UI **(not verified)**. |
| Custom fields | JSON column plus definitions per entity type, per legal entity, with types, options, lookup, required, searchable (`Customization/CustomFieldFeatures.cs:32-60`). Modules register customisable entities; country packs seed fields (`OnboardingService.cs:123-129`). | G | Fields are data-only: no conditional visibility, no field-level permissions, no calculated fields **(not verified)**. |
| Document templates (print) | Typed print DTOs for invoice, receipt, bill, payment, journal (`Printing/PrintFeatures.cs:12-50`) plus a generic prepared-text document any module can PDF/email (`Printing/GenericDocument.cs:12-23`). | P | Layout is code, not tenant-editable: no template designer, no per-tenant header/footer/logo variants beyond the legal-entity header fields **(editing UI not verified)**. |
| Settings definitions | `ISettingsCatalog` merges core and module definitions; values per workspace, optionally per legal entity in the store (`Settings/SettingsFeatures.cs:12-47`). | X | Per-entity override cannot be set: `UpdateSettings` calls `SetRawAsync(key, value, ct: ct)` with no entity id (`:88`). Of 7 core settings only `finance.chequeDueAlertDays` and `core.maxAttachmentMb` have consumers (`Dashboard/FinanceDashboard.cs:84`, `AttachmentFeatures.cs:131`). `core.dateFormat`, `core.fiscalYearStartMonth`, `core.invoiceTermsDays`, `core.journalRequiresApproval` are dead in the backend **(frontend use of dateFormat not checked)**. |
| Units of measure | Only a free-text `Item.Uom = "ea"` string (`Domain/Catalog/Item.cs:20`). | M | No UoM table, no conversion factors (case/each, kg/g). Blocks purchase-in-cases, sell-in-units. |
| Currencies | Global (not tenant) `Currency` table with decimals (`Finance.cs:6-14`). | X | `Decimals` is stored (KWD/BHD/OMR/JOD = 3) but money is rounded and validated to 2dp everywhere (`Invoicing.cs:193-196`, `MoneyValidation.cs:11,21`). 3-decimal currencies cannot be represented. |
| Branches / business units | `BusinessUnit` belongs to a legal entity (`Organizations.cs:52-64`). Carried by `Invoice`, `Bill`, `JournalLine` only (`Documents.cs:41,94`, `Finance.cs:137`); AR ageing can group by it (`ArAgeing.cs:71`). | P | Not on receipts, payments, cheques, budgets, items, parties, bank accounts. Trial balance, P&L and balance sheet cannot filter by unit. Journal-line unit is written but never reported. |
| Departments | Core has no department concept. `Department` exists only inside HR (`modules/ERP.Modules.Hr/Domain/Structure.cs:44`, carrying a `BusinessUnitId`). | I (HR only) | Non-HR cost-by-department is impossible; the free `Dimensions` JSON is unused (above). |
| Reporting | Fixed reports: trial balance, GL, P&L, balance sheet, ageing, tax summary, cash movements and forecast, finance dashboard. | P | No report designer, no saved filters or schedules, no export framework beyond what individual endpoints provide **(export formats not verified)**. |

#### 4. Approvals (what is actually enforced)

| Question | Finding (evidence) | Class |
|---|---|---|
| Which document types consult the policy engine? | Exactly one: vendor bills, `RequiresApprovalAsync("bill", total)` (`Finance/BillFeatures.cs:143`). Only other callers of `IApprovalService` are the generic submit endpoint and bill withdraw. Payments, manual journals, invoices, credit notes, cheques, budgets, receipts do not consult it. The `PendingApproval` status exists on invoices (`Documents.cs:5`) but invoice code never sets it. | X |
| Policy model | `ApprovalPolicy { DocumentType, MinAmount, ApproverPermission, IsActive }` (`Domain/Platform/Platform.cs:84-90`). | P |
| Multi-level? | No. One request, one decision (`Platform.cs:104-114`). No stages, no per-amount tiers beyond one threshold per policy row. | M |
| Who may approve? | `ApproverPermission` is stored but never read; `DecideAsync` always requires `core.approvals.decide` (`WorkflowServices.cs:31`). Policies are not scoped by legal entity or business unit. | X |
| Delegation / substitute approver / escalation / timeout | None found. | M |
| Segregation of duties | Requester cannot decide own request (`Platform.cs:105-107`). | G |
| Approval then post | Approving does not post the bill; the maker must press post again (`BillFeatures.cs:143-155`). A default `general` policy is created for every workspace (`OnboardingService.cs:155`) but no document type "general" is ever consulted, so it gates nothing. | P |

#### 5. Configuration engine state

| Topic | Finding (evidence) | Class |
|---|---|---|
| IndustryTemplate fields | `Key, Label, Description, Availability, SuggestedModules, Accounts, Roles, Sequences, Settings, SortOrder` (`Templates/Templates.cs:17-27`). Four neutral industries in core (`:125-132`); each module adds one keyed to itself (e.g. `RealEstateModule.cs:107`). Same key from a module overrides the core one (`OnboardingService.cs:27`). | P |
| What a template cannot carry | No tax codes, custom fields, approval policies, document templates, fiscal year start, payment terms, currencies, dashboards, report layouts, or demo data. Country packs carry taxes and custom fields only (`CountryPack.cs:35-55`); everything else is code. | X |
| Onboarding | One transaction creates org, first legal entity (`ENT-0001`), currencies, COA (core plus template), tax codes (pack or neutral `STD`/`EXEMPT`), custom fields, sequences, roles, settings, modules, admin user, default approval policy (`Infrastructure/Services/OnboardingService.cs:63-168`). | G |
| Onboarding limits | Single legal entity, no fiscal-year or opening-balance step, no import of parties/items, no approval-policy or numbering choices, base currency not changeable later by design **(change flow not verified)**. Core COA is hard-coded in code (`Templates.cs:32-71`) and contains payroll, WIP, inventory and cheque accounts for every tenant. | P |
| OrganizationModule | One row per module and workspace with enabled flag and dates (`Organizations.cs:67-74`). Disabled modules return 403 and keep data (`Modularity/Modules.cs:94-108`). | G |
| Module `Requires` enforced? | Enforcement code exists for enable and disable (`Modularity/Features/ModuleFeatures.cs:65-68`). But no installed module declares `Requires` (grep of all `*Module.cs`: none; manifests such as `PosModule.cs:22`, `RetailModule.cs:22`, `ManufacturingModule.cs:22` pass only key, name, description, category). Onboarding enables suggested modules directly without calling this check (`OnboardingService.cs:148-151`). Industry templates suggest only their own key (e.g. `PosModule.cs:68`). | X |
| Practical effect | A tenant can enable retail, POS or manufacturing without inventory, or sales without CRM, and hit runtime errors instead of a clear block. Dependencies are documented nowhere in code. | X |
| Settings per tenant | Workspace-wide values editable; legal-entity overrides readable but not writable (see section 3). | X |
| Tenant can configure | Chart of accounts incl. numbering mask (`AccountNumbering.cs`), tax codes, number sequences, custom fields, roles and permissions, legal entities, business units, attachment size, cheque alert days, approval policy for bills. | G |
| Tenant cannot configure | Fiscal year, period calendar, multi-currency rates, approval chains, print layouts, posting rules (account mapping is by `SystemRole` string only), dimensions, workflows/states, automation rules, report layouts, units of measure. | M |

#### 6. Defects worth fixing first (each with evidence)

| ID | Defect | Evidence | Impact |
|---|---|---|---|
| D1 | No year-end close, so "current earnings" accumulates forever and retained earnings stays empty. | `LedgerReports.cs:118`; no consumer of `equity.retained` | Cannot produce a compliant second-year balance sheet or equity statement. |
| D2 | Documents cannot be raised in a foreign currency; the exchange-rate table has no API and no consumer. | `Invoicing.cs:88`, `BillFeatures.cs:89`, grep of `ExchangeRate` | Any importer, exporter or multi-country customer is blocked. |
| D3 | Void after lock: the reversal is dated today, the document flips to `Voided` while keeping its old date, so tax and ageing reports for the locked period change retroactively. | `Posting.cs:79-83`, `Invoicing.cs:119-120`, `TaxReturnFeatures.cs:35` (voided excluded by original date) | A filed return no longer reconciles to the document set. |
| D4 | Tax return and tax summary read documents, not the ledger, and the summary and the return use different status filters. | `CashReports.cs:56` (excludes Draft, Voided) vs `TaxReturnFeatures.cs:35` (also excludes PendingApproval) | Totals can differ between the two screens; manual tax journals ignored. |
| D5 | Approval engine gates only bills; `ApproverPermission` ignored; default "general" policy gates nothing. | `BillFeatures.cs:143`, `WorkflowServices.cs:31`, `OnboardingService.cs:155` | Payments (cash leaving the company) and journals have no maker-checker despite the UI offering policies. |
| D6 | Three-decimal currencies unsupported (rounding and validation fixed at 2dp). | `Invoicing.cs:195`, `MoneyValidation.cs:21` | Cannot operate in several Gulf and Levant currencies even though they are seeded (`Templates.cs:145-148`). |
| D7 | Dead or partly dead settings give the impression of control. | Section 3, settings row | Support load: users change a setting and nothing happens. |
| D8 | Account type/postable flags editable on accounts with history. | `FinanceFeatures.cs:139-146` | Silent reclassification of closed periods. |
| D9 | Module dependency model is unused. | Section 5 | Broken tenant setups. |
| D10 | Aggregate reports sum across entities with different base currencies. | `LedgerReports.cs:126` | Wrong group totals presented as correct. |

#### 7. Weighing the existing reviews

`docs/PROJECT-REVIEW-2026-11-05.md` section 3 is largely accurate: revaluation, fixed assets, inter-company, cash-flow statement, withholding tax, payment runs, supplier debit notes and bank feeds are missing as stated (confirmed above). Corrections and additions:
- It says year-end close is "Partial: period lock exists". Period lock exists, but there is no close at all; the balance sheet workaround in `LedgerReports.cs:118` hides the absence.
- It says the "Document approvals" row needs checking against payments and journals. Confirmed: only bills are gated, and a declared journal-approval setting is dead.
- It lists "Foreign-currency revaluation: documents carry an exchange rate". In practice documents cannot carry a non-base currency (D2); the gap is larger than stated.
- It does not mention: 3-decimal currencies, missing UoM table, dead settings, module `Requires` never used, per-entity settings not writable, consolidation producing mixed-currency sums.
- It lists "cost centers/analytic dimensions: Missing". Partly true: a `Dimensions` column and `BusinessUnitId` exist on journal lines but nothing consumes them.
`docs/GAP-ANALYSIS.md` was searched for finance terms (fixed assets, consolidation, revaluation, year-end, expense claims, contracts); no matching lines were found **(file not read in full)**.

#### 8. Not verified

Frontend behaviour and screens, report export formats, performance at volume, database constraints and triggers (including immutability of posted rows), contents of each country pack's tax boxes, attachment storage back end, scheduled job behaviour, dashboard widgets, platform console billing, and anything under sales, purchasing, inventory or HR.

---

# Appendix B

### Supply chain audit: Procure to Pay, Order to Cash, Inventory, Distribution

**Method.** Static read of handlers, endpoints and tests on `claude/infallible-napier-9331dc` (HEAD 2c631b6). Nothing was executed: no server, no test run, no database. Defects below are traced in code; where a defect has no test to confirm it, it is marked *(static)*. Paths are shortened: `P/` = `backend/modules/ERP.Modules.Purchasing/`, `S/` = `.../ERP.Modules.Sales/`, `I/` = `.../ERP.Modules.Inventory/`, `D/` = `.../ERP.Modules.Distribution/`, `F/` = `backend/src/ERP.Application/Finance/`.

**What holds up.**
- Every journal goes through `F/Posting.cs:48` (`PostingService.PostAsync`). `JournalEntry.EnsureCanPost` (`backend/src/ERP.Domain/Finance/Finance.cs:104-115`) rejects unbalanced entries, one-sided lines, fewer than two lines and locked periods. Debits equal credits on every path below by construction.
- Goods receipt, delivery, trip completion, bill post/void, payment and PO/order-to-document conversions run in `IUnitOfWork.InTransactionAsync` (`AppDbContext.cs:468`, nested calls join the outer transaction), so stock and ledger commit together. Invoice issue/void and receipts have no explicit transaction and rely on one save call, which holds unless a notification handler saves midway (S8). Requisition and quote conversions have no transaction but touch no ledger.
- Voids of bills, payments, invoices and receipts are clean: reversal journal with `ReversalOfId`, original kept and marked `Reversed`, reason stamped, `AuditEvent` written.
- There is no double-COGS risk on the sales path: `IssueAsync` posts only AR, income and tax (`F/Invoicing.cs:95-113`); COGS comes only from stock issues, and conversion issues only the undelivered remainder (`S/Features/OrderFeatures.cs:384`).

**Weighing the 2026-11-05 review.** Its "9 open cards" is stale: TASK-131 to 138 are `ready-for-review` and the code reflects them; only TASK-139 is open. Its "slow-mover report missing" is wrong (`I/Features/InventoryReports.cs:69`). Its "credit limit field not verified" is now resolved: no such field exists anywhere. Its P2P section understates the problems: it reports GR/IR and PO-to-bill as built, without the defects in section 1. TASK-135 passed its stated scope but leaves defect P4 open.

---

#### 1. Procure to Pay, step by step

| Step | State | Finding |
|---|---|---|
| Vendor | Exists, correct | Party with role flag; `VendorRules.RequireVendor` on PO, bill, payment |
| Requisition create/edit | Exists, wrong | Edit and delete allowed once Approved, no re-approval (`P/Features/RequisitionFeatures.cs:163,183`). No date rules in validator (`:82-93`) |
| Requisition approval | Status only; engine not consulted | `PUT /status` with `requisitions.manage`; requester can approve own request. `purchasing.requireRequisitionApproval` is declared and read nowhere (`P/PurchasingModule.cs:45`) |
| Requisition to PO | Exists, wrong | Currency hard-coded `"QAR"` (`RequisitionFeatures.cs:220`), vendor not validated (`:216`), two saves with no transaction (`:250-251`), order date forced to today |
| PO create/edit/delete | Exists, correct | Guards from TASK-133 and 136 present |
| PO approval | Status only; engine not consulted | `Draft -> Approved` directly (`P/Features/PurchasingTransitions.cs:11`). `purchasing.autoApproveOrdersBelow` is dead (`PurchasingModule.cs:46`). `approvals.SubmitAsync` is called only for bills, payroll and general requests |
| Goods receipt, stock move | Exists, wrong | No warehouse on the input: always the first active warehouse, unordered (`P/Features/ReceiptFeatures.cs:209`, `I/Features/StockPostingService.cs:262-269`). See P1, P2 |
| Goods receipt, journal | Exists, correct when stocked | Dr `inventory` 1300, Cr `inventory.clearing` 2050 (`StockPostingService.cs:69-79`). Test: `PurchasingLedgerTests.cs:89` |
| Bill from PO | Exists, wrong | One bill per PO, full quantity, unreceived goods expensed. See P3 |
| 3-way match | Missing | `PostBill` never looks at a PO or receipt (`F/BillFeatures.cs:137-172`, core cannot see the module). A draft bill's quantity, price and account can be edited freely before posting. A manual bill can double a PO |
| Price variance | Exists, half broken | Upward works; downward fails with 400. See P4. No test covers variance at all |
| Bill post | Exists, correct | Dr clearing/expense/variance and input tax, Cr AP with party. Approval engine consulted when a `bill` policy matches (`BillFeatures.cs:143`) |
| Payment | Exists, correct | Dr AP, Cr bank or `instruments.issued` for cheques; allocation checks; advance stays unallocated (`F/PaymentFeatures.cs:87-126`) |
| Cheque clearing | Exists, correct | Dr `instruments.issued`, Cr bank (`PaymentFeatures.cs:170-183`) |

**Reversals**

| Reversal | State | Finding |
|---|---|---|
| Cancel PO | Partial | `Draft`, `Submitted`, `Approved` only, requisition released (`P/Features/OrderFeatures.cs:287`). `PartiallyReceived` cannot be cancelled or short-closed (`PurchasingTransitions.cs:14`): every receipt must be deleted first |
| Reverse receipt | Exists, weak trail | `DELETE /receipts/{id}`: journal reversed correctly, but the document and stock movements are soft-deleted, no reason captured, and the rows leave the stock ledger (`StockPostingService.cs:191`). The GL shows entry and reversal; the stock ledger shows nothing. See P6 |
| Return to vendor | Missing | No debit note, no purchase return document, no negative receipt. Once billed, the receipt is locked (`ReceiptFeatures.cs:254`) |
| Void bill | Exists, correct | Reversal, reason, audit, PO released to re-bill (`BillFeatures.cs:176-188`, `P/Features/OrderBillReleasedHandler.cs`). Blocked if anything is paid; no partial credit |
| Cancel payment | Exists, correct | Reversal, bills reopened, cheque cancelled; refused once cleared (`PaymentFeatures.cs:142-166`). A dedicated bounce flow for issued cheques was not found (the register lists received cheques only, `ChequeFeatures.cs:81`; unverified whether `/bounce` accepts an issued id) |

**Defects**

| # | Sev | Location | Concrete failure |
|---|---|---|---|
| P1 | High | `StockPostingService.cs:262-269` | Warehouse fallback crosses legal entities: when entity B has no warehouse, an active warehouse of entity A is used, and the movement and journal are written to A (`:44,:70`). The bill later debits clearing in B. An invalid warehouse id also falls back silently *(static)* |
| P2 | High | `OrderFeatures.cs:363,372,382`; `StockPostingService.cs:32,38` | Receipt with Inventory off or no warehouse returns null with no journal and no error, but conversion still treats received product quantity as clearing. Dr 2050 with no credit stays on the balance sheet, no expense or asset recorded *(static)* |
| P3 | High | `OrderFeatures.cs:323,371-373,402-411`; `ReceiptFeatures.cs:126` | Bill quantity is `max(ordered, received)` and only one bill per PO. Billing a partly received or unreceived PO expenses the remainder to `purchases.expense`, labelled "(Pending)", and receipts are then refused forever. Stock never arrives in the books. Supplier part-invoicing is impossible |
| P4 | High | `OrderFeatures.cs:387-398`; `BillFeatures.cs:64` | Override below PO price gives a negative `priceDiff`; `BillInputValidator` requires `Price >= 0`, so a lower supplier price on received goods always returns 400. TASK-135 validates only the override itself *(static)* |
| P5 | Med | `BillFeatures.cs:161` | Non-recoverable tax is added to the line's account. On a clearing line, Dr 2050 exceeds the receipt credit by the tax, leaving a permanent residual instead of cost |
| P6 | Med | `StockPostingService.cs:172-178` | Reversal guard checks only total on-hand. 10@10, 10@20, issue 10 (COGS 150), delete first receipt: allowed. GL inventory 50, stock value 0, nothing posts the difference *(static)* |
| P7 | Med | `StockPostingService.cs:55` vs `F/Invoicing.cs:195` | Banker's rounding on receipts, away-from-zero on bills. One unit at 0.125: Cr 0.12, Dr 0.13. Per-receipt rounding adds further cents. No write-off for clearing residue |
| P8 | Med | `PurchasingTransitions.cs:13`; `OrderFeatures.cs:284-286` | `PUT /status` accepts `Received`/`PartiallyReceived` with no receipt. The order then refuses receipts (`ReceiptFeatures.cs:131`) and bills everything to expense |
| P9 | Low | `OrderFeatures.cs:349`; `S/.../OrderFeatures.cs:345` | Tax code chosen by rate only; `Kind` ignored |
| P10 | Low | Module domain models | `Currency = "QAR"` defaults on PO, quote, order and retail price list (`PurchasingModels.cs:102`, `SalesModels.cs:46,122`) breach the country-neutral rule |

---

#### 2. Order to Cash, step by step

| Step | State | Finding |
|---|---|---|
| Customer | Exists, partial | Party with role; `CustomerRules` enforced only at invoice. `CustomerPartyId` unchecked on quote and order (`S/Features/QuoteFeatures.cs:156`, `OrderFeatures.cs:174`) |
| Quotation | Exists, correct | Status map fine. `sales.defaultQuoteValidityDays` is dead (`S/SalesModule.cs:42`); `Sent -> Expired` is manual only |
| Quote to order | Exists, partial | Requires `Accepted`. Order date forced to today (`QuoteFeatures.cs:257`), two saves with no transaction (`:290-291`), always `Confirmed` (setting `autoConfirmOrdersFromQuotes` dead) |
| Sales order | Exists, partial | No date or decimal rules (`OrderFeatures.cs:114-128`): 5-decimal quantity is accepted, then fails at invoicing. Currency unchecked until conversion (`:330`) |
| Credit limit | Missing | No field on `Party` (`backend/src/ERP.Domain/Parties/Party.cs`), no check anywhere |
| Customer price lists | Missing in Sales | Order lines take free prices. `RetailPriceList` has no party link and no non-Retail consumer. Wholesale matrix unused by Sales |
| Stock reservation | Missing | No reservation or allocation entity; on-hand not checked at confirm or delivery |
| Delivery | Exists, wrong | No delivery document: only cumulative `DeliveredQuantity` on the line. Dated today, always (`:284`). No warehouse choice. No stock check (S1). Journal: Dr `cogs`, Cr `inventory` in one transaction |
| Invoice from order | Exists, partial | Draft invoice, full quantity only (`:357`), no partial invoicing. Undelivered lines are auto-issued while the invoice is still a draft (`:384-395`). Invoice date today (`:367`); PUT on the draft can change it |
| Invoice issue | Exists, correct | Dr AR with party, Cr income by account, Cr output tax. `Item.IncomeAccountId` ignored (role account unless line account given) |
| Double COGS | None | See "What holds up"; re-invoicing after a void does not re-issue (`DeliveredQuantity` stays) |
| Standalone invoice | Gap | Invoice with a product item from Finance moves no stock and posts no COGS (`F/Invoicing.cs:95`) |
| Receipt | Exists, correct | Dr bank, Cr AR with party; manual or oldest-due allocation (`F/SettlementService.cs:66`). Unlike payments, no check that the invoice predates the receipt |

**Reversals**

| Reversal | State | Finding |
|---|---|---|
| Cancel order | Wrong | `Processing -> Cancelled` allowed with delivered quantity; stock stays out, COGS stays posted, no revenue (`SalesTransitions.cs:25`, `OrderFeatures.cs:251`). `Delivered` cannot cancel. Quote not released (unlike PO/requisition), and stays `Converted` |
| Delete order | Wrong | Any status except `Invoiced` (`OrderFeatures.cs:232-238`), including delivered orders: orphaned movements and COGS. Quote then cannot be reconverted (`QuoteFeatures.cs:240-248`) |
| Reverse delivery | Missing | Lowering quantity is refused (`OrderFeatures.cs:273`); no reversal service call |
| Return from customer | Missing in Sales | `ReturnIssuedStockAsync` exists (`StockPostingService.cs:244`) but only POS calls it (`Pos/Features/ReturnFeatures.cs:280`). No RMA |
| Credit note | Exists, half | Line caps correct, linked by `CreditNoteForId`/`SourceLineId`, sign-reversed journal (`F/InvoiceFeatures.cs:210-238`). Reverses revenue and tax only: stock not returned, COGS not reversed |
| Void invoice | Exists, S2 | Clean reversal, order reopened. Stock stays out. Delivered order can be re-invoiced, not cancelled |
| Receipt cancel | Exists, S3 | Clean reversal, allocations restored, `InvoicePaymentChanged` published |
| Refund | Missing | No customer refund document in Finance; credit on a paid invoice can only be applied to other invoices |

**Defects**

| # | Sev | Location | Concrete failure |
|---|---|---|---|
| S1 | High | `StockPostingService.cs:101-145`; `I/Features/StockCosting.cs:24` | Service-level issue has no negative guard (the manual `/stock/issue` does, `I/Features/StockFeatures.cs:137`). Sales, trips, work orders can oversell. `Replay` clamps below zero and forgets the shortfall. Sell 5 at 0 on hand (Dr COGS 500 at `PurchasePrice`), receive 5@100: stock report says 5 worth 500, raw sum says 0 (`GetLowStock`, POS `GetOnHandAsync`), GL says 0. At zero purchase price, no journal at all *(static)* |
| S2 | High | `F/Invoicing.cs:117` | Void checks only `Status` and `Paid`. An invoice with an issued, unapplied credit note can be voided: AR +100, -100 credit note, -100 void, leaving a phantom -100. No test *(static)* |
| S3 | Med | `F/ReceiptFeatures.cs:111-121,124` | `VoidReceipt` ignores the instrument (payments do not, `PaymentFeatures.cs:147`): a cheque-register receipt can be voided with the cheque left in hand. `LoadAsync` has no direction filter, so a payment id loads here and an allocated one throws on `InvoiceId!.Value` |
| S4 | High | `OrderFeatures.cs:384-395` + `F/InvoiceFeatures.cs:148` | Convert then delete the draft invoice: order goes to `Delivered`, stock out and COGS posted, no revenue, and the order cannot be cancelled |
| S5 | Med | `OrderFeatures.cs:271,276` | Unknown `lineId` silently ignored; over-delivery silently clamped to ordered |
| S6 | Low | `S/Features/OrderFeatures.cs:109-110` | Fulfilment and conversion are not `IRetryOnConcurrency`, unlike PO equivalents |
| S7 | Low | `F/JournalFeatures.cs` | No guard found against manual journals on `inventory`, `ar.control`, `ap.control`, so subledgers can be bypassed *(static; grep only)* |
| S8 | Med | `F/ReceiptFeatures.cs:73-99`; `F/SettlementService.cs:47` | `CreateReceipt` has no transaction, and allocation publishes `InvoicePaymentChanged` before the journal is posted. The Services and Real Estate handlers save during that event (`RealEstate/Features/FinanceFeatures.cs:67-73`), persisting `invoice.Paid`. A later posting failure (locked period) leaves an invoice paid with no receipt or journal. Sales-order invoices are not affected unless a handler matches their source *(static)* |

---

#### 3. Inventory

| Capability | State | Finding |
|---|---|---|
| Warehouses | Exists, correct | Per legal entity, code unique, cannot deactivate holding stock. No bins or zones, no business-unit link |
| Valuation | Moving average only | Per item and warehouse, replayed in memory from all movements (`StockCosting.cs:14`). No FIFO, standard or per-item choice. Backdated receipts change history without revaluing posted COGS |
| Batch / lot / serial / expiry | Missing | No fields on `Item` or `StockMovement` |
| UoM conversion | Missing | `Item.Uom` and line `UnitOfMeasure` are free strings. A PO line in boxes posts box count as base quantity |
| Variants | Missing | No model |
| Barcode | Partial | `Item.Barcode`; lookup only in POS (`Pos/Features/TerminalFeatures.cs:87`), not in receipts, counts or transfers |
| Transfers | Exists, narrow | Immediate, same legal entity only (`StockFeatures.cs:182`), no document, no reversal, no journal (correct for same entity). No in-transit state, no inter-entity or branch flow, no approval |
| Counts | Exists, partial | Snapshot, post, one adjustment per difference, idempotent. Delta uses the current position, not the count date (`StockCountFeatures.cs:103-104`), so a backdated count misstates. Variances go to `cogs`, no shrinkage account (`:116`). Found stock with no cost enters at zero value with no journal (`:106,:113`). Number is `count + 1`, not the numbering service (`:55`), so a race hits the unique index |
| Negative guard | Partial | Manual issue, adjust and transfer are guarded including backdating (`StockFeatures.cs:137,159,184,225`); document-driven issues are not (S1) |
| Stock vs GL reconciliation | Missing | No report compares stock value with the `inventory` account. They drift through S1, P6, backdating and rounding (`Replay` uses unrounded value; journals round each issue). Single inventory account, no category mapping |
| Reports | Exists, correct | Valuation as-of, movement summary, slow-moving, reorder. On-hand sums across legal entities with no filter |
| Landed cost | Missing | Freight or duty cannot be added to stock cost |

---

#### 4. Distribution

| Capability | State | Finding |
|---|---|---|
| Routes and stops | Exists, loose | No `ILegalEntityGuard` on create (`D/Features/RouteFeatures.cs:124`); stop customer not validated (a test passes random GUIDs, `ChildRowEditTests.cs:32`) |
| Trip load | Exists, wrong | Creating a trip moves no stock (`D/Features/TripFeatures.cs:166-218`): no van location, stock stays sellable. Warehouse not validated (`:188`) although `IsWarehouseUsableAsync` exists. Trips start `InProgress`; `Scheduled` is never used; no cancel route |
| Van sale | Wrong | Sold quantity is a reconciliation number. No invoice, no customer, no per-stop sale |
| Trip completion | Wrong | `TotalSalesAmount` and `TotalCollectionsAmount` are typed in, unchecked against quantity x `UnitPrice` (`:247-248`), and post nothing. Empty `items` accepted. Dated today (`:246`) |
| Delivery / proof of delivery | Missing | No stop visit, signature, GPS or link to sales orders |
| Trip returns and damage | Partial | Returned means nothing happens (nothing left). Damage goes to COGS with sold (`:269`). Unaccounted quantity (loaded minus sold, returned, damaged) is not flagged |
| Ledger | Wrong | **Only** Dr `cogs`, Cr `inventory` for sold plus damaged (`:272`). No revenue, AR, cash or tax. A completed trip records cost with no income. Negative guard absent |
| Wholesale price matrix | Isolated | Fixed tier enum, item code and name taken from the request, no party link, `calculate-rate` unused by any order path |
| Settings | Dead | `autoCreateDeliveryOrders` (says trips become invoices) and `strictInventoryLoading` are read nowhere (`D/DistributionModule.cs:45-46`); `VehicleCapacityKg` unused |

---

#### 5. Classification

| Area | Feature | Class |
|---|---|---|
| P2P | Vendor master, party roles | generic and reusable |
| P2P | Requisition document | incomplete or wrong |
| P2P | Requisition and PO approval through engine | missing |
| P2P | Purchase order document and status map | partly reusable |
| P2P | Goods receipt | incomplete or wrong |
| P2P | GR/IR clearing journal | partly reusable |
| P2P | Bill document, post, approval, void | generic and reusable |
| P2P | PO to bill conversion | incomplete or wrong |
| P2P | 3-way match and tolerances | missing |
| P2P | Purchase price variance | incomplete or wrong |
| P2P | Payments, allocation, issued cheques | generic and reusable |
| P2P | Reverse receipt | partly reusable |
| P2P | Purchase return, debit note | missing |
| P2P | Landed cost, RFQ, blanket orders, payment runs | missing |
| O2C | Customer master | generic and reusable |
| O2C | Quotation | generic and reusable |
| O2C | Sales order document | partly reusable |
| O2C | Stock reservation | missing |
| O2C | Delivery note / shipment document | missing |
| O2C | Line-level fulfilment with COGS | incomplete or wrong |
| O2C | Invoice, issue, ledger | generic and reusable |
| O2C | Partial invoicing | missing |
| O2C | Credit note | partly reusable |
| O2C | Sales return with restock and COGS reversal | missing |
| O2C | Cancel and delete order | incomplete or wrong |
| O2C | Receipts, allocation, void | partly reusable |
| O2C | Customer refund | missing |
| O2C | Credit limit | missing |
| O2C | Customer price lists in Sales | missing |
| Inventory | Warehouses | generic and reusable |
| Inventory | Stock ledger, moving average | partly reusable |
| Inventory | Manual receive, issue, adjust | generic and reusable |
| Inventory | Service-level issue (document stock out) | incomplete or wrong |
| Inventory | Warehouse resolution | incomplete or wrong |
| Inventory | Transfers | partly reusable |
| Inventory | In-transit, inter-entity transfers | missing |
| Inventory | Stock counts | partly reusable |
| Inventory | Batch, lot, serial, expiry | missing |
| Inventory | UoM conversion, variants, bins | missing |
| Inventory | Barcode | partly reusable |
| Inventory | Stock vs GL reconciliation | missing |
| Inventory | FIFO, standard cost | missing |
| Distribution | Routes and stops | industry-specific |
| Distribution | Van sales trips | incomplete or wrong |
| Distribution | Trip ledger posting | incomplete or wrong |
| Distribution | Wholesale tier matrix | industry-specific |
| Distribution | Delivery proof, van stock, cash settlement | missing |

---

#### 6. Lifecycle API for the demo-data seeder

Envelope: `{ success, statusCode, message, data, errors, traceId }`; lists are paged with `filter=field:eq:value`. Module routes need `POST /api/v1/modules/{key}/enable` first. Core base `/api/v1`, module base `/api/v1/m/{key}`.

**Setup**

| Route | Command | Required body |
|---|---|---|
| `POST /legal-entities` | `CreateLegalEntity` | `name, countryCode, baseCurrency` (seen in tests; full input not checked) |
| `POST /parties` | `CreateParty` | `name, kind (Company/Individual), roles ("Customer", "Vendor", flags), paymentTermsDays?` |
| `POST /items` | `CreateItem` | `sku, name, type (product/service), uom, salesPrice?, purchasePrice?, taxCodeId?, barcode?` |
| `POST /finance/tax-codes` | | `code, name, kind (Vat, Gst, SalesTax, Withholding, Excise, Other), rate, isRecoverable?, isDefault?` |
| `GET /finance/accounts?pageSize=200` | | Find bank/cash ids and `systemRole` |
| `POST /m/inventory/warehouses` | `CreateWarehouse` | `code, name, legalEntityId, address?` |
| `POST /approval-policies`; `POST /approvals/{id}/approve` | | Only if demonstrating bill approval. Requester cannot decide: needs a second user |

**Purchasing, `/m/purchasing`**

| Route | Command | Required body |
|---|---|---|
| `POST /requisitions`, `PUT /{id}`, `DELETE /{id}` | `CreateRequisition` | `legalEntityId, requesterName, requisitionDate, lines[{itemId?, itemCode, description, quantity, unitOfMeasure, estimatedUnitPrice}]`, `number?, department?, requiredDate?, reason?` |
| `PUT /requisitions/{id}/status` | `ChangeRequisitionStatus` | `{status}`: draft, pendingApproval, approved, rejected |
| `POST /requisitions/{id}/convert-to-order` | `ConvertRequisitionToOrder` | `{vendorPartyId?, vendorName}`; needs approved |
| `POST /orders`, `PUT /{id}`, `DELETE /{id}` | `CreatePurchaseOrder` | `legalEntityId, vendorName, orderDate, currency (must equal base), vendorPartyId (needed to bill), lines[{itemId?, itemCode, description, quantity, unitOfMeasure, unitPrice, discountPercent, taxRate}]`, `number?, requisitionId?, vendorReference?, expectedDeliveryDate?, paymentTerms?, deliveryAddress?, notes?` |
| `PUT /orders/{id}/status` | `ChangePurchaseOrderStatus` | `{status}`: submitted, approved, cancelled. `billed` is refused |
| `POST /receipts`, `DELETE /{id}` | `CreateGoodsReceipt` | `legalEntityId, purchaseOrderId, receivedBy, receiptDate, lines[{purchaseOrderLineId, description, quantityReceived}]`, `number?, deliveryNoteNumber?, notes?`. Description is validated although the stored value comes from the PO line; no warehouse field exists |
| `POST /orders/{id}/convert-to-bill` | `ConvertPurchaseOrderToBill` | Empty, or `{unitPriceOverrides: {lineId: price}}`. Returns a draft bill; idempotent |

**Payables, `/finance`**

| Route | Command | Required body |
|---|---|---|
| `POST /bills`, `PUT /{id}`, `DELETE /{id}` | `CreateBill` | `legalEntityId, partyId, billDate, lines[{description, quantity, price, accountId, taxCodeId?, itemId?}]`, `vendorReference?, dueDate?, notes?, businessUnitId?` |
| `POST /bills/{id}/post` | `PostBill` | None. May return `pendingApproval`; after approval call again |
| `POST /bills/{id}/void` | `VoidBill` | `{reason}` 3-500 chars |
| `POST /payments` | `CreatePayment` | `legalEntityId, partyId, date, method (Cash, BankTransfer, Card, Instrument, Other), bankAccountId, amount, reference?, allocations?[{billId, amount}]`; for Instrument `chequeNo`, `chequeDate?, bankName?` |
| `POST /payments/{id}/allocate` | `AllocatePayment` | `{allocations[{billId, amount}]}` |
| `POST /payments/{id}/void` | `VoidPayment` | `{reason}` |
| `POST /finance/cheques/{id}/clear-issued` | `ClearIssuedCheque` | `{date}` |

**Sales, `/m/sales`**

| Route | Command | Required body |
|---|---|---|
| `POST /quotes`, `PUT /{id}`, `DELETE /{id}` | `CreateQuote` | `legalEntityId, customerName, quoteDate, currency, lines[{itemId?, itemCode, description, quantity, unitOfMeasure, unitPrice, discountPercent, taxRate}]`, `customerPartyId?, validUntil?, terms?, notes?` |
| `PUT /quotes/{id}/status` | `ChangeQuoteStatus` | `{status}`: sent, accepted, declined, expired, draft |
| `POST /quotes/{id}/convert-to-order` | `ConvertQuoteToOrder` | None; needs accepted |
| `POST /orders`, `PUT /{id}`, `DELETE /{id}` | `CreateOrder` | `legalEntityId, customerName, orderDate, currency, customerPartyId (needed to invoice), lines[...]` as quote; `quoteId?, expectedDeliveryDate?, paymentTerms?, shippingAddress?` |
| `PUT /orders/{id}/status` | `ChangeOrderStatus` | `{status}`: confirmed, processing, delivered, cancelled. `invoiced` refused |
| `PUT /orders/{id}/fulfillment` | `UpdateOrderFulfillment` | `{lines[{lineId, deliveredQuantity}]}`: **cumulative**, not delta |
| `POST /orders/{id}/convert-to-invoice` | `ConvertOrderToInvoice` | None; returns a draft invoice |

**Receivables, `/finance`**

| Route | Command | Required body |
|---|---|---|
| `POST /invoices`, `PUT /{id}`, `DELETE /{id}` | `CreateInvoice` | `legalEntityId, partyId, invoiceDate, lines[{description, quantity, price, taxCodeId?, accountId?, itemId?}]`, `dueDate?, isCreditNote?, notes?` |
| `POST /invoices/{id}/issue` | `IssueInvoice` | None |
| `POST /invoices/{id}/void` | `VoidInvoice` | `{reason}` |
| `POST /invoices/{id}/credit-note` | `CreateCreditNoteFromInvoice` | `{date?, lines?[{invoiceLineId, quantity}], notes?}`; returns draft, then `/issue` |
| `POST /invoices/{creditNoteId}/apply-credit` | `ApplyCreditNote` | `{invoiceId, amount}` |
| `POST /receipts` | `CreateReceipt` | `legalEntityId, partyId, date, method (not Instrument), bankAccountId, amount, reference?, allocations?[{invoiceId, amount}], autoAllocate?` |
| `POST /receipts/{id}/allocate`, `/void` | `AllocateReceipt`, `VoidReceipt` | `{allocations}`; `{reason}` |

**Inventory, `/m/inventory`**

| Route | Command | Required body |
|---|---|---|
| `POST /stock/receive` | `ReceiveStock` | `warehouseId, itemId, quantity, unitCost, date, offsetAccountId, reference?, memo?` (use for opening stock) |
| `POST /stock/issue` | `IssueStock` | `warehouseId, itemId, quantity, date, expenseAccountId?, reference?, memo?` |
| `POST /stock/adjust` | `AdjustStock` | `warehouseId, itemId, quantityDelta, date, unitCost?, offsetAccountId?` |
| `POST /stock/transfer` | `TransferStock` | `fromWarehouseId, toWarehouseId, itemId, quantity, date` |
| `PUT /stock/policy` | `SetStockPolicy` | `itemId, reorderLevel, reorderQuantity` |
| `POST /counts`, `PUT /{id}/lines`, `POST /{id}/post`, `POST /{id}/cancel` | `CreateStockCount` etc. | `warehouseId, countDate, memo?`; `{lines[{itemId, countedQuantity}]}` |

**Distribution, `/m/distribution`**

| Route | Command | Required body |
|---|---|---|
| `POST /routes`, `PUT /{id}`, `DELETE /{id}` | `CreateRouteCommand` | `legalEntityId, code, name, isActive, stops?[{customerPartyId, customerName, sequenceOrder}]` |
| `POST /trips` | `CreateTripCommand` | `legalEntityId, routeId, driverUserId, driverName, vehicleNumber, warehouseId, departureDate, items[{itemId, itemCode, itemName, uom, loadedQuantity, unitPrice}]` |
| `POST /trips/{id}/complete` | `CompleteTripCommand` | `totalSalesAmount, totalCollectionsAmount, items[{itemId, soldQuantity, returnedQuantity, damagedQuantity}], notes?` |
| `POST /wholesale`, `PUT /{id}`, `DELETE /{id}` | `CreateWholesalePriceCommand` | `legalEntityId, itemId, itemCode, itemName, tier (standard..platinum), minQuantity, wholesaleUnitPrice, isActive, discountPercentage` |
| `POST /wholesale/calculate-rate` | `CalculateWholesaleRateQuery` | `legalEntityId, itemId, tier, quantity` (a read over POST) |

**Contract oddities that will affect the seeder**

1. **Status casing.** Responses are camelCase (`partiallyReceived`, `inProgress`, `issued`; `Program.cs:167`). Requests and list filters accept any case (`Paging.cs:229`), and integers also bind. Compare case-insensitively.
2. **Dates that cannot be backdated.** These use `DateTime.UtcNow` directly, not `IClock`, so history cannot be produced by the API or by a clock override:
   - Delivery stock issue and COGS (`S/Features/OrderFeatures.cs:284,391`): **every delivery and all historical COGS land on the run date.** Biggest obstacle for TASK-139.
   - Trip completion (`TripFeatures.cs:246`).
   - Fixable by a follow-up PUT while editable: order date from quote (`QuoteFeatures.cs:257`), PO date from requisition (`RequisitionFeatures.cs:218`), invoice date (`OrderFeatures.cs:367`), bill date (`P/.../OrderFeatures.cs:415`).
   - Void reversals are dated today or later (`Posting.cs:79-83`), so voided history shows reversals on the run date.
3. **Date windows.** Document dates must be after 2000-01-01 and at most one year ahead. Stock commands allow plus or minus 10 years. Locked books reject posting.
4. **Bill PUT loses items.** `BillLineDto` has no `itemId` (`BillFeatures.cs:20`); a seeder re-sending lines to redate must keep them itself. `accountId` is on the DTO.
5. **Convert-to-bill body** is read only when `ContentLength > 0` (`PurchasingEndpoints.cs:42`); chunked requests lose overrides silently *(static)*.
6. **Requisition conversion** gives a `QAR` draft PO on any entity; PUT it with the base currency before approving, and supply tax and discount there, since conversion carries neither.
7. **Order of operations.** Receive stock before delivering: oversell succeeds silently and corrupts valuation (S1). Receive before billing: receipts are refused afterwards (P3). Keep only one warehouse per legal entity unless it is acceptable that all document stock uses the first found.
8. **Avoid until fixed:** override below PO price (400, P4); `PUT status=received` (P8); voiding an invoice with an unapplied credit note (S2); deleting delivered orders.
9. **Vendor reference** is unique per vendor among non-voided bills; `convert-to-bill` uses `order.VendorReference ?? order.Number`, so two POs sharing a reference fail with 409.
10. **Distribution custom fields** are raw JSON strings, not objects, and bypass the `cf_` rule (`RouteFeatures.cs:138`, `TripFeatures.cs:194`). The path id overrides `tripId`/`id` in the body; tests still send them.
11. **`itemCode` must be present** on requisition, PO, quote and order lines (empty string is fine): no validator rule, but handlers call `.Trim()` on it, so null gives a 500 *(static)*. Booleans such as route `isActive` default to false when omitted.
12. **Reachable states.** Trip `Scheduled` and `Cancelled` have no route. PO short-close does not exist. No delivery, return or debit-note documents exist to seed, so TASK-139 cannot show them.

**Test coverage gaps on these flows** *(from test names and grep only)*: price variance, receipt with Inventory off, cross-entity warehouse fallback, oversell through sales, cancel or delete after delivery, void with open credit note, and any ledger assertion on trips (`DistributionTripTests.cs` checks status and quantities only).

---

# Appendix C

### Audit section: production, retail/POS, projects, services, CRM

Method: read the handlers named below (code at the worktree HEAD, 2c631b6). Nothing was run: no server, no database, no tests. Anything marked **(unverified)** was inferred, not traced. Paths are relative to `backend/modules/ERP.Modules.<X>/` unless stated. Line numbers are in the files as read.

Classification key (exactly one per feature): **G** generic and reusable, **I** industry-specific, **P** partly reusable, **M** missing, **X** incomplete or wrong.

Weighing the earlier notes: most defects in `docs/PROJECTS-REVIEW-2026-11-05.md` (void does not release work, no cost rate, free-text people, no expenses, no approval, no preview/grouping/discount/percent billing) are now fixed in code (`BillingFeatures.cs:368-394`, `TimesheetFeatures.cs:268-274`, `ProjectExtras.cs`). Most items in the POS gap note (returns, held sales, drawer and variance journals, on-account via real invoice, barcode lookup, shop time zone in reports) are also fixed (`ReturnFeatures.cs`, `SessionFeatures.cs:270-282,316-330`, `OrderFeatures.cs:582-594`). Both documents are stale on those points. Two POS items in the note still hold: promotions/price lists are not applied at the till, and gift card/loyalty do not exist.

---

#### 1. Plan to Produce (Manufacturing)

Scope of the module: BOM, work order (with free-text operations), cost roll-up and variance. Nothing else. Tables: `mfg_Boms`, `mfg_BomComponents`, `mfg_WorkOrders`, `mfg_WorkOrderOperations`, `mfg_ProductCostings` (the last is never written, see 1.3).

##### 1.1 Step-by-step status

| Step | Status | Evidence |
|---|---|---|
| Production plan / master schedule | Missing | No entity, handler or route. |
| MRP / material requirements | Missing | No netting of demand, stock and open orders; no requirement query. Work order create only totals BOM cost (`WorkOrderFeatures.cs:168-171`). |
| BOM single level | Exists, correct (with caveats below) | `BomFeatures.cs:115-162`. Components must be `ItemType.Product` (`:108`), no duplicates and no self-component (`:63-64`). |
| BOM multi-level (sub-assemblies) | Missing | A component is just another product; no explosion, no cost roll-up through children, no cycle check beyond direct self (`:171`; A uses B uses A is accepted). Sub-assembly must be produced by its own work order first. |
| BOM versions | Exists but wrong | `Version` is a free string (`:131,176`), no effective dates, no supersede rule. `Code` must be unique org-wide (`:119`), so a new version needs a new code. `UpdateBom` overwrites components in place (`:183-202`), so history and the cost basis of existing work orders change silently. |
| BOM component cost | Exists but wrong | `UnitCost` is typed by the caller (`:153`), not read from the item or stock average, so standard cost goes stale. |
| Work centres | Missing as master data | Only `WorkCenterName` free text on each operation (`ManufacturingModels.cs` `WorkOrderOperation`). No capacity, calendar or rate card. |
| Routing | Missing | Operations are typed per work order (`CreateWorkOrderCommand.Operations`); the BOM carries none. |
| Work order create | Exists, correct | `WorkOrderFeatures.cs:153-218`. Status set directly to Planned (`:189`); `Draft` is never used. Estimate = BOM material (with scrap %) + labour + overhead per unit, but ignores operation hours. |
| Work order release | Exists but wrong (hollow) | `:220-232` only flips Planned to InProgress. No reservation of materials, no availability check, no material issue. |
| Material issue | Exists but wrong | There is no issue step. Components are back-flushed only at completion (`:280-303`). Qty = BOM qty x completed/output (`:282-287`); actual consumption cannot be entered, extra or short issues are impossible. |
| Component scrap/wastage | Exists but wrong | BOM `ScrapPercentage` is added to the estimate (`:169`) but ignored when issuing stock (`:287`, no scrap factor) and when costing. `ScrappedQuantity` is stored (`:263`) but nothing is issued, costed or posted for it. |
| Insufficient stock | Exists but wrong | `IssueStockAsync` does not reject negative stock (`Inventory/Features/StockPostingService.cs:90-146`) and manufacturing does not call `GetOnHandAsync`. Completion succeeds against empty shelves. |
| Production (progress/partial) | Missing | Single-shot completion only (`:234-364`); no partial output, no per-operation progress entry. `CompletedQuantity` has no upper bound (`CompleteWorkOrderValidator :107-115`). Operation `ActualHours` has no way to be set (no endpoint), so conversion cost always uses planned hours (`:275`). |
| Quality check / inspection | Missing | No entity, hold, or reject flow. |
| Finished-goods receipt | Exists, correct | `ReceiveStockAsync` at actual unit cost (`:308-321`) into the work order warehouse. |
| Costing (actual) | Exists but wrong | Material at average cost (`:288-290`) plus conversion; the caller-supplied `ActualTotalCost` (`CompleteWorkOrderCommand` line 88) is validated but then ignored (recomputed `:305-306`). Stock service books the issue at its own average cost (`StockPostingService.cs:107`), which can differ from the cost manufacturing used for the journal (`:289`, falls back to BOM cost). Ledger and stock valuation can drift. |
| Standard cost roll-up and variance | Partly | Roll-up `CostingFeatures.cs:58-94` correct for one level. Variance `:96-144` compares the current active BOM to the average of all completed orders, not per work order, and has no price/usage/efficiency split. |
| Journals | Exists but wrong | One entry: Dr inventory (finished goods) total; Cr inventory (raw materials); Cr WIP "absorption" for conversion (`:336-358`). Raw and finished goods share the `inventory` role account. WIP is only ever credited: labour and overhead are never debited to it from payroll or expense, so it builds an unexplained credit. If the Inventory module is off, `IssueStockAsync` returns null (`StockPostingService.cs:93`) but the journal still posts (`:351`), giving ledger entries with no stock. |
| Cancellation | Exists, correct but narrow | `:366-380`. Allowed from Draft/Planned/InProgress; refused if `CompletedQuantity > 0`. Nothing to reverse because nothing is issued before completion. |
| Reversal of a completed order | Missing | `Completed` has no transitions (`ManufacturingTransitions.cs:14`); `ReverseIssueAsync`/`ReverseReceiptAsync` exist in the stock service but manufacturing never calls them. A wrong completion cannot be undone. |
| Subcontract / by-products / co-products | Missing | Not modelled. |

##### 1.2 Feature classification

| Feature | Class |
|---|---|
| BOM single level and cost roll-up | G |
| Work order with free-text operations | G |
| Multi-level BOM / explosion / MRP / production plan | M |
| BOM versioning | X |
| Work centres, routing, capacity | M |
| Material issue, scrap/wastage, partial output | X |
| Quality inspection | M |
| WIP and variance accounting | X |
| Completion reversal | M |
| Cost-record table `ProductCostingRecord` | X (dead table) |

##### 1.3 Dead code

`ProductCostingRecord` (`ManufacturingModels.cs:100`) is mapped in `ManufacturingModule.cs:147` but nothing reads or writes it; `GetProductCostVariance` computes live. A seeder should not write it.

##### 1.4 Routes and required fields (base `/api/v1/m/manufacturing`)

| Verb and route | Permission | Body / required fields |
|---|---|---|
| GET `/boms`, `/boms/{id}` | `manufacturing.boms.read` | paged list / by id |
| POST `/boms` | `manufacturing.boms.manage` | `legalEntityId, code (unique), name, producedItemId (Product), producedItemCode, producedItemName, outputQuantity>0, uom, version, laborCostPerUnit, overheadCostPerUnit, isActive, description?, components[{componentItemId (Product), componentItemCode, componentItemName, quantityRequired>0 (4 dp), uom, scrapPercentage 0-100, unitCost>=0, notes?}], customFields?`. Code and name strings from the request are overwritten by the catalog (`:127-128,148-149`). |
| PUT `/boms/{id}` | manage | same minus `legalEntityId, code, producedItem*` (components replaced wholesale) |
| DELETE `/boms/{id}` | manage | refused if any work order uses it |
| POST `/workorders` | `manufacturing.workorders.manage` | `legalEntityId, bomId (active, same entity), plannedQuantity>0, warehouseId (active, same entity), startDate, dueDate>=startDate, notes?, operations[{sequenceOrder, operationName, workCenterName, plannedHours, hourlyRate}]?, customFields?`. Number from sequence `WorkOrder` (module-provided, `ManufacturingModule.cs:66`; the core seeder key `mfg_workorder` in `ERP.Api/ProgramSeeder.cs:187` is not the one used). |
| POST `/workorders/{id}/release` | manage | none. Planned to InProgress. |
| POST `/workorders/{id}/complete` | manage | `completedQuantity>=0, scrappedQuantity, actualTotalCost (ignored but must be >=0), notes?`. Component items must exist and be Product. Completion date = today (not settable). |
| POST `/workorders/{id}/cancel` | manage | `{ reason? }` |
| GET `/costing/rollup/{bomId}`, `/costing/variance?legalEntityId&itemId` | `manufacturing.costing.read` | query only |
| GET `/kpis` | `manufacturing.analytics.read` | none |

Seeder cautions: no back-dating (completion date is "now"); stock for components must exist or the books go negative with no error; complete only once per order; one BOM code per version.

---

#### 2. Retail and POS

Tables: `pos_Registers, pos_Sessions, pos_DrawerTransactions, pos_Orders, pos_OrderLines, pos_Payments, pos_Returns, pos_ReturnLines, pos_ReturnRefunds`; `ret_Stores, ret_PriceLists, ret_PriceListItems, ret_Promotions`. Store-to-store stock movement lives in Inventory, not Retail.

##### 2.1 Step status

| Step | Status | Evidence |
|---|---|---|
| Barcode | Exists, correct (lookup only) | `Item.Barcode` (`src/ERP.Domain/Catalog/Item.cs:17`); exact scan by barcode then SKU `Pos/Features/TerminalFeatures.cs:85-89`. One barcode per item, no pack/unit barcodes, no weight-embedded codes. |
| Variants (size/colour/style matrix) | Missing | No variant or attribute model anywhere in Domain, Retail or Pos (searched). Each variant must be a separate item. |
| Cash register / shift | Exists, correct | Open `SessionFeatures.cs:181-240` (one open per register enforced by unique index), close with variance journal `:245-284`, pay-in/out and float/drop `:286-347`. |
| Shift date handling | Exists but wrong | Open, close and drawer journals use UTC "today" (`:206,277,326`); orders `OrderFeatures.cs:534`; only reports use the workspace zone (`PosLedger.cs:99-129`). Late-evening sales in a +3 zone land on the previous ledger day. |
| Sale pricing | Exists, correct | Server prices (`OrderFeatures.cs:289-396`): catalog price, override and free-text need permission, inactive items refused, tax from item tax code. |
| Discounts | Partly | Manual line % with global and cashier limits (`:291-318`). No order-level, coupon, reason code or approval workflow. |
| Promotions | Missing at till | Retail promotions are never read by POS (no reference to `Retail` in `Pos/*.cs`). `RetailPromotion.TimesUsed` is never incremented anywhere, so `UsageLimit` never bites (`PromotionFeatures.cs:182`). |
| Price lists | Missing at till | No price-resolution code exists (`PriceListFeatures.cs` is CRUD only); POS uses `Item.SalesPrice`. |
| Loyalty | Missing | No points, tiers or customer wallet. |
| Gift cards | Missing | `PosPaymentMethod.GiftCard` is a tender label only; no card issue, balance, redemption or liability account. It posts to the "other clearing" account (`PosLedger.cs:58-69`). |
| Tendering and split payments | Exists, correct | `OrderFeatures.cs:446-513`; exact total, change from cash, on-account via a real issued invoice (`:582-594`). |
| Held sales | Exists, correct | Park/resume/discard `:646-690`. |
| Returns | Exists, mostly correct | `ReturnFeatures.cs:125-283`: per-line quantity caps, proportional net/tax with last-unit rounding (`:151-157`), credit note for on-account part, restock reverses cost exactly (`:279-280`). |
| Return refund method | Exists but wrong | Only the total non-account refund is checked against the total paid (`:189-192`); the method is free. A card-paid sale can be refunded in cash (and cash refunds hit the drawer, `:195-201`). No return window or receipt-less policy. |
| Damaged returns | Exists but wrong | `Restock=false` returns money and tax but posts no write-off or damaged-stock entry (`:279` skips, nothing else); cost of sales stays booked on goods that are gone. |
| Exchanges | Missing | Must be a return plus a new sale, with no link and no net-settlement. |
| Void | Exists, correct | `OrderFeatures.cs:692-764`: same open shift only, blocked after any return, voids the invoice, reverses stock and journal. |
| Journals | Exists, correct | Sale (`:566-607`): Dr tenders, Cr income per item (item income account else sales/services role), Cr tax output, on-account leg through invoice. COGS and inventory from the stock service when the sale issues stock (`:610-623`). Return and variance journals confirmed above. |
| Stock effect | Exists, correct but optional | Issue per line to the register warehouse (`:614-621`). Default `pos.allowNegativeStock = true` (`PosModule.cs`), so oversell is allowed by default. `EnsureStockAsync` iterates the on-hand result (`:636`), so an item missing from that result escapes the check (unverified whether tracked items with no movements are returned). |
| Idempotent retry | Unverified | Core has `Common/Idempotency.cs` and middleware; not traced whether the POS sale route is covered. |
| Store master | Partly | `RetailStore` has warehouse, manager, contact. No link from `PosRegister` to a store (register has only `WarehouseId`); no store-level price list, calendar or targets. |
| Store transfers | Partly (Inventory) | `POST /api/v1/m/inventory/stock/transfer` (`InventoryModule.cs:135`, `StockFeatures.cs:177-189`): instant two-leg movement, same legal entity only. No request, dispatch, in-transit, receive or discrepancy step; no document number. |
| Currency leakage | Exists but wrong | Price list currency defaults to `"QAR"` (`Retail/Domain/RetailModels.cs:58`) and the promo error message names QAR (`PromotionFeatures.cs:189`). Also in Sales/Purchasing defaults. Breaks the country-neutral rule. |
| Promo validation | Exists but wrong | `ValidatePromoCode` returns valid with a discount of 0 for BuyXGetY and Bundle (`PromotionFeatures.cs:192-201`); promotions have no item or category scope, no stacking rule (`RetailModels.cs:98-129`). |

##### 2.2 Classification

| Feature | Class |
|---|---|
| Register, shift, drawer, variance journal | G |
| Counter sale, tender, held sale, void, returns | G |
| Barcode scan lookup | G |
| Variants / size-colour matrix | M (retail-specific) |
| Promotions, price lists, store master | P (data exists, not applied or enforced) |
| Loyalty, gift cards, exchanges | M |
| Store transfer workflow | P |
| Return refund routing, damaged-goods write-off | X |

##### 2.3 Routes and required fields

POS, base `/api/v1/m/pos` (permissions in parentheses).

| Verb and route | Body / required fields |
|---|---|
| POST `/registers` (`pos.registers.manage`) | `legalEntityId, code, name, warehouseId?, defaultCashAccountId?, isActive, receiptHeader?, receiptFooter?, customFields?, cardAccountId?, varianceAccountId?, expenseAccountId?`. PUT `/registers/{id}` replaces the record. |
| POST `/sessions/open` (`pos.sessions.manage`) | `legalEntityId, registerId, openingCash>=0, cashierUserId? (ignored), cashierName? (ignored), notes?`. Requires a signed-in user; cashier is the caller. One open session per register. |
| POST `/sessions/{id}/drawer-transaction` | `type (FloatIn 0, PayIn 1, PayOut 2, DropOut 3), amount>0, reason, accountId? (needs registers.manage)`. Pay-in/out always journal. |
| POST `/sessions/{id}/close` | `actualCash>=0, notes?`. Posts variance journal if not zero. |
| POST `/orders` (`pos.orders.create`) | `legalEntityId, registerId, sessionId, customerPartyId?, customerName?, lines[{itemId or (itemCode + unitPrice), quantity>0, unitPrice?, discountPercent, taxRate?}], payments[{method (Cash 0, Card 1, GiftCard 2, Account 3, Other 4), amount>0, tenderedAmount, changeAmount, reference?}], notes?, parkedOrderId?`. Payments must sum exactly to the total unless `pos.allowPartialPayment`; Account tender needs `customerPartyId` (a Customer-role party) and `pos.orders.on-account`. Item needs a sales price. Order date is now (no back-dating). |
| POST `/orders/park`, DELETE `/orders/{id}/park` | `sessionId, customerPartyId?, customerName?, lines[], notes?` |
| POST `/orders/{id}/void` (`pos.orders.void`) | `{ reason }` (required) |
| POST `/returns` (`pos.orders.refund`) | `orderId, sessionId (open shift), reason, lines[{orderLineId, quantity, restock}], refunds[{method, amount, reference?}]`; refunds must equal the computed return total. Account refund only if the sale left an invoice. |
| GET `/terminal`, `/terminal/session`, `/terminal/catalog`, `/terminal/customers`, `/terminal/held`; `/sessions/current`, `/sessions/{id}/report`, `/reports/sales`, `/kpis` | read |

Number sequences `POS`, `POSR` come from the module (`PosModule.cs:80-81`); the core seeder keys `pos_order` etc. are not used by the handlers. Required system accounts: roles `income.sales`, `tax.output`-style `SystemRoles.TaxOutput`, `cash`/`pos.cash`, `bank`/`pos.card-clearing`, `cogs`, `inventory`, and a variance account (falls back to purchases expense).

Retail, base `/api/v1/m/retail` (CRUD plus delete, permissions `retail.<stores|pricelists|promotions>.read|manage`; names assumed from the pattern, verify in `Retail/Permissions.cs`).

| Route | Body |
|---|---|
| POST `/stores` | `legalEntityId, code, name, warehouseId?, storeManagerName?, phone?, email?, address?, isActive, customFields?` |
| POST `/pricelists` | `legalEntityId, code, name, type (Standard 0, Promotional 1, Tiered 2, Member 3), currency (set it; default QAR), validFrom?, validTo?, isDefault, isActive, description?, customFields?, items[{itemId?, itemCode, itemName, basePrice, price, minQuantity?}]` |
| POST `/promotions` | `legalEntityId, code, name, type (PercentageDiscount 0, FixedAmountDiscount 1, BuyXGetY 2, Bundle 3), discountValue>0, minimumOrderAmount?, startDate, endDate>=startDate, usageLimit?, isActive, description?, customFields?` |
| POST `/promotions/validate` | `promoCode, orderTotal, legalEntityId` (field names from query record, verify) |
| (Inventory) POST `/api/v1/m/inventory/stock/transfer` | `fromWarehouseId, toWarehouseId, itemId, quantity, date, reference?, memo?` (date is settable here) |

---

#### 3. Projects, with fitness for construction and engineering

Tables: `prj_Projects, prj_Milestones, prj_Tasks, prj_Timesheets, prj_Rates, prj_Expenses, prj_TaskDependencies, prj_Comments, prj_ChangeOrders, prj_Risks, prj_Members, prj_Allocations, prj_Acceptances, prj_BudgetLines, prj_ProgressBillings`. Design target: professional services (hours, rate cards, time-and-materials, fixed fee).

##### 3.1 Step status

| Step | Status | Evidence |
|---|---|---|
| Project, status, clone, template, archive | Exists, correct | `ProjectsTransitions.cs:8-17`, `PlanningFeatures.cs`. |
| Budget | Exists but wrong (conflated) | `Project.BudgetAmount` is both the cost budget (health, `ProjectFiguresService.cs:55`) and the fixed contract fee (`BillingFeatures.cs:178,345`). Margin on a fixed-fee project cannot be derived separately. Budget lines are free-text categories (`ProjectBudgetLine.Category`) without cost codes or a hierarchy. |
| Tasks, dependencies, baseline, critical path | Exists, correct | `TaskFeatures.cs`, `PlanningFeatures.cs:31+` (calendar days). |
| Timesheets | Exists, correct | Rate cards, cost rate, approval setting, per-day limit, write-off (`TimesheetFeatures.cs:260-330`). |
| Expenses | Partly | Free text category and amount, optional mark-up, approval (`ExpenseFeatures.cs`). No supplier, no purchase order or bill link, no quantity or unit, no commitment tracking. Cannot receive a supplier invoice against a project. |
| Milestones and acceptance | Exists, correct | Fee-cap check on milestone amounts (`MilestoneFeatures.cs:156-159`), customer-portal signature (`ProjectPortalFeatures.cs`). |
| Billing: T&M, milestone, percent of fee, expenses, discount | Exists, correct | `BillingFeatures.cs:261-299, 316-364`; void releases work (`:368-394`). |
| Progress billing | Exists but not construction-grade | One flat percent of the fee per invoice, capped to the fee (`:343-353`). No certified cumulative valuation per item, no previous-certified column, no retention held or released, no advance recovery. |
| Retention | Missing | No code or field (searched "retention"). |
| Costing to ledger (WIP) | Exists but wrong | See 3.2 defects 1 and 2. |
| Revenue recognition | Missing | Revenue = invoice at billing. `EarnedRevenue` exists only as a report column (`ProjectReports.cs:15,186`); nothing posts it. |
| Profitability | Exists, correct as a report | `ProjectReports.cs`, KPIs. Cost covers labour and expenses only. |
| BOQ / measured quantities | Missing | No entity. |
| Subcontractor / contractor records | Missing | No subcontract order, certification, back-to-back billing. Party roles are not used by the module (**unverified** beyond a grep for "subcontract"/"contractor": only a comment in `ProjectExtras.cs:21`). |
| Material consumption | Missing | No stock issue to a project; no link to Inventory or Purchasing from the module. Materials only enter as a typed expense. |
| Equipment / plant hire and utilisation | Missing | None. |
| Variations / change orders | Exists, correct | Approve changes the effective fee and hours (`ProjectExtrasFeatures.cs:47+`). Reusable for construction variations. |
| Resource allocation, utilisation | Exists, correct | `ProjectAllocation`, utilisation report. |
| Site records (daily reports, RFIs, submittals, snag lists, safety) | Missing | None. |

##### 3.2 Defects (concrete)

1. **WIP never released on fixed-fee and non-billable projects.** `PostProjectCosts` moves approved time and expense cost to WIP for any non-template project (`ProjectLedgerFeatures.cs:30-31`, no billing-type check). Release to cost of sales happens only for the time/expense lines that go on an invoice (`BillingFeatures.cs:283-290`), but fixed-fee projects bill no time (`:332`, "milestones only") and milestones carry no cost lines, so labour cost stays in WIP indefinitely; non-billable projects can never be invoiced (`:321-322`). Result: WIP asset overstated, cost of sales understated, margin overstated. Only the explicit write-off (`:196-219`) clears it, and only for billable-flagged, unbilled rows.
2. **No completion step for WIP.** `ChangeProjectStatus` to Completed or Cancelled does not release or write off the remaining WIP (`ProjectsTransitions.cs`, no hook; `ProjectWip` is referenced only from billing and timesheet write-off).
3. **Cost posting is a manual button** (`POST /billing/post-costs`) and dated today (`ProjectLedgerFeatures.cs:36`), not the date of the work. The book date moves with whoever clicks it.
4. **Percent billing is not cumulative certification.** Each invoice bills `fee x pct` (`:349`) with no retention and no check against measured progress or task percent complete.
5. **Single tax code for the entire invoice** (`BillingFeatures.cs:265-266`), and invoice date is UTC today (`:273`).
6. **Fixed-fee and T&M cannot mix expenses fairly:** expenses bill on fixed-fee projects regardless of the fee cap (`:338`, only milestone and percent are capped).

##### 3.3 Fitness for construction and engineering

Not fit as shipped. It is a time-and-materials/fixed-fee professional-services module. A contractor needs, and the module lacks: BOQ and measured quantities, cost codes, subcontracts and certificates, retention, certified interim valuations, material issue from stock, equipment cost, site documents, revenue recognition by percent complete. What can be reused: project master, tasks and critical path, change orders (as variations), milestones with customer sign-off, timesheets and rate cards, expense approval, invoice-from-project, portal, risks, allocation. Estimated reuse: roughly the project shell and billing plumbing; the construction cost and valuation layer is a new build.

##### 3.4 Classification

| Feature | Class |
|---|---|
| Project shell, tasks, schedule, timesheets, rate cards, expenses, approvals | G |
| Change orders | P (usable for variations) |
| Milestones, acceptance, portal | G |
| T&M and fixed-fee billing, void release | G |
| WIP posting and release | X |
| Revenue recognition by percent complete | M |
| Progress billing, retention | M (percent-of-fee exists, X as a construction feature) |
| BOQ, subcontractor, material consumption, equipment, site records | M (industry-specific) |
| Project budget as cost vs contract value | X |

##### 3.5 Routes and required fields (base `/api/v1/m/projects`; body types in `ProjectsEndpoints.cs`)

| Verb and route | Permission | Body / required fields |
|---|---|---|
| POST `/projects` | `projects.projects.manage` | `legalEntityId, number? (sequence "project"), name, description?, customerId?, customerName?, managerName?, status, billingType (TimeAndMaterials 0, FixedFee 1, NonBillable 2), budgetAmount, startDate?, endDate?, customFields?, managerUserId?, taxCodeId?, budgetHours?, sourceType?, sourceId?, isTemplate?`. A customer is required later to bill (`BillingFeatures.cs:325`). |
| PUT `/projects/{id}/status` | manage | `{ status }`; Planning to Active to OnHold/Completed/Cancelled; nothing can be added once Completed or Cancelled. |
| POST `/projects/{id}/clone`, `/archive`, `/baseline` | manage | `CloneInput{name, startDate?, customerId?, includeTeam, asTemplate}` etc. |
| POST `/projects/{id}/members` | `team.manage` | `{ userId, role? }` |
| POST `/budget-lines` | manage | `projectId, category, amount>=0, hours` |
| POST `/tasks`; PUT `/tasks/{id}/status`, `/dependencies` | tasks.manage | `projectId, milestoneId?, title, description?, status, priority, assignedTo?, estimatedHours, actualHours, dueDate?, assignedUserId?, parentTaskId?, startDate?, labels?` |
| POST `/milestones`; POST `/milestones/{id}/achieve` | milestones.manage | `projectId, title, dueDate, amount, status?, achievedDate?`; achieve `{ achievedDate }`. Billing needs status Achieved. |
| POST `/timesheets` | timesheets.manage | `projectId, taskId?, date, workerName?, hours, hourlyRate?, description?, isBillable, workerUserId?, costRate?`. Rates default from rate cards. If `projects.timesheetApproval` is on, entries start Draft: POST `/timesheets/submit {ids}` then `/timesheets/decide {ids, approve, reason?}` (approver must differ from worker unless `projects.allowSelfApproval`). Bulk: POST `/timesheets/week`. |
| POST `/expenses` | expenses.manage | `projectId, date, category, description, amount, isBillable, markupPercent, workerUserId?, workerName?`; approval flow like timesheets (`/expenses/submit`, `/expenses/decide`). |
| POST `/rates` | rates.manage | `projectId?, userId? or role?, billRate, costRate` |
| POST `/change-orders`; POST `/change-orders/{id}/decide` | change-orders.manage | `projectId, title, description?, amountChange, hoursChange`; decide `{ approve, note? }` |
| POST `/billing/post-costs` | billing.manage | `{ projectId }` (moves approved cost to WIP) |
| POST `/billing/preview`, `/billing/bill` | billing.read / manage | `projectId, timesheetIds?, milestoneIds?, expenseIds?, grouping (detailed/person/task/project), discountPercent, progressPercent? (fixed fee only), notes?, dueDate?`. Creates the invoice dated today. |
| POST `/billing/write-off`, `/timesheets/write-off`, `/expenses/write-off` | billing.manage | `{ projectId }` or `{ ids }` |
| POST `/acceptances` | milestones.manage | `projectId, milestoneId?, title`; customer signs via `/portal/acceptances/{id}/sign` (portal role). |
| POST `/risks`, `/allocations` | risks/team manage | per `ProjectExtrasFeatures.cs` records |

Settings that change behaviour: `projects.timesheetApproval`, `projects.expenseApproval`, `projects.maxHoursPerDay` (16), `projects.allowFutureTime`, `projects.restrictToMembers`. Required accounts: roles `wip`, `cogs`, `projects.costrecovery` (in the core template `Templates.cs:62`; created only if missing by `ProgramDemoTopUp.cs:39-42`). Timesheet and expense dates can be back-dated; invoice and journal dates cannot.

---

#### 4. Services (retainers, engagements, contracts)

Tables: `svc_Engagements, svc_Retainers, svc_Milestones, svc_TimeEntries`. Overlaps heavily with Projects (client work billed by milestone and time) but is a separate, thinner model.

| Step | Status | Evidence |
|---|---|---|
| Engagement (contract header) | Exists but wrong | Created directly as Active though Draft exists (`EngagementFeatures.cs:117`); `ClientName` is taken from the request, not the party record (`:53`); client party not validated. No contract document, terms, renewal or e-sign. |
| Retainer definition | Exists, incomplete | Fee, included hours, overage rate, cycle stored (`RetainerFeatures.cs:101-120`). `EngagementId` and `ClientId` are not validated. |
| Retainer billing | Missing | Nothing generates a recurring invoice, tracks the period, computes overage against included hours or rolls hours over. The only `CreateInvoice` call is for milestones. `RecurringFee` and `OverageHourlyRate` are inert data. |
| Time entry | Exists but wrong | `TimeEntryFeatures.cs:95-122`: engagement existence is silently ignored when null (`:116-121`), no status check (a Cancelled engagement accepts time), no edit or delete so mistakes cannot be corrected and `ConsumedHours` cannot be reduced. `ConsumedCost` is actually billing value including non-billable entries (`:120`). |
| Time approval | Exists but wrong | `ApprovedByUserId` comes from the request body (`:130-141`) so approval can be attributed to anyone; no separation from the worker. |
| Time billing (T&M engagement) | Missing | Approved time is never invoiced. |
| Milestone billing | Exists, correct with gaps | Invoice per milestone (`MilestoneFeatures.cs:158-202`); paid/void follow the invoice (`:239-278`). Gaps: no cap against `ContractValue`; create does not verify the engagement exists (`:112-130`); no tax code on the line (`:187-190`) so the default or none applies; `UpdateMilestoneStatus` accepts any `InvoiceId` (`:150-152`); `GET milestone` writes status (`:209-216`). |
| Contract value vs billing | Missing | No remaining-to-bill figure. |
| Profitability / cost | Missing | No cost rate; margin impossible. |

Classification: engagement and milestone billing **P**; retainer billing **M** (record exists, behaviour absent, so the row as built is **X**); time entry approval and billing **X**; overall module duplicates Projects: **P**.

Routes (base `/api/v1/m/services`):

| Verb and route | Body / required fields |
|---|---|
| POST `/engagements` (`services.engagements.manage`) | `legalEntityId, title, clientId, clientName, type (FixedPrice 1, TimeAndMaterials 2, Retainer 3), contractValue>=0, budgetedHours>=0, startDate, targetEndDate?, leadConsultantUserId?, leadConsultantName?, description?, customFields?` |
| POST `/engagements/{id}/status` | `{ status }` (Draft 1, Active 2, OnHold 3, Completed 4, Cancelled 5; allowed per `ServicesTransitions.cs`) |
| POST `/retainers` (`services.retainers.manage`) | `legalEntityId, engagementId, clientId, clientName, title, cycle (Monthly 1, Quarterly 2, Annual 3), recurringFee>=0, includedHours, overageHourlyRate, startDate, endDate?, terms?, customFields?`; POST `/retainers/{id}/status {status}` |
| POST `/milestones` (`services.milestones.manage`) | `legalEntityId, engagementId, title, amount, dueDate, notes?, customFields?`; POST `/milestones/{id}/status {status, invoiceId?}` (Planned to ReadyToInvoice by hand); POST `/milestones/{id}/invoice` (no body; creates the invoice dated today) |
| POST `/time-entries` (`services.timeentries.manage`) | `legalEntityId, engagementId, consultantUserId, consultantName, activityDate, hours, hourlyRate, taskDescription, isBillable, customFields?`; POST `/time-entries/{id}/approve {approvedByUserId}` |
| GET `/analytics/kpis?legalEntityId=` | read |

Sequences used: `ENG`, `RET`, `MLS` (module-provided).

---

#### 5. CRM (leads, pipeline, conversion)

Tables: `crm_Leads, crm_Opportunities, crm_Interactions`.

| Step | Status | Evidence |
|---|---|---|
| Lead capture, assignment, source | Exists, correct | `LeadFeatures.cs`, fixed source list (no campaign entity). |
| Lead qualification | Exists, correct | Status New, Contacted, Qualified, Unqualified, Converted; unqualified cannot convert (`:212`). No scoring. |
| Conversion | Exists, correct with gaps | One transaction: creates a customer-role party and an opportunity, marks the lead Converted (`:203-265`). No duplicate check on name, email or phone against existing parties (`:214-229`); the opportunity takes the typed title and revenue only. |
| Pipeline | Exists, correct | Stages Qualification, Proposal, Negotiation, ClosedWon, ClosedLost with transition map (`CrmTransitions.cs`); pipeline summary by stage (`OpportunityFeatures.cs:226+`). |
| Close won/lost | Exists but wrong | Two paths disagree: `/stage` to ClosedWon sets `ActualRevenue = ExpectedRevenue` and never records a reason (`OpportunityFeatures.cs:176-180`), while `/close` takes actual revenue and a reason (`:198-218`). |
| Hand-off after win | Missing | Closed-won creates no quote, order, project or engagement; nothing links forward. |
| Products on an opportunity, forecasting by line | Missing | Single revenue number and a typed probability. |
| Activities | Partly | Interactions with follow-up date and resolve flag; `RelatedEntityId` is not validated against the kind (**unverified**). No calendar, email sync or sequences. |
| Campaigns, territories, quotas | Missing | None. |

Classification: leads, pipeline, interactions **G**; conversion **P**; hand-off to sales/project **M**; the two close paths **X**.

Routes (base `/api/v1/m/crm`):

| Verb and route | Body / required fields |
|---|---|
| POST `/leads` | `legalEntityId, fullName, companyName?, jobTitle?, email?, phone?, source (Website 1, Referral 2, TradeShow 3, ColdCall 4, Campaign 5, Other 99), estimatedValue>=0, assignedUserId?, assignedUserName?, notes?, customFields?`; PUT `/leads/{id}` adds `status`. |
| POST `/leads/{id}/convert` | `opportunityTitle, expectedRevenue, expectedCloseDate?` (returns the new opportunity id) |
| POST `/opportunities` | `legalEntityId, title, customerPartyId?, customerName, stage, probabilityPercent, expectedRevenue, expectedCloseDate?, assignedUserId?, assignedUserName?, notes?, customFields?` |
| POST `/opportunities/{id}/stage` | `stage, probabilityPercent` (winning only from Negotiation) |
| POST `/opportunities/{id}/close` | `isWon, actualRevenue, closeReason?` |
| GET `/opportunities/pipeline-summary?legalEntityId=`, `/analytics/kpis` | read |
| POST `/interactions` | `legalEntityId, relatedEntityKind (Lead/Opportunity/Customer), relatedEntityId, type (Call 1, Meeting 2, Email 3, Note 4, Task 5), subject, details?, interactionDate?, performedByUserId?, performedByName?, followUpDate?, isResolved, customFields?`; POST `/interactions/{id}/resolve`; GET `/interactions/by-entity?kind&id` |

Sequences `LEAD`, `OPP` (module-provided).

---

#### 6. Cross-module facts a data seeder must respect

1. **No back-dating through the API for any of these documents.** Work order completion date, POS order/return/session timestamps, stock issue/receive dates and journal dates, project invoice and journal dates, milestone invoice dates all use "now" (`WorkOrderFeatures.cs:259`, `OrderFeatures.cs:534,618`, `ReturnFeatures.cs:209-234`, `BillingFeatures.cs:273`, `MilestoneFeatures.cs:178`). Only timesheet/expense dates, work order start/due, stock transfer date and CRM close dates are caller-controlled. A seeder wanting history must write rows (and matching journals) directly, with the date columns set.
2. **Direct table writes bypass the ledger.** Orders, returns, work-order completion, project cost posting and billing each post journals and stock movements inside the handler. If a seeder writes tables it must also write `JournalEntry` lines with `SourceType` `pos`, `pos-return`, `pos-session`, `manufacturing`, `projects.cost`, `projects.cost.release`, `projects.project`, and `StockMovement` rows with `Reference` = document number (void and return logic look documents up by that reference).
3. **Identity.** POS session open/close, drawer movements and returns need a signed-in user (`SessionFeatures.cs:172`, `ReturnFeatures.cs:127`); seeding through the API requires an authenticated token with the right permissions (`pos.orders.override-price` for free-text lines, `pos.orders.on-account` for account tenders).
4. **Sequences.** Handlers request `WorkOrder`, `POS`, `POSR`, `ENG`, `RET`, `MLS`, `LEAD`, `OPP`, `party`, `project`-style keys; they resolve from module-provided sequences (`ManufacturingModule.cs:66`, `PosModule.cs:80-81`). The core seeder list in `ERP.Api/ProgramSeeder.cs:183-198` uses different keys (`mfg_workorder`, `pos_order`, `crm_lead`...), which the handlers do not read.
5. **Required master data:** an active warehouse of the same legal entity (work orders, registers), Product items with sales price (POS) and tax code if taxed, accounts with roles `income.sales`, `TaxOutput`, `cash`, `bank`, `cogs`, `inventory`, `wip`, `projects.costrecovery`, a customer-role party for on-account sales and project/engagement billing.
6. **Stock must exist before work-order completion and POS sale** if the data is to be sane: nothing blocks negative stock (work orders always; POS unless `pos.allowNegativeStock=false`).
7. **Do not seed** `mfg_ProductCostings` (unused) or Retail promotion `TimesUsed` (never read by the till).
8. **Currency:** pass `currency` explicitly on price lists; the default is QAR.

---

#### 7. Highest-impact gaps (ranked)

1. Manufacturing has no material issue, MRP, routing/work centres, quality or reversal; consumption is back-flushed at completion with no stock check, scrap is not costed, and a completed order cannot be undone (`WorkOrderFeatures.cs:234-380`).
2. Projects leave fixed-fee and non-billable cost stranded in WIP and never release it at completion (`ProjectLedgerFeatures.cs:30-31`, `BillingFeatures.cs:332,283-290`).
3. Projects is not construction-capable: no BOQ, retention, certified valuations, subcontractors, material or equipment cost.
4. POS ignores Retail promotions and price lists; `TimesUsed` is never incremented; loyalty, gift cards, variants and exchanges do not exist.
5. POS returns can refund a card sale in cash and damaged returns leave cost of sales unadjusted (`ReturnFeatures.cs:189-195,279`).
6. Services retainers never bill; time entries can be approved by a caller-named user, are never invoiced and cannot be corrected (`TimeEntryFeatures.cs:95-141`).
7. Manufacturing journal credits WIP with no matching debit and still posts when Inventory is off (`WorkOrderFeatures.cs:336-358`, `StockPostingService.cs:93`).
8. Country neutrality breach: `"QAR"` hard-coded in Retail price list default and promo message (`RetailModels.cs:58`, `PromotionFeatures.cs:189`); document dates use UTC rather than the workspace zone across POS and Projects.

---

# Appendix D

### Audit: HR and Real Estate modules (code-traced)

Scope: `backend/modules/ERP.Modules.Hr` (paths below abbreviated `Hr/`), `backend/modules/ERP.Modules.RealEstate` (`RE/`). Method: followed handlers, not docs. Docs (`docs/REAL-ESTATE.md`, `GAP-ANALYSIS.md`, `PROJECT-REVIEW-2026-11-05.md`) were weighed, not trusted; where they disagree with code the code wins. Anything not traced end to end is marked **[unverified]**. No repo file was changed.

Classification key: **G** generic and reusable, **I** industry-specific, **P** partly reusable, **M** missing, **X** incomplete or wrong.

#### 1. Headline

| # | Finding | Severity |
|---|---|---|
| 1 | Real estate has no seller-side sale: no sale contract, no buyer payment plan, no handover, no revenue recognition, no unit "Sold" status. `DealType.Sale` is only a brokerage commission record. Developers and housing societies cannot be run. | Critical for those segments |
| 2 | Rent of `ManagedForOwner` properties is booked as company leasing income (`RE/Features/BillingFeatures.cs:108-110`), never as an owner payable. No owner payout, no owner ledger. Owner statement is a read-only report. | Critical for managers |
| 3 | Owner statement and portfolio collection use invoice allocations at gross (tax-inclusive) amounts: management fee and owner net are overstated for VAT-bearing leases (`RE/Features/OwnerStatementFeatures.cs:46-48`, `ReportFeatures.cs:113-131`). Likely, **[unverified]** that allocation amounts include tax. | High |
| 4 | Payroll is data-driven but country plumbing is split and uneven: statutory rules are per-entity data with nothing seeded by country packs; end-of-service exists for only 2 countries; the salary-file builders (UAE, Qatar) live inside the HR module, not in packs; `Employee` carries UAE fields. | High |
| 5 | Final settlement skips statutory deductions (`Hr/Features/PayrollFeatures.cs:246`, `TerminationFeatures.cs:199-202`). Gratuity base hardcodes 30 days, ignoring `hr.daysBasis` (`Hr/Features/EmployeeFeatures.cs:109`) while leave encashment uses the setting. | High |
| 6 | Payroll posting takes debits from line sums but credits from run totals (`PayrollFeatures.cs:182,194-196`). A stale run total posts an unbalanced journal. The project review reports exactly this on a demo draft (off by 2,000) with cause unknown; this is the structural weak point. | High |
| 7 | Loans are recovered by payroll without checking they were disbursed (`PayrollFeatures.cs:242`), which credits the advances account for money never paid out. | Medium |
| 8 | A renewal draft activated after the nightly job has ended the old lease throws (`RE/Features/LeaseFeatures.cs:250-255` calls `MarkRenewed` on an `Ended` lease; job at `LeaseExpiry.cs:17-19`). Holding-over leases generate no rent after the end date. | Medium |

#### 2. Hire to Retire trace

| Step | Verdict | Evidence and failure mode | Class |
|---|---|---|---|
| Org structure: departments, positions, org chart | Exists and correct | `Hr/Domain/Structure.cs`; departments nest (`ParentDepartmentId`), loop-guarded (`EmployeePeople.cs:123`), carry `BusinessUnitId` which payroll uses to split the expense line (`PayrollFeatures.cs:179-184`). Positions have `Grade` only. Branches: **no branch or location entity in HR**; the core legal entity and business unit stand in. Employee has both `Department` (text, kept in sync from `DepartmentId`, `EmployeePeople.cs:67-71`) and `DepartmentId`. | G |
| Job requisition | Missing | `JobOpening` (`Onboarding.cs`, `OpeningStatus` Open/OnHold/Closed) is a vacancy, not a requisition: no approver, no budget, no position-to-headcount control. | M |
| Candidates and pipeline | Exists and correct | `RecruitmentFeatures.cs`: stages Applied, Screening, Interview, Offer, Hired, Rejected; forward-only moves, reject and reopen (`CandidatePipeline.CanMove`). No CV attachment, interview schedule, scorecard or source analytics. | G |
| Offer | Missing | Stage `Offer` exists but there is no offer entity (salary, letter, expiry, acceptance). Hire reads salary from `HireInput` only. | M |
| Hiring | Exists and correct | `HireCandidate` requires stage Offer, creates the employee through `CreateEmployee` (so numbering, history, validation apply), optionally applies an onboarding checklist (`RecruitmentFeatures.cs:154-171`). Two saves, no transaction: if `ApplyChecklist` fails the candidate is already Hired. | G |
| Onboarding | Exists and correct | Checklist templates with due offsets, applied once per employee, open-task list (`OnboardingFeatures.cs`). Document expiry reminders exist. No asset issue, no probation confirmation workflow. | G |
| Employee master and pay structure | Partly right | Pay is `BasicSalary` + 3 fixed allowance columns + one `RecurringDeductions` figure (`Hr/Domain/Hr.cs:25-30`). No salary structure or components, no effective-dated salary revisions (history is audit text), single currency (entity base). `LabourCardNumber` and `BankRoutingCode` are UAE salary-file fields on the core employee row (`Hr.cs:33-36`). `Nationality` is free text and statutory rules match on it (known issue, confirmed `StatutoryFeatures.cs:73-82`). | P |
| Attendance | Exists and correct | Monthly exceptions (`AttendanceRecord`) and daily logs with shifts, roster, device punch endpoint, late and overtime maths, roll-up into the monthly record (`DailyAttendanceFeatures.cs:159-189`). Roll-up reads an org-wide calendar, not the entity's (`calendars.LoadAsync(start,end)` has no entity), while settings are per entity, so mixed-country orgs get the wrong weekend. **[unverified]** whether the calendar reads per-entity weekend. | G |
| Leave | Exists and correct | Types with tiers, pay bands, accrual, half days, year-split balances, approval with line manager and self-approval block, holiday calendar, country defaults endpoint (`LeaveFeatures.cs`, `LeaveDefaults.cs`). Leave types and holidays are org-scoped, not entity-scoped. No scheduled carry-forward or monthly accrual job. | G |
| Payroll calculation | Partly right | Maths in `PayrollMath.cs`: gross, absence at gross over days-basis, overtime from basic, proration for joiners and leavers, rounding half away from zero. Then statutory rules (`StatutoryEngine`), then loan instalments, never below zero net. Regular, off-cycle and final-settlement kinds. Wrong or weak: no tax annualisation or YTD (slabs apply to the month, `StatutoryFeatures.cs:57-70`); statutory only on Regular runs (`PayrollFeatures.cs:246`); overtime multiplier is a single setting; absence on gross includes allowances by design. | P |
| Payroll journals | Exists, one structural defect | Accrual: Dr salaries (split by business unit), Dr gratuity from provision first then expense, Cr accrued salaries (net), Cr deductions payable, Cr advances for loans, plus employer statutory cost (`PayrollFeatures.cs:172-206`). Payment: Dr accrued, Cr bank, partial pay by employee (`PayrollVoidFeatures.cs:41-77`). Defects: finding 6; a business-unit group whose net debit is zero or negative is skipped (`:183`), unbalancing the entry; notice-shortfall deductions are credited to the deductions-payable liability and never reach income (`:195`). Posting date is the month end, pay date free. Approval gate via `hr.payrollRequiresApproval`. | P |
| Payroll reversal | Exists and correct | `VoidPayroll` reverses each payment journal then the accrual, restores loan balances and settled status, requires a reason, writes an audit event, frees the month (`PayrollVoidFeatures.cs:79-107`). Reversals are dated today, not the original period. A posted run cannot be recalculated; only void and regenerate. | G |
| Bank and salary files | Partly right | Generic bank CSV; salary file chosen by entity country: UAE and Qatar builders, else a configurable CSV (`Wps/WpsFileHandlers.cs`). Builders are registered in the HR module (`HrModule.cs:113-114`), violating the "country rules ship as packs" rule in AGENTS.md. | X |
| Performance | Exists, shallow | Cycles, reviews, 1 to 5 ratings, competencies, submit and acknowledge (`ReviewFeatures.cs`). No goals as entities, no 360, no calibration, no link to pay revision or promotion. | P |
| Promotion, transfer, salary revision | Missing | Only free-text history notes and field-change history (`EmployeeHistoryFeatures.cs`). No letters, no effective-dated change. | M |
| Separation | Exists and correct | `TerminateEmployee`: status, date, reason, last working day, notice date, cancels future leave (and restores balances), history entry (`TerminationFeatures.cs:80-92`). Offboarding checklist kind exists. No resignation request or clearance approval. | G |
| Final settlement | Partly right | Pro-rated final month (skipped if a regular run already paid it), encashable leave at basic over days-basis, end of service, notice shortfall, loan recovery capped at remaining pay; creates a one-line run that posts and pays like any run (`TerminationFeatures.cs:94-206`). Defects: no statutory deductions; end-of-service uses basic over a hardcoded 30; `ExistingSettlementAsync` loads every settlement run and filters in memory (`:130-131`); the final month's leave balance uses the termination year only. | P |
| Gratuity and provision | Partly right | Calculator pulls the rule from the country pack (`EmployeeFeatures.cs:99-111`, `ICountryPack.EndOfService`); true-up posts Dr expense, Cr provision (`GratuityFeatures.cs:34-52`). Rules exist for AE and QA only **[unverified: grep found no other `EndOfService` overrides in `backend/countrypacks`]**. Report excludes terminated staff, so the provision is released at termination and then consumed by the settlement. | P |

Country plug-in answer: **payroll is not hardcoded to one country in its maths** (rates, caps, slabs, nationality filters are rows in `StatutoryRule`/`StatutorySlab`, per legal entity, effective-dated). It is hardcoded in four places: (a) salary-file builders for AE and QA in the HR module; (b) UAE columns on `Employee`; (c) setting defaults (`hr.weekendDays` Saturday,Sunday, `hr.daysBasis` 30, shift 08:00) not set by packs; (d) the end-of-service divisor 30. Packs plug in through `ICountryPack` (`Application/Templates/CountryPack.cs`): taxes, custom fields, `EndOfService`, `LeaveDefaults`, `PublicHolidayTemplates`, `TaxReturn`. There is no pack hook for payroll statutory rule sets, salary-file builders, weekend or days-basis defaults.

#### 3. Real estate trace

| Step | Verdict | Evidence and failure mode | Class |
|---|---|---|---|
| Portfolio: property, unit, unit type | Exists and correct | `RE/Domain/Portfolio.cs`. Unit state machine is enforced in the entity (private setters, Reserve, Occupy, Vacate, MakeUnavailable). Rent history rows, bulk unit creation per floor, import definitions, custom fields. Limits: two levels only (property then unit); no phase, block, tower, plot or land parcel; `AreaSqft` and annual rent are fixed units; `Community`, `Region` are free text; no ownership share or co-owners. | P |
| Listings, leads | Exists and correct | Draft, Published, Closed listings for Rent or Sale; lead pipeline; convert to deal (`CrmFeatures.cs`). Commission rate defaults are hardcoded 2% for sale and 5% for lease (`CrmFeatures.cs:212`), market-specific. Listing publish does not reserve the unit or check status. | P |
| Sales vs leasing | Leasing correct, sales missing | Leasing is full lifecycle. For sales only `BrokerageDeal` with `DealType.Sale` exists: a client, value, commission rate, invoice for the commission, agent payout. Selling an owned unit to a buyer creates no contract, no receivable, no revenue, and the unit never leaves the portfolio (`UnitStatus` has no Sold). | X |
| Lease creation and activation | Exists and correct | Overlap check plus DB filtered unique index; unit to Occupied; schedule fixed at activation in one transaction (`LeaseFeatures.cs:235-273`). Tenant must be a customer party. Draft-only edit. | I |
| Rent schedule | Exists and correct, narrow | `RentSchedule.cs`: 1/2/4/6/12 instalments, anchored month arithmetic, prorated short last period (days/365 or 366), remainder on last row, grace days. Due on first day of each period. Missing: rent escalation, irregular or cheque-count plans, per-instalment amounts, service charge or utilities lines, late fees. | I |
| Billing run | Exists and correct, one gap | Invoices due instalments, one invoice each, via the core invoicing service; voiding an invoice releases the instalment (`BillingFeatures.cs:67-95`). Commercial rent takes the org's default tax code, residential none unless the lease sets one: assumes the default tax code is the standard rate **[unverified]**. Ownership type ignored (finding 2). Holding-over periods are not billed. | I |
| Tenants | Exists, thin | Tenant is a core party with the Customer role; no tenant screening, no tenant ledger inside the module (uses core AR). Tenant portal: leases, maintenance requests (`PortalFeatures.cs`). | G |
| Deposits | Exists and correct | Receive (Dr bank, Cr deposits held by lease and tenant), settle (damages to income, arrears via a real settlement, refund from bank), roll over to a renewal with register-only entries (`FinanceFeatures.cs:123-218`). Damages income is posted without tax. Roll-over uses `DateTime.UtcNow`, not the clock service (`:210`). | I |
| Renewal and termination | Exists, one defect | Renewal workbench and bulk drafts; termination prorates the current instalment, cancels later ones, lists and credits invoiced instalments after the date (`CreateTerminationCredits`). Defect: finding 8. | I |
| Maintenance: work orders, preventive | Exists and correct, no money | Status machine with permission per target status; completion releases a unit parked for repairs; cost basis (owner, company, tenant, shared) and bill link (`WorkOrderFeatures.cs`). Completing a work order posts nothing. Tenant-chargeable cost is shown on the owner statement but never invoiced to the tenant. | I |
| Inspections | Exists and correct | Move-in, move-out, routine checklists; completion can raise repair work orders per poor or damaged item (`InspectionFeatures.cs:83-103`). Damage cost is not tied to the deposit settlement automatically. | I |
| Commissions | Exists and correct | Deal stages, commission invoice (income), agent and team-lead payout (Dr commission expense, Cr bank) after the client pays; payout state follows the invoice payment event (`FinanceFeatures.cs:23-84`). No commission on leases tied to the lease record, no clawback if the deal is later credited. | I |
| Installment plans for buyers | Missing | The only installment machinery is `RentInstalment` (rent) and `FundingInstalment` (buyer-side loan from the investor's view). No seller-side schedule of down payment, milestones, handover payment, late penalty or cancellation and forfeiture. `FundingType.DeveloperPlan` is a loan-style annuity or 0% equal-principal schedule (`FundingSchedule.cs`) used by the acquirer, monthly only. | M |
| Valuation | Missing | Only `Property.IndicativeValue`, a typed number used as the yield denominator (`ReportFeatures.cs:124`). No valuation entity, no history, no revaluation journal. | M |
| Owner statements | Partly right | Per-owner, per-period: collected rent, management fee, owner share of completed maintenance (cost from linked bill, else actual, else estimate) (`OwnerStatementFeatures.cs`). Defects: findings 2 and 3; cash basis only; ignores deposits and owner contributions; not saved or issued; fee has no tax; no minimum fee or leasing fee. Reads rows in memory for the entire org's receipts in the period. | X |
| Portfolio reports | Partly right | Rent roll, occupancy, performance, lease expiries, maintenance, commissions (`ReportFeatures.cs`). Defects: rent roll "outstanding" and cheques held are per tenant party, so a tenant with two leases is counted on both rows (`:63-66,73`); performance uses only completed work-order costs as operating expense (excludes bills, taxes, utilities, management cost) and counts receipts only for invoices billed inside the same period (`:113-131`), so collection rate misstates; expiry report ignores HoldingOver (`:141`). | X |
| Acquisitions and funding | Exists and correct | Draft, complete (Dr investment property, Cr bank and financing payable), schedule generation, ordered instalment payment posting principal and interest, cash-forecast source (`AcquisitionFeatures.cs`). Balanced when fully funded. No disposal, no depreciation or fair-value model, whole-property level only (no unit-wise purchase), `Complete` does not touch the property record. | I |

#### 4. What is missing for each target segment

| Segment | Missing (all **M** unless stated) |
|---|---|
| Developers | Project, phase, block, tower hierarchy and unit pricing/price lists; unit statuses Sold, Booked, Handed over; booking and reservation with token money (a `Reserved` unit has no amount or buyer); sale and purchase agreement; buyer payment plan (down payment, dated or milestone-linked instalments, handover payment) with its own invoicing; percentage-of-completion or handover revenue recognition; escrow and construction-linked collection; buyer default, cancellation and forfeiture; title transfer and handover checklist, snagging (inspections exist and are reusable); cost-to-complete and land cost allocation to units; broker commission tied to receipts; agent hierarchy. |
| Housing societies | Member and plot/file register (members are parties but there is no membership or share entity); plot, street, block hierarchy; monthly maintenance and service charge billing by plot area or type with arrears, surcharge and waiver; development charges and transfer fees on resale; utility meter billing; committee, voting, receipts by head of account; non-profit fund accounting; encroachment and possession records. |
| Property investment | Valuation history and revaluation postings; depreciation or fair-value model; unit-level acquisition; disposal with gain or loss; return metrics (IRR, cash-on-cash, cap rate over time) beyond yield on a typed value; mortgage covenant tracking; investor or co-owner registers, distributions and per-investor statements; budget per property (the budget feature is generic in core); property tax and insurance schedules. |
| Property managers | Owner payable or trust account, owner payout run, owner statement issuing and approval, tenant chargeback invoicing, management-fee invoicing and leasing-fee rules. |

#### 5. Preserve (do not rewrite)

- Lease and unit state machines in the domain entities (`Portfolio.cs`, `Leasing.cs`), with the filtered unique index guarding double occupancy.
- `RentSchedule` and `FundingSchedule` pure maths and their rounding rules (remainder to last row); reusable for sale instalments.
- Deposit register design (shared liability account, register-only roll-over) and settlement through a real receipt allocation.
- Billing run through the core invoicing service with void-releases-instalment notification; same pattern for any new schedule.
- Deal commission flow driven by invoice payment events.
- Work order permission-per-target-status and repair-parking of units.
- Whole HR side that is correct: leave policy engine, attendance roll-up, payroll void with loan restoration, statutory rule engine (data-driven), final-settlement structure, recruitment pipeline rules, separation-of-duties and approval gating.
- Cross-module hooks already in place: `IReminderSource`, `ISearchProvider`, `ICashForecastSource`, `IScheduledJob`, `IImportDefinition`, attachment resolvers, `ICountryPack.EndOfService` and leave/holiday templates.

#### 6. Feature classification summary

| Feature | Class |
|---|---|
| Departments, positions, org chart, employee master, documents, history | G (employee master is P because of UAE columns and fixed allowance columns) |
| Recruitment pipeline, hire, onboarding and offboarding checklists | G |
| Requisition, offer, promotion/transfer/salary revision | M |
| Attendance, shifts, leave, holidays, loans | G |
| Payroll run, post, pay, void | P (engine generic, journals need the finding 6 and 7 fixes) |
| Statutory rules engine | G; pre-filled country rule sets: M |
| Salary file builders | X (country code in the wrong layer) |
| Gratuity, final settlement | P |
| Performance reviews | P |
| Properties, units, unit types | P |
| Leases, rent schedule, billing run, renewals, termination credits, deposits | I |
| Work orders, preventive maintenance, inspections | I |
| Listings, leads, brokerage deals and commissions | I (commission defaults hardcoded: X) |
| Acquisitions and funding | I |
| Owner statements, portfolio reports | X |
| Sale contract, buyer installment plan, handover, revenue recognition | M |
| Valuation | M |
| Owner payout and trust accounting | M |

#### 7. API reference for a demo-data seeder

Base paths: HR `/api/v1/m/hr`, RE `/api/v1/m/realestate`. All responses are `ApiResponse<T>`. Enums are numeric unless the JSON options map strings **[unverified]**. Positional records need every non-nullable member present (send `status`, `kind`, `priority`, etc. explicitly). Tables if writing SQL: HR `hr_Employees, hr_PayrollRuns, hr_PayrollLines, hr_AttendanceRecords, hr_AttendanceLogs, hr_LeaveRequests, hr_LeaveBalances, hr_EmployeeLoans, hr_PayrollLoanRecoveries, hr_StatutoryRules, hr_StatutorySlabs, hr_PayrollLineStatutory, hr_JobOpenings, hr_Candidates, hr_ChecklistTemplates, hr_EmployeeTasks, hr_PerformanceReviews`; RE `re_Properties, re_Units, re_UnitTypes, re_UnitRentRates, re_Listings, re_Leads, re_BrokerageDeals, re_Acquisitions, re_FundingSources, re_FundingInstalments, re_DepositTransactions, re_Inspections, re_PmSchedules` (leases, rent instalments and work orders are mapped through `RealEstateModel.cs`, table names not confirmed). A SQL seeder must also write ledger journals and invoices consistently; the API route is the safer path. An existing EF-based seeder is `backend/src/ERP.Api/ProgramSeeder.cs` (`SeedHrAsync`, line 1503).

Prerequisites for posting: GL roles `payroll.expense, payroll.accrued, payroll.advances, payroll.deductions (optional), payroll.gratuity.provision, payroll.gratuity.expense`; RE roles `realestate.income.leasing, .brokerage, .damages, realestate.expense.commission, .finance, realestate.deposits, realestate.investment, realestate.financing`; an open period; a bank/cash account; legal entity with `CountryCode` (drives end of service and salary file).

**HR lifecycles**

| Document | Calls in order | Required fields |
|---|---|---|
| Department / position | `POST /departments`, `POST /positions` | dept: `code, name` (`parentDepartmentId, headEmployeeId, businessUnitId, isActive`); position: `code, title` (`grade`) |
| Employee | `POST /employees`; `PUT /employees/{id}` | `legalEntityId, fullName, joinDate, status` (1 Active, 2 OnLeave, 3 Terminated), `basicSalary` (>0, required on create), `designation` unless `positionId`. Optional: `number` (auto `EMP-0001`), allowances, `iban` (shape checked), `departmentId`, `managerEmployeeId`, `nationality`, `contractType`, `noticePeriodDays`, `userId` for self-service |
| Statutory rule | `POST /statutory-rules` | `legalEntityId, code, name, kind` (1 employee, 2 employer), `method` (1 flat, 2 slab), `base` (1 basic, 2 gross); `rate` 0-100 or `slabs[{fromAmount,rate}]`; `nationalities` text e.g. `!Qatari` |
| Recruitment | `POST /openings` then `POST /candidates` then `POST /candidates/{id}/move` (Applied 1, Screening 2, Interview 3, Offer 4, Rejected 6) then `POST /candidates/{id}/hire` | opening: `title, headcount, status` (1); candidate: `openingId, fullName`; hire only from Offer: `legalEntityId, joinDate, designation, basicSalary` (`onboardingTemplateId`) |
| Onboarding | `POST /checklists` then `POST /employees/{id}/checklists/{templateId}` then `POST /tasks/{id}/done` | template: `name, kind` (1 onboarding, 2 offboarding), `items[{title,dueOffsetDays}]`; done: `{done:true}` |
| Attendance | `PUT /attendance` (monthly) or `PUT /attendance/daily`, `POST /attendance/daily/bulk`, `POST /attendance/punch`, then `POST /attendance/daily/roll-up` | monthly: `employeeId, year, month, workingDays, daysPresent, unpaidAbsentDays, paidLeaveDays, overtimeHours`; daily: `employeeId, date` (`checkIn, checkOut, status` 1 Present, 2 Absent, 3 Leave, 4 Holiday, 5 WeeklyOff, 6 HalfDay); punch: `{punches:[{employeeNumber,at}]}`; roll-up: `{year,month}` |
| Leave | `POST /leave-types`, `POST /holidays`, `POST /leave/apply-country-defaults`, `POST /leave` then `/submit` then `/approve` or `/reject`, `/cancel` | type: `code, name, isPaid, annualDays, carryForwardCap`; holiday: `date, name`; request: `employeeId, leaveTypeId, fromDate, toDate` (`submit:true` to submit at once). Approver must not be the requester unless `hr.blockSelfApproval` is off |
| Loan | `POST /loans` then `POST /loans/{id}/disburse` | `employeeId, kind` (1 loan, 2 advance), `principal, instalments, startYear, startMonth`; disburse: `bankAccountId, date` (do this before the first payroll, finding 7) |
| Payroll | `POST /payroll` then optional `PUT /payroll/{id}/lines/{lineId}`, `/recalculate`, then `/post` then `/pay` then optionally `/void`; `GET /payroll/{id}/wps-sif`, `/bank-file`, `/payslips/{lineId}/payslip.pdf` | generate: `legalEntityId, year, month` (`kind` 0 regular, 1 off-cycle needs `notes`; `employeeIds`, `includeSalary`); adjust: `adjustment` (`note`); pay: `bankAccountId, date` (`employeeIds` for partial); void: `reason` required. Post returns PendingApproval when `hr.payrollRequiresApproval` is true. One non-voided regular run per entity and month |
| Review | `POST /review-cycles`, `POST /reviews`, `/submit`, `/acknowledge` | cycle: `name, periodStart, periodEnd, status` (1 open); review: `employeeId, cycleId, competencies[{name,score 1-5}]`, `overallRating` 1-5 required to submit |
| Separation | `GET /employees/{id}/settlement-preview?terminationDate=` then `POST /employees/{id}/terminate` then `POST /employees/{id}/final-settlement` (no body) then payroll `/post`, `/pay` | terminate: `terminationDate` (`reason, lastWorkingDay, noticeGivenOn`). Settlement run is Kind 2, one line |
| Gratuity | `GET /reports/gratuity?asOf=`, `POST /reports/gratuity/provision` | `legalEntityId, asOf` |

**Real estate lifecycles**

| Document | Calls in order | Required fields |
|---|---|---|
| Unit type, property, unit | `POST /unit-types`, `POST /properties`, `POST /units` or `POST /units/bulk`; `POST /units/{id}/status` | unit type: `code, name, defaultUsage` (1 residential, 2 commercial); property: `legalEntityId, name, propertyType, ownershipType` (1 owned, 2 managed, which also needs `ownerPartyId`), `totalUnits, indicativeValue` (`managementFeePercent`); unit: `propertyId, unitNumber, unitTypeId, areaSqft, askingRent`; bulk: `propertyId, unitTypeId, fromFloor, toFloor, unitsPerFloor, areaSqft, askingRent`; status body `{action: Reserve/ReleaseReservation/MakeUnavailable/MakeAvailable, until?, reason}` |
| Lease | `POST /leases` (or `activateNow:true`), `POST /leases/{id}/activate`, `/renew`, `/terminate`, `/cancel`, `/holding-over`, `/move-out`, `POST /leases/process-expiries` | `unitId, lesseePartyId` (customer party), `startDate, annualRent` (`endDate` defaults to one year, `paymentFrequency` 1/2/4/6/12 default 4, `securityDepositAmount, graceDays, taxCodeId`); terminate: `date` inside the term, `reason` |
| Billing | `POST /billing/run`, `POST /billing/instalments/{id}/invoice` | run: `asOf` (`legalEntityId`); instalments exist only after activation |
| Deposit | `POST /leases/{id}/deposit`, `/deposit/settle`, `/deposit/transfer`, `POST /leases/{id}/termination-credits` | receive: `bankAccountId, date, amount` (cannot exceed the agreed deposit); settle: `date, damages, refund` (`arrears[{invoiceId,amount}]`, `refundBankAccountId`), lease must be Ended, Terminated or Renewed; transfer: `toLeaseId` |
| Work order | `POST /work-orders`, `POST /work-orders/{id}/status`, `PUT /work-orders/{id}/cost-basis` | `propertyId, issue, priority` (1-4); status (0 New, 1 Assigned needs vendor or technician, 2 InProgress, 3 OnHold, 4 Completed, 5 Closed, 6 Cancelled): `{status, actualCost, resolutionNotes}` |
| Preventive maintenance | `POST /pm-schedules`, `POST /pm-schedules/run` | `propertyId, title, frequencyMonths, nextDue` |
| Inspection | `POST /inspections`, `POST /inspections/{id}/complete` | `propertyId, kind` (1 move-in, 2 move-out, 3 routine), `inspectionDate, items[{area,condition 1-4,repairCost}]`; complete needs at least one item (`raiseWorkOrders`) |
| Listing, lead, deal | `POST /listings`, `/listings/{id}/status`; `POST /leads`, `/leads/{id}/stage`, `/leads/{id}/convert`; `POST /deals`, `/deals/{id}/stage` (Lead 0, Viewing 1, Negotiation 2, Agreed 3, Lost 9), `/invoice-commission`, `/pay-agent` | listing: `propertyId, kind, title, price`; lead: `legalEntityId, name`; deal: `legalEntityId, dealType, clientName, dealDate, dealValue, commissionRatePercent`; invoice needs `clientPartyId` and Agreed stage; pay-agent only after the client has paid: `bankAccountId, date` |
| Acquisition | `POST /acquisitions`, `POST /acquisitions/{id}/complete`, `POST /acquisitions/instalments/{id}/pay` | `legalEntityId, propertyId, sellerName, purchaseDate, purchasePrice, downPayment, transferFees, otherCosts`, `payFromAccountId` when cash is due, `funding[{type,lender,principal,annualRatePercent,termMonths,firstDueDate}]`; down payment plus funding must equal the price at completion; instalments pay in sequence |
| Reports | `GET /reports/owner-statement?ownerPartyId&from&to`, `/rent-roll?asOf`, `/occupancy`, `/portfolio-performance?from&to`, `/lease-expiries`, `/maintenance-costs`, `/commissions` | need `core.reports.read` |
| Tenant and vendor portal | `/portal/leases`, `/portal/requests`, `/portal/vendor/work-orders` | separate portal tokens; outside the staff seeder |

#### 8. Notes and unverified items

- Frontend: RE and HR routes exist for each lifecycle above; the RE reports (owner statement, rent roll, occupancy) are registered in the shared reports page (`frontend/src/app/pages/reports/report-registry.ts`), not under `modules/realestate`. No UI was run.
- Not read in full: `PortalFeatures.cs`, `VendorPortalFeatures.cs`, `PreventiveMaintenance.cs`, `RealEstateImports.cs`, `UnitFeatures.cs`, `HrReports.cs`, `HrDashboard.cs`, `SelfServiceFeatures.cs`, `PayslipFeatures.cs`, `LeaveEntitlement.cs`, the database migrations, and all tests. No code was executed.
- The review document's claims on statutory nationality text matching, double department storage, weekend default and missing country payroll sets were confirmed in code; its claim that the unbalanced draft cause is unknown stands, finding 6 is a candidate not a proven cause.

---

# Appendix E

### Tenant isolation and authorization review

**Scope and method.** Static review of `backend/` (paths below are relative to it) plus read-only catalogue and count queries against the dev database. No server was started and nothing was exploited live. "Verified" means read in code (or counted in the database where stated). "Suspected" means inferred and not traced end to end.

**Bottom line.** The tenant data filter itself is well built and I found no way to read another workspace's business rows through it. The weaknesses sit around it: accounts are global across tenants and can be pre-claimed, the audit trail bypasses field-level controls and even stores two-factor secrets, delegated admins can grant themselves anything, and the company (legal entity) boundary is enforced only where each handler remembers to.

#### Findings summary

| ID | Severity | Finding | Status |
|---|---|---|---|
| C1 | Critical | Cross-tenant account takeover: admin-chosen password on a global account, reused silently by later invites | Verified (code) |
| H1 | High | Authenticator secrets and recovery codes copied in clear into the workspace audit log | Verified (code + DB counts) |
| H2 | High | `core.users.write` or `core.roles.write` escalates to full admin, including self | Verified (code) |
| H3 | High | Audit log exposes salary and personal fields to holders of `core.audit.read`, bypassing `hr.salary.read` | Verified (code + DB counts) |
| H4 | High | Company boundary not enforced on writes in about 10 module handlers, nor on approvals | Verified (code; some by pattern scan) |
| M1-M11 | Medium | See below | Verified unless marked |
| L1-L8 | Low | See below | Verified unless marked |

---

#### Critical

**C1. Cross-tenant account takeover through pre-claimed global accounts.**
Evidence: `src/ERP.Infrastructure/Identity/UserDirectory.cs:37-53` creates the account with the inviter's chosen password, or returns an existing account and silently ignores the new password. `src/ERP.Application/Security/SecurityFeatures.cs:176-194` then adds that account to the workspace. Login lists every membership of the global account (`src/ERP.Infrastructure/Auth/AuthService.cs:407-412`). `MustChangePassword` is not enforced: the server only returns it from `/me` (`src/ERP.Modularity/Features/SessionFeatures.cs:45`) and the only non-test frontend use is a type (`frontend/src/app/core/session/session.store.ts:19`). `EmailConfirmed` is never checked at login. With two-factor enforced, a password alone is enough to enrol the first authenticator (`AuthService.cs:97-101,130-143`), so enforcement does not help.

Exploit:
1. Attacker holds admin in any workspace A (a trial is enough). They call `POST /users` with `cfo@victim.example` and a known password P.
2. Later the real company's admin in workspace B invites `cfo@victim.example` as Org admin. B's temporary password is silently discarded and the existing account is linked.
3. Attacker signs in with P, enrols their own authenticator, picks workspace B, and has full access to B's data.

Variants:
- Workspace A's admin invites a consultant, who later joins B. A's admin still knows the never-changed temporary password.
- Public sign-up can claim an unverified email. Verification only happens when mail is enabled (`src/ERP.Application/Platform/RegistrationFeatures.cs:96`), and the shipped production config has mail off, so a reviewed sign-up provisions the account with `EmailConfirmed = true` (`src/ERP.Infrastructure/Services/OnboardingService.cs:77`). Sign-up is approval-gated by default (`SignupMode.Review`); open mode without verification provisions at once (`RegistrationFeatures.cs:179`, non-default).
- Amplifier: `src/ERP.Infrastructure/Services/PlatformBootstrap.cs:105-110` promotes a pre-existing account named by `Platform:FounderEmail` to platform admin.
- Information leak: the invite response returns the existing account's id and full name to any tenant admin (`SecurityFeatures.cs:176-193`, `UserDirectory.cs:52`), which also works as an email oracle.

Smallest correct fix:
1. Remove `TemporaryPassword` from `InviteUserInput`. Create the account with no password and email a single-use set-password link, reusing `PasswordResetToken`.
2. For an existing account, create the membership with `IsActive = false` and activate it only when the user accepts (invitation token or prompt at sign-in). `WorkspacesForAsync` already filters on `m.IsActive`.
3. Enforce `MustChangePassword` on the server: at `IssueAsync`, return a challenge instead of a session, as the two-factor setup path already does.
4. Respond to invites with the input email only, never the existing full name.
5. Migration: force a reset on accounts with `EmailConfirmed = 0` or `MustChangePassword = 1` that have more than one membership.

---

#### High

**H1. Two-factor secrets in the audit log.**
Evidence: the interceptor's `NotAudited` list (`src/ERP.Infrastructure/Persistence/AuditSaveChangesInterceptor.cs:27`) and `SecretProperties` (`:21-22`) omit the identity token entity and its `Value` column. A signed-in `/auth/2fa/setup` or `/auth/2fa/enable` (`AuthService.cs:215,235`) saves under an org context, so `:90-96` writes an event. `src/ERP.Application/Audit/AuditLog.cs:64,105-107,126-137` returns every field unmasked except a short name list.
Database check (counts and lengths only, no values printed): 8 audit rows of record type `IdentityUserToken`1` across 2 organizations, 4 containing `AuthenticatorKey` with a 32-character value, 2 containing `RecoveryCodes` with a 95-character value.
Exploit: a default Finance manager (`core.audit.read`, `src/ERP.Application/Templates/Templates.cs:114`) opens `/audit/{id}` and copies a colleague's authenticator secret and recovery codes. Combined with a phished or shared password this defeats two-factor on a global account, including that user's other workspaces.
Smallest correct fix: add `IdentityUserToken<Guid>`, `IdentityUserClaim<Guid>`, `IdentityUserLogin<Guid>` and `IdempotencyRecord` to `NotAudited`, delete existing rows of those record types, and force re-enrolment of affected users. Follow with M7 (encrypt tokens at rest).

**H2. Delegated admins can escalate to full admin.**
Evidence: `SecurityFeatures.cs:100,118` explicitly allow `"*"` in a role. `:132-144` assigns any workspace role to any member, including the caller, with no subset check. `:176-186` does the same on invite. Permissions refresh at the next token rotation (`AuthService.cs:415-429`).
Exploit: a helpdesk custom role with only `core.users.write` calls `PUT /users/{self}/roles` with the Org admin role id and gains everything within five minutes. A holder of `core.roles.write` edits their own custom role to add `"*"`. Both also work on any other member. Tenancy still holds, but internal separation of duties does not. No built-in role except Org admin holds these two permissions (verified), so the risk lands on custom delegated-admin roles, which multi-industry customers will create.
Smallest correct fix: in `CreateRole`, `UpdateRole`, `SetUserRoles` and `InviteUser`, load the caller's effective permissions and refuse any permission or role outside them. Only a `"*"` holder may grant `"*"`. Refuse changes to one's own roles unless the caller holds `"*"`.

**H3. Audit log bypasses field-level controls.**
Evidence: snapshots copy every property except secrets (`AuditSaveChangesInterceptor.cs:121-131`). The read side masks only `Iban, IdNumber, TaxNumber, AccountNumber, LabourCardNumber` (`AuditLog.cs:105`). The normal API gates pay on `hr.salary.read` (`modules/ERP.Modules.Hr/Features/EmployeeFeatures.cs:178`, `EmployeePeople.cs:109`).
Database check: 83 of 83 `Employee` audit rows contain pay fields. `IdempotencyRecord` rows (24) also mirror stored response bodies into the audit log (`AuditSaveChangesInterceptor.cs:27` does not exclude them; sensitive content in them is suspected, not confirmed).
Exploit: a Finance manager without HR access reads every salary, date of birth, address and emergency contact through `/audit` and `/audit/record?recordType=Employee`. By design, any future patient field would be mirrored the same way.
Smallest correct fix: add a map of record type to required read permission in `AuditHandlers` and `RecordHistoryHandler` (for example `Employee`, `PayrollRun`, `PayrollLine` require `hr.salary.read`; otherwise strip field values and keep who/when/which-fields). Longer term, tag sensitive properties with an attribute that the interceptor stores masked.

**H4. Company (legal entity) boundary is opt-in per handler.**
Evidence: the read filter is sound (`src/ERP.Infrastructure/Persistence/AppDbContext.cs:533-547`) but nothing checks writes; the interceptor validates only `OrgId`. Handlers that take `request.LegalEntityId` with no guard: `modules/ERP.Modules.Services/Features/EngagementFeatures.cs:107-111` (read), and by pattern scan `RetainerFeatures.cs:106`, `MilestoneFeatures.cs:120`, `TimeEntryFeatures.cs:100`; `modules/ERP.Modules.Retail/Features/StoreFeatures.cs:89-92` (read), `PriceListFeatures.cs:133`, `PromotionFeatures.cs:116`; `modules/ERP.Modules.Distribution/Features/RouteFeatures.cs:124-128` (read), `WholesaleFeatures.cs:96`; `modules/ERP.Modules.Pos/Features/RegisterFeatures.cs:144` (entity checked only through the warehouse, `:112`, so not when no warehouse is given).
Approvals: `ApprovalRequest` carries `LegalEntityId` but is not `ILegalEntityScoped`, so `src/ERP.Application/Workflow/ApprovalFeatures.cs:42-48` and `WorkflowServices.cs:28-36` let a restricted approver list and decide requests of any company. `ApprovalFeatures.cs:55` also stores an unchecked `LegalEntityId`.
Exploit: a user restricted to Company A posts an engagement or store with `LegalEntityId` = Company B. It lands in B's records and reports, and the author can no longer see it. An approver restricted to A approves B's requests. A foreign workspace's entity id is also accepted, leaving an orphan row (no cross-tenant read results).
Smallest correct fix: one central check in `AuditSaveChangesInterceptor.Process`: for `ILegalEntityScoped` rows in `Added` or `Modified`, throw if `org.AllowedLegalEntityIds` is set and does not contain the row's `LegalEntityId`, and add `ILegalEntityGuard.RequireAsync` to the handlers above for the existence check. Make `ApprovalRequest` scoped, or filter it explicitly in list and decide.

---

#### Medium

- **M1. Cross-tenant foreign keys are stored unchecked in several commands; the database cannot stop them.** Reads stay filtered, so I found no read-through, but references are corrupted and fail later. Only 2 of 111 foreign keys include `OrgId` (catalogue query). See the sample table below. Fix: `IReferenceCheck.AllExistAsync` at each gap; composite `(OrgId, Id)` keys for new tables.
- **M2. Password reset links are logged wherever email is off, and the shipped production config has it off.** `src/ERP.Infrastructure/Auth/AuthService.cs:41` logs the live link at Information whenever `email.IsEnabled` is false, despite the "Dev-only" label. `src/ERP.Api/appsettings.Production.json:11` sets `Email.Enabled: false`; the minimum level is Information (`appsettings.json`). Anyone with log access requests a reset for a target and uses the link. Fix: log the link only in the Development environment.
- **M3. Revocation is slow or lossy.** Permissions live in the token. Role and role-permission changes do not revoke sessions (`SecurityFeatures.cs:113-144`), so old rights last up to the 5-minute lifetime. Token validation never reads membership (`src/ERP.Api/Program.cs:85-98`). The deny and cutoff entries live in an in-process cache (`Program.cs:65`), so a restart or a second instance loses them; a deactivated member keeps access for up to 5 minutes in those cases. Refresh does re-check membership (`AuthService.cs:311-312`). Portal tokens last 60 minutes with no revocation path (`PortalAuth.cs:27,64-71`; `Program.cs:91`), and hub connections outlive deactivation. Fix: revoke on role change, use a shared cache before scaling out, check party status for portal tokens or shorten them.
- **M4. POS writes bypass the audit trail.** `ExecuteUpdate` skips the interceptor: `modules/ERP.Modules.Pos/Features/SessionFeatures.cs:257,300,303`, `ReturnFeatures.cs:199`, `OrderFeatures.cs:556,727`. The tenant filter still applies, since they run through `Tracked(...)`. Session close and cash totals leave no before/after event and no `UpdatedBy`. Fix: write an explicit audit event at those points.
- **M5. Attachment vault lists everything to every role.** `src/ERP.Api/Endpoints/WorkflowEndpoints.cs:56` returns file name, description, category and owner for all attachments, including employee documents, on `core.attachments.read`, which is in `CoreRead` (`Templates.cs:96-101`), so Viewers have it. Download is properly gated by owner permission (`AttachmentFeatures.cs:98-118,148-151`). Fix: filter vault rows by the owner resolver's read permission.
- **M6. Company and branch isolation is shallow on reads.** Org-wide rather than company-scoped: `Party` (with IBAN), `Account`, `Item`, `Attachment`, `AuditEvent`, `MessageLog`, `Department`, `JobOpening`, `Candidate` (`modules/ERP.Modules.Hr/HrEndpoints.cs:225,231`, with email and expected salary). There is no per-user branch (business unit) restriction anywhere: any member with a permission sees all branches. No rows means all companies (`src/ERP.Domain/Identity/Identity.cs:36`; `AuthService.cs:428`; `src/ERP.Infrastructure/Services/Context.cs:27`), which is fail-open. Access can be set only at invite (`SecurityFeatures.cs:187-191`); no endpoint edits it. Fix: an endpoint to edit entity access, plus a branch-access model if multi-branch customers need it.
- **M7. Sensitive data at rest is not protected.** Authenticator keys and recovery codes sit in clear in the token table. IBAN, identity numbers and salary are plain columns. Only AI provider keys are encrypted. Data protection keys are encrypted only on Windows (`src/ERP.Infrastructure/DependencyInjection.cs:80`). Attachments are plain files, and deleting one soft-deletes the row and leaves the file (`AttachmentFeatures.cs:156-163`, `CoreServices.cs:98-115`). Connection strings use `TrustServerCertificate=True`.
- **M8. No second line of defence for tenancy.** Zero row-level security policies (catalogue query). The filter lives in one place, and a new `IgnoreQueryFilters` has no gate: the "allow-listed" comments have no enforcing test (`tests/ERP.UnitTests/ArchitectureTests.cs` does not mention it). Child tables without `OrgId` have no filter (see part 1). Default config uses a highly privileged database login (`appsettings.json:4`). Fix: an architecture test for `IgnoreQueryFilters`, and for direct repository use of unfiltered tables.
- **M9. Account lockout is a remote denial of service, across workspaces.** Five wrong passwords lock the global account for 15 minutes (`AuthService.cs:77-82`, also through sign-up `PlatformServices.cs:106-110`). IP-based limits are the only other control. `/auth/refresh` has no limit, and there is no per-tenant API throttle (`Program.cs:120-139`).
- **M10. Email enumeration on public sign-up.** "This email already has an account" (`PlatformServices.cs:105`; `OnboardingService.cs:98`), while login, forgot-password and portal flows are careful not to reveal this.
- **M11. Platform support can take over accounts without tenant visibility.** `ConsoleFeatures.cs:561-570` issues a temporary password returned in the response. It revokes sessions and writes platform activity (good) but leaves no entry in the client's own audit log, has no reason or dual control, and is not forced to change (see C1). Accounts with two-factor stay protected, and platform admins are excluded. There is no impersonation feature (verified). Fix: mirror it into the tenant audit log with a mandatory reason.

#### Low

- **L1.** `SwitchWorkspaceAsync` omits `user.IsActive` (`AuthService.cs:283`). Moot today: nothing in `src` ever sets `AppUser.IsActive = false`, so global account deactivation is not reachable.
- **L2.** Hub groups are by user, not workspace (`src/ERP.Api/Infrastructure/NotificationsHub.cs:14`), so a multi-workspace user gets another workspace's toast in the current session. Their own data only.
- **L3.** Portal: one-time code hash is SHA-256 of a 6-digit code plus party id, cheap to brute force from a database copy (`PortalAuth.cs:40`). Party email is not unique, so the first match wins (`PortalAuth.cs:82`).
- **L4.** `Seed:Enabled` has no environment guard. If turned on in a shared environment, `src/ERP.Api/ProgramSeeder.cs:160-177` grants fixed demo emails Org admin in every workspace, with known dev passwords. Currently off in Staging and Production config.
- **L5.** Refresh tokens are not bound to device. `UserAgent` is never populated. Replay within the 20-second overlap window (`AuthService.cs:299`) does not trigger family revocation.
- **L6.** No content security policy header (`Program.cs:188-194`). `AllowedHosts: *`.
- **L7.** Email channel logs message bodies (including portal codes) at Debug when email is off (`src/ERP.Application/Reminders/Reminders.cs:52`). Off at the default level.
- **L8.** Suspected, not traced: `RunImport` checks permission but not module-enabled (`src/ERP.Application/Imports/Imports.cs:155`, compare `:107`), so a disabled module's import stays reachable until the caller's token rotates.

---

#### 1. How OrgId scoping works

- **Context.** `OrgContext` takes `OrgId` from the JWT `org` claim, or a trusted `Use(orgId)` override (`src/ERP.Infrastructure/Services/Context.cs:40-60`). Client headers cannot set the org. `X-Entity-Id` is validated against the workspace and allowed set (`src/ERP.Api/Infrastructure/Middleware.cs:22-36`).
- **Filter.** `ApplyCrossCuttingConventions` runs after each module's `ConfigureModel`, so module entities register automatically by implementing `IOrgScoped`, `ILegalEntityScoped` and `ISoftDelete` (`AppDbContext.cs:381,499-556`).
- **Null context.** `CurrentOrgId` becomes `Guid.Empty`, so reads return no rows (fail closed, `AppDbContext.cs:33`). Writes throw "No workspace context" (`AuditSaveChangesInterceptor.cs:57-58`).
- **SaveChanges.** Stamps `OrgId`, blocks moving rows between workspaces and cross-workspace writes (`:51-64`), and converts deletes to soft deletes (`:82-87`). `ExecuteUpdate` bypasses all of this, but obeys filters. Note: with a null context, `:62` does not block an explicitly set `OrgId`; this is relied on by trusted flows only.

**Filter bypasses judged.** No raw SQL or `FromSql`/`ExecuteSql` in production code; only in tests.

| Location | Judgement |
|---|---|
| `src/ERP.Infrastructure/Persistence/ReferenceCheck.cs:15` | OK for tenancy (explicit `OrgId`). Ignores soft delete and company by design, so a restricted user can reference another company's row (feeds H4) |
| `AuthService.cs:408` | OK: explicit `UserId`, needed before an org exists |
| `PortalAuth.cs:78`, `ScheduledJobRunner.cs:42` | OK: `Organization` has no org filter, so effectively no-ops |
| `PlatformServices.cs:67-91,136,152` | OK: explicit org predicates, reachable from the platform console and subscription flows |
| `ProgramSeeder.cs`, `ProgramDemoTopUp.cs:286`, `DevSeeder.cs:41` | OK: seed only, gated by `Seed:Enabled` (see L4) |
| `ExecuteUpdate` in `AuthService.cs:289,304,326,338` | OK: refresh tokens, explicit keys |
| `AssistantUsageStore.cs:51-53` | OK: platform table |
| POS `ExecuteUpdate` | Tenant safe, audit gap (M4) |

**Tenant data outside `OrgEntity`.**

| Table | Assessment |
|---|---|
| `InvoiceLine`, `BillLine`, `JournalLine`, `BudgetLine`, `SettlementAllocation`, `PayrollLine`, `ReviewCompetency`, `StockCountLine`, `InspectionItem`, `RolePermission` | No `OrgId` and no filter (catalogue query). All direct repository queries reviewed: keyed by a parent id already filtered (`ChequeFeatures.cs:176,248`, `EmployeePeople.cs:87`). Safe today; one new `Query()` would cross tenants (M8) |
| `RefreshToken` | Has `OrgId`, no filter by design; accessed by hash or explicit user and org |
| `WorkspaceSubscription`, `SubscriptionInvoice` | Have `OrgId`, not `IOrgScoped`. `GetMyInvoice` correctly adds `i.OrgId == orgId` (`BillingFeatures.cs:93`) |
| `PlatformActivity`, `AiProvider`, `Brand*`, `SignupRequest`, `PlatformSetting`, `SubscriptionPlan` | Platform tables, console or public flows only |
| `Currency` | Global reference, read-only endpoint, no writer except onboarding |
| Users, tokens, claims, reset tokens | Global accounts. Root of C1 and H1 |

#### 2. Cross-tenant references (command sample)

| Command | Evidence | Verdict |
|---|---|---|
| Invoice create | `src/ERP.Application/Finance/Invoicing.cs:59-71` | Entity, party, tax, item, account, unit all checked. `SourceType`/`SourceId` not bindable (`InvoiceFeatures.cs:13-15`). `CreditNoteForId` is set by the handler from the source invoice (`InvoiceFeatures.cs:129,234`) |
| Bill create | `BillFeatures.cs:209-224` | All checked |
| Vendor payment | `PaymentFeatures.cs:90-93` | Entity, party, bank checked |
| Manual journal | `JournalFeatures.cs:167-171` | Accounts checked. **`PartyId`, `BusinessUnitId` not checked** |
| Sales order | `modules/ERP.Modules.Sales/Features/OrderFeatures.cs:173-174,213,427` | Entity checked. **`QuoteId`, `CustomerPartyId`, line `ItemId` not checked** |
| Sales quote | `QuoteFeatures.cs:156,193,269,317` | **`CustomerPartyId`, line `ItemId` assigned from input** (no check seen, not traced fully) |
| Purchase order | `modules/ERP.Modules.Purchasing/Features/OrderFeatures.cs:308,462` | Vendor checked. **Line `ItemId` not checked** |
| Stock count, stock posting | `modules/ERP.Modules.Inventory/Features/StockCountFeatures.cs:53,76`; `StockFeatures.cs:198,208-210` | Checked |
| Manufacturing work order, BOM | `WorkOrderFeatures.cs:155-164`; `BomFeatures.cs:167,176` | Checked, including BOM and warehouse company match |
| Employee, loan | `modules/ERP.Modules.Hr/Features/EmployeePeople.cs:50-70`; `LoanFeatures.cs:75-77` | User, manager, department, employee checked |
| Lease | `modules/ERP.Modules.RealEstate/Features/LeaseFeatures.cs:367-371` | Unit, tenant, tax checked |
| Real estate work order, deal | `WorkOrderFeatures.cs:188-193`; `DealFeatures.cs:133-137` | Checked |
| CRM opportunity | `modules/ERP.Modules.Crm/Features/OpportunityFeatures.cs:136,139` | Checked |
| Services engagement | `EngagementFeatures.cs:107-125` | **Entity, `ClientId`, `LeadConsultantUserId` not checked** |
| Retail store | `StoreFeatures.cs:89-95` | **Entity, `WarehouseId` not checked** |
| Distribution route | `RouteFeatures.cs:124-149` | **Entity, `WarehouseId`, stop `CustomerPartyId` not checked** |

**Unique indexes.** Catalogue query of tables with `OrgId` found 7 unique indexes without it. Five are keyed by `LegalEntityId` (`crm_Leads`, `crm_Opportunities`, `svc_Engagements`, `svc_Milestones`, `svc_Retainers`), which is effectively scoped; `RefreshTokens.TokenHash` and `SubscriptionInvoices.Number` are global by design. No cross-tenant collision oracle found.

#### 3. Company and branch isolation

Companies: `UserLegalEntityAccess` becomes token claims, the filter applies to `ILegalEntityScoped` tables (66 entities), and `LegalEntityGuard` protects writes where used. Core finance uses the guard on every entity-bearing command (`Invoicing`, `BillFeatures`, `PaymentFeatures`, `ReceiptFeatures`, `ChequeFeatures`, `BankFeatures`, `BudgetFeatures`, `JournalFeatures`, `Posting`); gaps are in modules and approvals (H4). Reports pass an optional entity id on top of the filter, so a disallowed id returns nothing. Branches: no restriction exists; `BusinessUnitId` is a reporting tag only (M6).

#### 4. Authentication

- **Tokens.** HS256 with algorithm allow-list, 5-minute access, key at least 32 bytes or startup fails, empty default (`Program.cs:60-62,69-76`). Claims: sub, jti, issue time, email, name, org, permission list, entity list.
- **Org switch.** Checks membership, denies the old jti, revokes that workspace's refresh tokens (`AuthService.cs:281-291`).
- **Refresh.** Hashed, rotated, family reuse detection, re-resolves membership and permissions (`AuthService.cs:293-316`). Cookie is HttpOnly, Secure, SameSite=Strict, path-limited, with a required custom header (`AuthEndpoints.cs:62,136-141`).
- **Removed member.** Membership deactivation revokes at once on a single instance (`SecurityFeatures.cs:158`); caveats in M3. Organization `IsActive` is honoured at login and refresh but never set; suspension uses the subscription gate with 30-second caching (`SubscriptionGateMiddleware.cs`).
- **Two-factor.** Enforced by default outside Development, replay-safe, lockout counted, challenge bound to password fingerprint (`AuthService.cs:191-200`; `TwoFactorChallenges.cs:31`). Secrets issue in H1.
- **Passwords.** 10+ characters, mixed case, digit, not the email (`DependencyInjection.cs:62-65`). No breach check. Seed passwords exist only in the Development config. `Platform:FounderPassword` is passed once through the environment by the deploy script.
- **CORS and limits.** Explicit origins (`Program.cs:162-164`). Login-class limiter on all anonymous auth routes. Forwarded headers trusted from loopback only.

#### 5. Authorization surface

- **Guard test.** `tests/ERP.IntegrationTests/EndpointAuthorizationTests.cs:11-23` proves every route has authorization metadata, not that it has a permission. A fallback policy also covers strays (`Program.cs:106-111`).
- **Endpoints with auth but no endpoint-level permission** (scan of all map calls): custom field form and values, dashboard, search, imports, settings PUT, approvals list/submit/withdraw, notifications, HR self-service, leave approve/reject, real estate status and lease routes, projects `/config`. Those carry a handler-level rule: `IRequirePermission` with a dynamic key (`LeaseFeatures.cs:55-58`, `UnitFeatures.cs:69-72`), per-key checks (`SettingsFeatures.cs:83-85`, `CustomFieldFeatures.cs:114,243`, `Imports.cs:107,155`), own-record scope (`NotificationFeatures.cs:25-44`, `SelfServiceFeatures.cs`), or manager/permission logic (`LeaveFeatures.cs:444-453`). Exceptions with no check, open to any staff member: import templates (`Imports.cs:113`, column headings only) and `GetProjectsConfig` (`ProjectInsightFeatures.cs:95`, content not reviewed).
- **`IRequirePermission`.** Pipeline check on every send, covering in-process calls (`src/ERP.Application/Behaviors/Behaviors.cs:36-45`). `MapPagedList` applies policy and query permission (`src/ERP.Modularity/ApiEnvelope.cs:87-97`).
- **Module disabled.** Gate runs before authorization (`Modules.cs:108-126`), and disabled-module permissions are dropped at token issue (`AuthService.cs:424-426`).
- **Wildcard.** `"*"` expands to all keys at issue (`CoreServices.cs:29-34`), escalation in H2.
- **Print and export.** Every print and PDF route has its document permission (`PrintEndpoints.cs`); the handlers use filtered repositories. Bank and wage files need `P.PayrollPay` (`HrEndpoints.cs:99-100`). There is no server-side bulk export.
- **Attachments.** Owner resolver gates list, upload, download, delete. Extension allow-list, derived content type, random storage key, path guard (`CoreServices.cs:117-123`). Gap in M5.
- **Hub.** `[Authorize]` uses the staff policy and joins only the caller's group (`NotificationsHub.cs:8-16`).
- **Jobs.** Each workspace gets its own scope and `Use(orgId)`, skips blocked workspaces, and checks module enablement (`ScheduledJobRunner.cs:44-60`).
- **Caches.** Org id is in module and subscription keys (`CoreServices.cs:67`, `PlatformServices.cs:35`). Assistant cooldown is keyed by provider and holds no tenant data.
- **Portal.** Separate claim, excluded from all staff policies (`Security.cs:22`, `Program.cs:108`). Every handler binds to the claim's party (`Portal.cs:61-63`, `PortalFeatures.cs`, `VendorPortalFeatures.cs`). Code limits: 10 minutes, 5 attempts, 5 per hour.
- **Platform admin.** Policy on the whole group (`PlatformAdminEndpoints.cs:44`), read from the database on every request (`Security.cs:34-40`). Console reads usage and membership, not business records.
- **Search.** Core and all 12 module search files contain permission checks (HR and core read, the rest by scan), plus module enablement (`Search.cs:33`), under the normal filter.
- **Assistant.** Tools go through the mediator, so the pipeline, filter and catalog gate apply (`WorkspaceCatalog.cs:53-77`, `WorkspaceTools.cs:85-99`). Readers project safe fields. The model can only propose; a person confirms and the permission is checked again (`WorkspaceAssistantFeatures.cs:125-157`). Conversations are bound to the user. Tool output is sent to an external AI service, disclosed in the module description.

#### 6. Sensitive data and healthcare readiness

Already good: IBAN and identity numbers masked without `core.sensitive.read` (`PartyFeatures.cs:133,153-156`, `EmployeePeople.cs:24`), salary gated in the API, secrets never logged except M2 and L7, no request-body logging, generic error text outside Development (`Middleware.cs:57`).

**Controls missing before storing patient data:**

| Control | Current state | Needed |
|---|---|---|
| Field-level encryption | None except AI keys | Per-tenant envelope encryption for identifiers and clinical fields, key rotation, encrypted attachments |
| Read audit | Writes only (`AuditSaveChangesInterceptor`), reads leave no trace | Append-only access log: who, which patient, when, why; break-glass with reason and review |
| Audit containment | Snapshots copy everything in clear (H3) | Clinical fields excluded or tokenized; audit read gated per record type |
| Consent | No model | Consent records by purpose, enforced at read |
| Retention and erasure | Soft delete forever, audit forever, files kept (M7). Only assistant chats have retention | Per-record-type retention, legal hold, verified purge including files and audit |
| Patient-level access | Company level at best, no branch or care-team rule (M6) | Row-level rule by care relationship, default deny |
| External AI | Assistant sends tool output to a provider | Hard off for clinical data, or contractually cleared provider plus field exclusion |
| Session and account | 30-day persistent refresh, no idle timeout, global accounts (C1) | Idle timeout, re-authentication for sensitive reads, C1 fixed first |
| Transport and storage | `TrustServerCertificate=True`, plain files | Validated database TLS, encrypted backups and volumes |
| Support access | Temporary password flow (M11) | Time-boxed, approved, tenant-visible access |

#### What is already good

- Tenant filter generated centrally, automatic for all module entities, fail-closed on null context, and backed by a write-time cross-workspace block.
- No raw SQL in production code. Every `IgnoreQueryFilters` carries an explicit predicate or is seed-only.
- Fallback authorization policy, guard test, pipeline re-check, portal fully separated from staff policies.
- Strong session mechanics: 5-minute tokens, rotating hashed refresh tokens with reuse detection, strict cookies, password change ends all sessions.
- Two-factor on by default with replay protection and shared lockout. Login, forgot-password and portal flows avoid enumeration and equalise timing.
- Signing key required from the environment, no seeded credentials or API docs in Staging or Production config.
- Platform admin checked per request, no impersonation tokens, activity log.
- Portal bound to the party claim. Assistant cannot act without a human click. Caches carry the org id. Jobs run per workspace.
- Core finance commands validate every reference and entity. Unique indexes are effectively workspace-scoped.
- Self-approval is blocked in the domain (`src/ERP.Domain/Platform/Platform.cs:112`).

#### Not covered

Mobile client, frontend route guards beyond the checks above, country-pack logic, and dependency vulnerabilities. Nothing was run against a live server, so C1, H2 and H4 have not been demonstrated end to end. I recommend integration tests for each as part of the fix; none exist today for cross-tenant foreign keys or role escalation.

---

# Appendix F

### Frontend, navigation, onboarding, settings and dashboards

Branch `claude/infallible-napier-9331dc` (head 2c631b6). Read-only audit, static reading only: nothing was run, no build, no tests. Paths below are relative to `frontend/src/app` unless they start with `backend/`. Items marked (unverified) were inferred from code, not observed running.

Classification key: **G** generic and reusable, **I** industry-specific, **P** partly reusable, **M** missing, **X** incomplete or wrong.

#### 0. Status-casing bug from the earlier note: FIXED on this branch

- Earlier note claimed 22 web files compare PascalCase strings (`'Delivered'`) while the API sends camelCase.
- API policy confirmed: `backend/src/ERP.Api/Program.cs:167` registers `JsonStringEnumConverter(JsonNamingPolicy.CamelCase)` (also `backend/src/ERP.Modularity/AssistantStreamResult.cs:17`). So enums go out as `delivered`, `partlyPaid`, `pendingApproval`.
- Commit `ca36a53` ("Screens now match the camelCase values the API sends, which restores the right actions and colours") rewrote the comparisons. 238 `status === '<lowercase>'` comparisons now exist in non-spec code, e.g. `pages/sales/order-detail.page.ts:86,101` (`'confirmed'|'processing'|'delivered'`). Typed unions follow the same casing: `core/api/sales.api.ts:8-9`.
- Repo-wide grep for comparisons or `case` against a quoted PascalCase word in non-spec `.ts` returns zero status comparisons. Remaining PascalCase hits are all legitimate:
  - audit action strings `Created/Updated/Deleted` (`pages/admin/audit.page.ts:195-262,442-446`, `shared/record-history.component.ts:39,51`, `pages/admin/invoice-detail.page.ts:122,180`, `pages/projects/project-tabs.components.ts:519,550`): the audit API returns plain strings, not an enum (`backend/src/ERP.Application/Audit/AuditLog.cs:14`, `AuditRow.Action` is `string`).
  - CRM `relatedEntityKind` `'Lead'|'Opportunity'|'Customer'` (`pages/crm/interactions.page.ts:233-337`): a free string column, default `"Lead"` (`backend/modules/ERP.Modules.Crm/Domain/CrmModels.cs:102`), not an enum.
  - key names (`'Enter'`, `'Home'`, `'Tab'`), country codes and a few display labels.
- Residual risk (unverified): `shared/thermal-receipt.component.ts:559` falls back to the literal `'Completed'` for a missing status; `core/format/audit-format.ts:33-34` accepts both `'1'|'Issued'` forms. Both tolerate either casing.
- Other items from the earlier note: CRM "$" and "weighted" defect also fixed (`pages/crm/crm-analytics.page.ts:35` uses `format="money"`, caption "expected value, not weighted"). "Pending Close" string no longer present.

#### 1. Route inventory

Counts are lazy `loadComponent` leaves in `app.routes.ts` (268 lines) plus child route files. 63 `redirectTo` entries exist (legacy aliases, mostly `app.routes.ts:50-96`).

| Area | Routes (pages) | Guard | Notes |
|---|---|---|---|
| Public / auth | 12 (login, forgot, reset, portal x3, print, pricing, terms, privacy, register, register/verify) | guest / none | `app.routes.ts:10-35` |
| Platform console | 11 + 2 invoice docs | `platformAdminGuard` | `pages/platform/platform.routes.ts` (founder-only, not workspace) |
| Core: dashboard, parties(2), items, approvals, security, import, documents, messages, audit, users, roles, settings, modules, custom fields, billing(2), party groups, business units, legal entities, approval rules, reports(1 page, 2 routes) | 28 | permission | `app.routes.ts:47-132` |
| Core finance | 17 (accounts, budgets, journals+detail, ledger reports, invoices+detail, receipts+detail, cheques, bank, bills+detail, payments+detail, tax codes) | permission, no module guard | `app.routes.ts:104-122` |
| inventory | 5 | `moduleEnabledGuard('inventory')` | `app.routes.ts:135-146` |
| hr | 23 | module + permission | `modules/hr/hr.routes.ts` |
| realestate (`/real-estate`) | 16 | module + permission | `modules/realestate/realestate.routes.ts` |
| projects | 13 | module + permission | `app.routes.ts:149-169` |
| sales | 5 | module + permission | |
| purchasing | 5 | module + permission | |
| pos | 7 | module + permission | |
| retail | 4 | module + permission | |
| distribution | 4 | module + permission | |
| manufacturing | 4 | module + permission | |
| crm | 4 | module + permission | |
| services | 4 | module + permission | |

Totals: 94 module routes, 45 core routes (counts approximate, from grep of `loadComponent`), 11 platform routes, 30 spec files / 195 `it(` cases.

Route-level gaps (Defect, `app.routes.ts`):
- `:102` `approvals` and `:129` `admin/import` have no `permissionGuard` (menu entry for import needs `core.parties.write`, `backend/src/ERP.Modularity/CoreMenu.cs`; the API still enforces, so this is UI exposure only). `modules/hr/hr.routes.ts:8` `my` has none (intended self-service).
- `:120-121` all reports, including stock, rent-roll, owner statement and gratuity, are guarded only by `finance.reports.read`, not the module's own permission. Module hiding is done per report (`pages/reports/report-registry.ts:15`), not at route level (unverified for direct URL).
- `:263-264` unknown paths show `placeholder.page.ts` ("Page not found"); `forbidden` also reuses it. Only 2 uses of `PlaceholderPage`, both are error pages, not unfinished features. No feature page is a placeholder.

Page inventory (path: what it does). Core and finance:

| Path | Purpose |
|---|---|
| `/` | Owner dashboard (section 5) |
| `/parties`, `/parties/:id` | Customers and vendors in one list, with detail |
| `/catalog/items` | Item master (goods and services) |
| `/approvals`, `/admin/approval-rules` | Approval inbox; rule setup |
| `/finance/accounts` | Chart of accounts tree plus numbering editor (`pages/admin/coa/`) |
| `/finance/invoices[/:id]`, `/receipts`, `/bills`, `/payments` | Receivables and payables documents with detail pages |
| `/finance/journals[/:id]`, `/finance/cheques`, `/finance/bank` | Manual journals, cheque register, bank reconciliation |
| `/finance/budgets`, `/finance/tax-codes`, `/finance/reports` | Budgets with vs-actual, tax codes, six ledger reports (trial balance, GL, P&L, balance sheet, AR and AP ageing) |
| `/reports[/:slug]` | 16 registry reports (tax return, tax summary, cash movements, cash forecast, owner statement, stock valuation, stock movement, slow-moving, reorder, rent roll, occupancy, portfolio, lease expiries, maintenance costs, commissions, gratuity) |
| `/admin/*` | legal-entities, business-units, party-groups, users, roles, settings, modules, custom-fields, import, documents, messages, audit, billing |
| `/account/security` | Own password and two-factor |

Modules (one line each):
- inventory: stock, movements, counts(+detail), warehouses.
- hr: employees, my, departments, positions, org-chart, documents-expiry, statutory-rules, daily-attendance, attendance, shifts, checklists, tasks, recruitment, loans, reports, payroll(+detail), reviews, review-cycles, leave, leave-balances, leave-types, holidays.
- realestate: properties(+detail), units, unit-types, leases(+detail), acquisitions(+detail), billing, work-orders, renewals, preventive-maintenance, inspections, listings, leads, deals.
- projects: list, tasks, timesheets, billing, board, week, expenses, approvals, reports (includes profitability), resources, change-orders, risks, `:id` detail.
- sales: quotes(+detail), orders(+detail), analytics. purchasing: orders(+detail), requisitions, receipts, analytics.
- pos: terminal, sessions, orders, returns, registers, analytics. retail: stores, pricelists, promotions, analytics.
- distribution: routes, trips, wholesale pricing, analytics. manufacturing: boms, workorders, costing, analytics.
- crm: leads, opportunities pipeline, interactions, analytics. services: engagements, retainers, milestones, analytics.

Calls to endpoints that do not exist: **none found**. Spot-checked 83 frontend calls against backend route definitions (sales 5, pos 14, crm 7, services 4, retail 2, distribution 3, manufacturing 6, purchasing 4, plus 38 core/HR/finance/real-estate/projects calls: ar/ap ageing, balance sheet, trial balance, income statement, general ledger, vs-actual, books-lock, users/active, users/roles, coa-numbering, cheques clear-issued, bank auto-match/book, wps-sif, bank-file, email-payslips, termination-credits, renewals drafts, billing overview and others). The first pass flagged 12 as missing; all 12 were route-syntax mismatches in my grep and are present (e.g. `BudgetEndpoints.cs:24`, `LedgerEndpoints.cs:32`, `SecurityEndpoints.cs:37-38`, `RealEstateEndpoints.cs:207`). Caveat (unverified): existence only, not request/response shape or permission parity. One shape caveat: services KPIs require `legalEntityId` (`ServicesEndpoints.cs:169`) and manufacturing variance requires `itemId` (`ManufacturingEndpoints.cs:75`).

Mock or hardcoded data in pages: no mock arrays, no `Math.random` data (only confetti in `core/format/celebrate.ts`). Findings:
- Marketing fixtures only: `pages/pricing-mobile.component.ts:206-210` fake customer names and invoice rows on the public front page (acceptable, but contains invented company names).
- Country-specific default currency hardcoded to `'QAR'` in a core path: `core/services/workspace-currency.service.ts:18`, `pages/print.page.ts:1615,1629`, `shared/party-statement.component.ts:125`, option list `pages/retail/pricelists.page.ts:359`. Violates "country-neutral core".
- Country tax-id labels hardcoded: `pages/print.page.ts:1315` and `pages/admin/invoice-sheet.component.ts:33` (`AE/SA -> TRN`, `IN -> GSTIN`, `PK -> NTN`, `GB -> VAT no.`).
- Industry vocabulary inside core UI (AGENTS.md hard rule): `pages/dashboard.page.ts:1699-1730` classifies KPIs into `real-estate` and `hr` buckets by hardcoded key names; `pages/reports/report-registry.ts:72-153` core registry holds rent roll, occupancy, commissions, owner statement; `pages/admin/settings.page.ts:17` hardcoded `MODULE_NAMES` (hr, realestate, crm, pos only); `layout/sidebar-nav.ts:10-28` group-to-icon map names every module.

#### 2. Navigation

Mechanics:
- Server-driven. `layout/shell.component.ts:1589-1590` loads `GET /navigation/menu` after `me` loads; failure yields an empty menu (silent).
- Backend `backend/src/ERP.Modularity/Features/SessionFeatures.cs:55-69` builds it: `CoreMenu.Items` (hardcoded list, `backend/src/ERP.Modularity/CoreMenu.cs`) plus each module's `Menu`, then keeps only modules that are core or enabled, then drops items whose permission the user lacks, groups by `GroupId`, orders by `GroupOrder`.
- So the menu is module-aware and permission-aware; a business sees only enabled modules. Items carry a single permission each (no "any of" support, so `projects/approvals` style routes that use `anyPermissionGuard` cannot be expressed in the menu).
- Disabled-module direct URL: `moduleEnabledGuard` (`core/guards/access.guards.ts:25-35`) is a `canMatch` guard that shows a warning toast ("The {module} module is not enabled for this workspace.") and redirects to `/`. API calls return 403 `module-disabled`, handled by `core/http/module-disabled.interceptor.ts`. Module data is kept.
- Permission-missing direct URL: `permissionGuard` redirects silently to `/` with no toast (`access.guards.ts:17-19`). Inconsistent with the module case; the `forbidden` page exists but is never targeted.

Hardcoded in the frontend:
- `layout/sidebar-nav.ts:10-28`: icons for 17 group ids (fallback icon `appstore`, so new modules still work).
- Quick Create dropdown, `layout/shell.component.ts:1208-~1300`: static links to invoices, quotes, sales orders, purchase orders, goods receipts, bills, journals, receipts, payments, accounts, POS terminal, items, work orders, real-estate leases. No `@if`, no permission or module check (Defect). A workspace without sales still gets "Quotation"; the click lands on the guard toast and redirect. Core UI naming real-estate and manufacturing destinations also breaches the neutrality rule.
- Arabic menu labels are looked up by key `menu.<id>` and `menu.group.<id>` with English fallback.

Can core finance, HR etc. be hidden?
- Finance, parties, items, reports, setup are in `CoreMenu` and core routes carry no module guard: **cannot be hidden** by module toggling. Only role permissions hide them. A business that needs no accounting still sees Finance & Accounting, Budgets, Tax codes, Cheques. (Classification X for "optional core areas".)
- HR is a normal module (`hr`) and can be switched off; inventory too.

Industry-aware navigation: **no**. `IndustryCode` exists in `backend/src/ERP.Domain/Organizations/Organizations.cs:11` (default `generic`), flows to `MeWorkspace` (`SessionFeatures.cs:11,46`), the login workspace list (`pages/login.page.ts:723` shows it as text) and platform console. Nothing in the menu, dashboard or guards reads it. Industry is used once at onboarding to pick suggested modules and seed accounts, roles and sequences (`backend/src/ERP.Infrastructure/Services/OnboardingService.cs:39-67`), then becomes a label. Nav and dashboard adapt to enabled modules only.

#### 3. Onboarding UI and modules page

`pages/register.page.ts` (360 lines): fields business name, sign-in key (live availability check), industry, country, currency, contact name and email, optional plan and billing cycle. Result panel `pages/register-result.component.ts` shows one of four states: provisioned (workspace code plus Sign in), pendingEmail, pendingReview, default. `/onboarding` redirects to `/register` (`app.routes.ts:30`).

Industry choices (`GET` list from `ListIndustries`; `backend/src/ERP.Application/Templates/Templates.cs:125-132` plus module templates; later key wins on duplicates, `OnboardingService.cs:26-27`):

| Key | Label | Suggested modules | Source |
|---|---|---|---|
| generic | General business / other | none (core only) | core |
| retail, distribution, manufacturing | Retail and e-commerce / Wholesale and distribution / Manufacturing | multi-module bundles | core, flagged `Preview`, so shown as "(coming soon)" and disabled (`register.page.ts:154,322`) |
| services | Professional services | `projects` | Projects module overrides the core "Preview" entry to Available |
| realestate, trading, crm, procurement, hr-only, retail_pos, retail_chain, fmcg_distribution, light_manufacturing, professional_services | one each | only that single module (`[Key]`) | module templates, all `Available` |

Observations:
- 15 distinct keys, mixing generic ("trading", "procurement") and vertical ("realestate", "fmcg_distribution") entries. Most are 1-module bundles, so "industry" is effectively "first module". Three core entries (retail, distribution, manufacturing) are unreachable because the module-supplied variants have different keys (`retail_chain`, `fmcg_distribution`, `light_manufacturing`) (Defect: dead entries in `Templates.cs:128-130`; classification X).
- Changing industry later: **not possible**. No endpoint writes `IndustryCode` after creation (only `OnboardingService.cs:67`). The platform console only displays it (`pages/platform/platform-client-detail.page.ts:132,245`). The workspace owner can only enable or disable modules (`pages/modules.page.ts`); accounts, roles and number sequences seeded by the first industry stay.
- Modules page (`pages/modules.page.ts`, 96 lines): card grid from `GET /modules`; badges Always on, Enabled, Disabled, Coming soon (planned modules come from `ModuleRegistry.Planned`); toggle then refreshes token and `me`.
- Dependencies: the API returns `requires` (`ModuleView`, `modules.page.ts:15`) and enforces them with 409 "Enable 'x' first." / "Disable a, b first." (`backend/src/ERP.Modularity/Features/ModuleFeatures.cs:66-71`), but the page never renders `requires`; the user learns about dependencies only from the error toast (`modules.page.ts:84`) (Defect, incomplete). No grouping by category either.
- Defect `pages/modules.page.ts:57`: toggle button is gated by `core.settings.write`, but the API requires `core.modules.manage` (`backend/src/ERP.Application/Security/Permissions.cs:35`, `PlatformEndpoints.cs:29`). A role with settings.write but not modules.manage sees a button that returns 403; the reverse role cannot see it.
- Description text says "industry-specific modules and functional packs" (`modules.page.ts:39`): wording only, no split.

#### 4. Settings UI (admin pages)

| Setting area | Status | Evidence | Class |
|---|---|---|---|
| Generic key/value workspace settings (bool, text, number) grouped by module | Exists | `pages/admin/settings.page.ts`, JSON-typed settings are filtered out (`:~104`) | G |
| Number series (invoice, order, lease...) | **Missing UI**; server uses `NumberingService` (`backend/src/ERP.Application/Common/NumberingService.cs`) with module defaults | no page; only chart-of-accounts numbering has an editor (`pages/admin/coa/coa-numbering-editor.component.ts`) | M |
| Tax codes | Exists, list plus drawer | `pages/admin/tax-codes.page.ts` | G |
| Tax return / country rules | Report only | `report-registry.ts:40` | P |
| Currency and exchange rates | **Missing UI**; API has `/finance/currencies` (`MasterDataEndpoints.cs:81`) and platform currencies list, used by registration only | no admin page | M |
| Custom fields | Exists | `pages/custom-fields.page.ts` | G |
| Approval policies | Exists as rules page plus inbox | `pages/admin/approval-rules.page.ts`, `approvals.page.ts` | G |
| Branches | Covered by legal entities and business units (no "branch" concept; no branch picker in documents beyond entity switcher) | `legal-entities.page.ts`, `business-units.page.ts` | P |
| Document templates (print layout, wording) | **Missing**; print is fixed (`pages/print.page.ts`, 1600+ lines, backend `QuestPdfPrintRenderer`) | none | M |
| Units of measure | **Missing**; `unitOfMeasure` is a free-text string on lines (`core/api/sales.api.ts:15`) | none | M |
| Item categories / groups | **Missing**; only party groups exist (`party-groups.page.ts`) | none | M |
| Users, roles and permissions | Exists | `users.page.ts`, `roles.page.ts` | G |
| Email templates and message log | Log only | `messages.page.ts` | P |
| Data import | Exists | `import.page.ts` | G |
| Audit log | Exists (rebuilt in recent commits) | `audit.page.ts` | G |
| Subscription and billing | Exists | `billing.page.ts` | G |
| Books lock, bank accounts | Exists inside entities and bank pages | `core/api/admin.api.ts:212` | G |
| Module settings (e.g. payroll approval flag, POS options) | Appear in generic settings via module setting declarations (`HrModule.cs:74`) | settings page groups | G |

#### 5. Dashboards

Backend: `GetDashboard` (`backend/src/ERP.Application/Dashboard/Dashboard.cs:151-175`) iterates every `IDashboardContributor`, skipping non-core ones whose module is disabled; merges KPIs, charts and alerts. Endpoint `GET /dashboard?legalEntityId&from&to` (`DashboardEndpoints.cs:13`). Validator caps range at 5 years and forbids future end date. All KPI numbers are real queries (no constants found); only core finance honours the chosen period.

Core contributor (`FinanceDashboard.cs`):

| KPI / card | Source | Respects period |
|---|---|---|
| receivables (+overdue caption) | issued and part-paid invoices, credit notes netted (`:30-36`) | no (as of today) |
| collected in period | issued received settlements (`:45`) | yes |
| payables | issued and part-paid bills (`:66`) | no |
| cash and bank | journal lines on bank and cash accounts to period end (`:72`) | to end date |
| cheques in custody, due and bounced alerts | instruments table (`:81-92`) | no |
| net result for period | income less expense journal lines (`:96-112`) | yes |
| pending approvals | approval requests (`:116`) | no |
| charts: billed vs collected, receivables ageing, expenses by group | same queries | first and third yes |

Module contributors (all real queries; none uses `scope.From/To` except HR and real estate for dates; "lifetime" labels admit it):

| Module | Cards (key: label) |
|---|---|
| inventory | stock-value; low-stock alert (`StockFeatures.cs:249`) |
| sales | open-sales-orders, total-sales-volume ("lifetime order book"), open-sales-quotes, pipeline chart |
| purchasing | open-purchase-orders, pending-requisitions, status chart |
| pos | pos-today-sales, pos-open-sessions |
| retail | active stores, active promotions |
| distribution | routes, trips en route |
| manufacturing | active boms, open work orders, status chart |
| crm | new leads, active deals, funnel chart |
| services | active engagements, ready-to-invoice milestones |
| projects | active projects, open and overdue tasks, unbilled hours (+value), timesheets to approve, task chart |
| hr | employees (+payroll cost caption), documents expiring, payroll open, reviews pending, leave pending, headcount chart, expiry alerts |
| realestate | portfolio value, occupancy, contracted rent, leases expiring, open work orders, occupancy and expiry charts, lease alerts |

Frontend `pages/dashboard.page.ts` (1951 lines): fetches once via `DashboardApi.get` (`:1931`); no KPI value is computed or hardcoded client-side. It localises captions, and groups cards into tabs (finance, sales-crm, supply-operations, projects-services, real-estate, hr) using **hardcoded key-name matching** (`:1699-1730`, charts `:1895-1905`). A new module's KPI with an unfamiliar key falls into "finance" (Defect: fragile; the server should send a category). Range control and presets are in `dashboard-range*.spec.ts`.

Adapts to industry? **No, only to enabled modules** and the user's permissions (`user.HasPermission` inside each contributor). The same finance cards appear for every business; there is no industry-specific layout, no owner-level ordering by industry.

Owner metrics from the requested list:

| Metric | On dashboard | Elsewhere | Class |
|---|---|---|---|
| Revenue | Billed (net of credit notes) inside "billed vs collected" chart; no revenue KPI card | P&L at `/finance/reports` | P |
| Expenses | Expenses chart and net-result caption | P&L | G |
| Gross profit | No | no cost-of-sales split; grep for "gross profit" finds nothing | M |
| Net profit | `net-result` card | P&L | G |
| Cash flow | `cash` position only | cash-forecast and cash-movements reports | P |
| Receivables / payables | cards plus AR ageing chart; AP has no ageing chart | AR and AP ageing reports | G |
| Inventory valuation | `stock-value` card | stock-valuation report | G |
| Slow-moving stock | No | slow-moving report | P |
| Shortages | low-stock alert | reorder-suggestions report | P |
| Sales performance | order and quote cards | `/sales/analytics` (4 stat cards), POS and retail analytics | P |
| Purchase performance | PO cards | `/purchasing/analytics` | P |
| Employee performance | No | HR reviews and review cycles pages | P |
| Branch comparison | No; entity switcher filters one entity at a time | none | M |
| Budget vs actual | No | `/finance/budgets` vs-actual | P |
| Project profitability | No | `projects-reports` profitability table (`pages/projects/project-reports.page.ts:47-54`) | P (industry-linked) |

Dashboard defects: `core/api/admin.api.ts` KPI formats mix `"money"` and `"currency"` (`SalesDashboard.cs:32`, `PosDashboard.cs:30` use `currency`; core uses `money`) (unverified whether both render identically). Module KPIs ignore the period selector but the page offers it for all cards.

Reports inventory (`pages/reports/`): `report-registry.ts` 178 lines declares 16 data-driven reports rendered by one generic `report.page.ts` (185 lines), with `module` hiding; plus six ledger reports on a tabbed page. Ten reports belong to inventory, real estate or HR but sit under core permission `finance.reports.read`.

#### 6. Conventions for a new page

- List: `<erp-paged-table [list] [columns]>` (`shared/paged-table.component.ts`, used by 81 files) with `createPagedList((q) => api.list(q), { sort })` from `core/api/paged-list.ts`; columns typed `TableColumn<T>` (`key,label,sortField,width,value,filterField,filterOptions`); custom cells through the `#cell` template; toolbar via `<button toolbar ...>`; row click `(rowClick)`. Backend pair: `MapPagedList` with whitelisted fields, no handler.
- Form: `<erp-record-drawer [fields]=FieldSpec[] [value] [saving] [errors] (submitted)>` plus `MasterDataState<T>` (`shared/master-data-state.ts`) for open/close, saving flag, server validation errors and a success toast that names the record. **`ErpFormModule` does not exist** in this repo (zero hits); the brief's assumption is wrong, the drawer is the form convention. Bespoke forms use ng-zorro `nz-form-item` (the a11y lint treats those as labelled).
- Exemplar to copy for a list page and a form: `pages/admin/tax-codes.page.ts` (73 lines, covers both: paged table, status tag, permission-gated New button, drawer form, `MasterDataState`). For a detail page copy `pages/admin/invoice-detail.page.ts` with `shared/detail-page.component.ts`.
- Loading, empty, error: use `erp-state-panel` (`kind="error"`, `actionLabel`, `(action)` retry) as in `pages/modules.page.ts:43`; skeleton loaders used in 21 files (e.g. `modules/hr/org-chart.page.ts`, `pages/admin/audit.page.ts`, `pages/admin/coa/account-panels.component.ts`); success wording through `doneText()` (`core/format/done-text`), problems through `problemMessage`/`asProblem`.
- Permissions: route `canMatch: [permissionGuard('<module>.<resource>.<action>')]`, module routes also `moduleEnabledGuard('<key>')`, buttons `*erpHasPermission`. Menu entry goes in `CoreMenu.cs` or the module's `Menu`, not in the frontend; add the group icon to `layout/sidebar-nav.ts` only for a new group.
- i18n: templates use `| translate: 'English text'` (`TranslatePipe`) or `i18n.t(key, fallback)`; Arabic strings are `['English', 'Arabic']` pairs in `core/i18n/phrases.ar.ts` (about 1050+ lines) and menu keys in `core/i18n/dictionaries.ts`; non-template text via `phraseNow()`/`tNow()` (`core/i18n/translate-now.ts`). Many page headings and button labels in the exemplar are plain English literals (not run through the pipe), so Arabic coverage is partial (unverified how the global phrase mapper handles untranslated literals).
- A11y: `npm run lint:a11y` (`scripts/a11y-lint.mjs`) fails on icon-only buttons without `aria-label`/`title`, `(click)` on non-interactive tags without `erpKeyboardClick`/keydown/tabindex/role, and placeholder-only inputs. Runs in CI (`.github/workflows/ci.yml:75`) before `ng build` and `ng test --watch=false --browsers=ChromeHeadless` (`:76-77`).
- Tests: Karma/Jasmine (`angular.json` `test` target, `@angular-devkit/build-angular:karma`, `tsconfig.spec.json`). 30 spec files, 195 `it(` blocks; coverage is thin (dashboard range/alert text, sidebar nav, profile menu, POS cart, HR waves 5-6, projects wave 2, pricing mobile, two-factor, record links, permission gating, assistant stream, paged list, subscription gate, platform AI providers, portal projects, legal page). No spec for most list or form pages.
- `frontend/node_modules` **does not exist** in this worktree (checked): `npm ci` is needed before `ng build`/`ng test`; nothing was built or run for this audit.
- Documentation drift (Defect): `AGENTS.md` says Angular 19 and ng-zorro 19.3.1; `frontend/package.json` pins Angular `^20.3.34`, ng-zorro `^20.4.4`; `frontend/README.md` says CLI 19.2.27.

#### Classification summary

| Feature | Class |
|---|---|
| Server-driven, permission- and module-aware sidebar | G |
| Module enable/disable with route guard and dependency rules (server) | G |
| Dependency display, correct permission on modules page | X |
| Core finance, parties, items, approvals, audit, roles, import | G |
| Hiding core finance for non-accounting businesses | M |
| Quick Create menu | X |
| Industry onboarding (single pick, seeds modules) | P |
| Changing industry after sign-up | M |
| Industry-aware navigation or dashboard | M |
| Dashboard engine (contributors per module, real queries) | G |
| Dashboard category tabs (hardcoded key names incl. real-estate, hr) | X |
| Owner metrics: gross profit, branch comparison | M |
| Owner metrics: revenue, cash flow, slow-moving, budget vs actual, project profitability, performance KPIs on the dashboard itself | P |
| Reports (registry + ledger) | P (module reports under core permission) |
| Settings: numbering, currency, document templates, units of measure, item categories | M |
| Settings: tax codes, custom fields, approval rules, users and roles | G |
| Branches | P |
| Real estate, HR, projects, POS, retail, distribution, manufacturing, services, CRM pages | I (each a module) |
| Hardcoded QAR default and country tax-id labels in core UI | X |
| Status casing vs API | G (fixed in ca36a53) |

---

# Appendix G

### AI assistant provider architecture

Scope: `main` (2c631b6) and `origin/claude/platform-console` ("branch", unmerged, +35k lines mostly migration designers). Read-only review; nothing was run. Paths are relative to `backend/` unless noted. Branch line numbers refer to the branch copy of the file.

#### Verdict

The provider layer is well built for one protocol (chat-completions-style chat). Key handling is sound. Gaps are cost in money, per-tenant provider choice, and non-chat-completions-style vendors. The branch closes the plan-allowance, per-client limit, 30-day usage and health-status gaps, so merging it matters.

#### Requirements

| # | Requirement | Status | Branch | Evidence |
|---|---|---|---|---|
| 1 | Enable/disable assistant globally | Present | main | Two owner switches, `SetAssistantVisibility`, `src/ERP.Application/Platform/AssistantFeatures.cs:166-180`; refuses to enable with no cleared service (`:170-173`). Workspace status false when off: `modules/ERP.Modules.Assistant/Features/WorkspaceAssistantFeatures.cs:97-101`. Public and workspace are switched separately |
| 2 | Add provider | Present | main | `CreateAiProvider` `:182-193`, console route `src/ERP.Api/Endpoints/PlatformAdminEndpoints.cs:93`. Branch: a new service starts with both uses off until a test passes (`AssistantFeatures.cs` branch `:191-193`, `:210-214`) |
| 3 | Update provider (model, URL, key) | Present | main | `UpdateAiProvider` `:195-210`. A changed address forces a re-entered key (`:202`). Branch also resets clearance and test state on any connection change |
| 4 | Hide provider | Partial | main | No per-provider hidden flag. "Hide" means unticking `UseForBrand` and `UseForWorkspace` (`src/ERP.Domain/Platform/AiProviders.cs:18-21`). Works, but there is no separate "disabled" state |
| 5 | Remove provider | Present | main | `DeleteAiProvider` `:212-220`, hard delete. `AssistantUsageDay` keeps the name (`AiProviders.cs:32-43`) |
| 6 | The four vendors the owner named, and others | Partial | main | One adapter only: `src/ERP.Infrastructure/Services/OpenAiStyleChatClient.cs:13`, posting to `{base}/chat/completions` (`:78`). Presets for several vendors. |
| 7 | Key encrypted at rest | Present, with a caveat | main | ASP.NET Data Protection, purpose `erp.ai-provider-key.v1`: `src/ERP.Infrastructure/Services/AiProviderServices.cs:11-22`. Fixed app name and persisted ring: `src/ERP.Infrastructure/DependencyInjection.cs:76-80`. Ring is DPAPI-wrapped only on Windows (`:80`); on Linux the ring is plain XML on disk (D1) |
| 8 | Key server side only | Present | main | Write-only `ApiKey` input (`AssistantFeatures.cs:22`); view carries only `KeyHint` and `KeyNeedsReentry` (`:13-15`, `:148-149`). Password field in UI (`frontend/.../platform-assistant.page.ts:43,48`). Only used in `AssistantRunner.cs:68,86` and the tester |
| 9 | Key never in logs, queries, DTOs, frontend | Present, no live leak found | main | See leak analysis below |
| 10 | Default provider / fallback order | Present | main | `SortOrder`, `ReorderAiProviders` `:222-231`; runner tries in order with a 429 cooldown (`AssistantRunner.cs:37-44,127-135`). One shared order for both bots; no explicit "default" flag, first in order is the default |
| 11 | Change model without module changes | Present | main | `Model` is a column edited in the console (`:203-204`); modules only see `IChatModelClient`. Callers (`BrandAssistantFeatures.cs`, `WorkspaceAssistantFeatures.cs:231`) never name a model |
| 12 | Availability by tenant / subscription | Partial | both | Main: workspace admin enables module `assistant`, then permission `assistant.chats.use` (`AssistantModule.cs:19-27,71-79`). Plan gating exists only on branch: `AssistantMonthlyMessages` (`src/ERP.Domain/Platform/Subscriptions.cs` branch), 0 hides it (`WorkspaceAssistantFeatures.cs` branch `:130`), monthly cap `:282-288`. Missing on both: per-tenant provider or model, per-tenant on/off by the owner other than module toggle |
| 13 | Usage monitoring | Present (tokens) | main; 30-day on branch | Daily rows per bot and service: `AssistantUsageDay` (`AiProviders.cs:32-43`), shown in console (`AssistantFeatures.cs:150`). Branch adds 30-day totals (`AssistantUsageStore.cs` branch `:47`), health status from real replies (`AssistantRunner.cs` branch `:75,142`) and per-workspace counts (`GetAssistantUsage`) |
| 14 | Cost monitoring | Missing | neither | No price per token on `AiProvider`, no currency, no cost column in usage rows or the console. Tokens only. Token counts are estimated at 4 chars per token when a vendor reports none (`AssistantRunner.cs:181`) |
| 15 | Usage limits | Present | main; extended on branch | Daily tokens per bot (`AssistantRunner.cs:30-36`), per user per day (`WorkspaceAssistantFeatures.cs:194-199`), public 30/hour per IP. Branch adds per-workspace daily messages, plan monthly cap, one reply at a time per user. No per-tenant token ceiling on main: one workspace can consume the shared daily pool |
| 16 | ERP works when AI is off | Present | main | The AI is isolated in the Assistant module plus `AssistantRunner`; no other module calls it (grep: only Assistant module, Brand and Try features). Off or no service: runner emits `no-service`/`off` events (`AssistantRunner.cs:40-44`, `WorkspaceAssistantFeatures.cs:186-190`). UI: toggle hidden unless `/status` is available (`frontend/src/app/layout/workspace-assistant.component.ts:31-44`, `shell.component.ts:1343`); status error also hides it. Public widget falls back to written FAQ answers plus contact form (`docs/ai-assistants.md`). Unit coverage for the off state is in `AiAssistantConsoleTests` (not run) |
| 17 | Tools respect org and permission | Present | main | `AssistantCatalog` checks permission and module per reader/action (`src/ERP.Application/Assistant/WorkspaceCatalog.cs:53-77`); tools take no org id; report and search tools go through MediatR so the pipeline enforces permissions (`WorkspaceTools.cs:85`); model cannot write, only propose, confirm re-checks the permission (`WorkspaceAssistantFeatures.cs:137`). Branch adds legal-entity recheck, view-only handling and HR/payroll exclusion |

#### Key leak analysis

Checked each path; none leaks the key today.

- **DTO**: `AiProviderView` has no key field (`AssistantFeatures.cs:13-15`). Clean.
- **Audit rows of the provider row**: `AiProvider` is in `NotAudited` (`src/ERP.Infrastructure/Persistence/AuditSaveChangesInterceptor.cs:27`), and `EncryptedApiKey` is also in `SecretProperties` (`:21-22`). Two layers. Console activity lines carry names and models only (`AssistantFeatures.cs:190,207,217`).
- **Serilog destructuring**: none configured (`Program.cs:45`, `appsettings.json:34-36`); `UseSerilogRequestLogging` (`Program.cs:187`) logs no bodies. No MediatR logging behavior exists (`Behaviors.cs`). Latent risk only (D3).
- **Provider HTTP errors**: failures map to fixed strings (`OpenAiStyleConnectionTester.Describe`, `AiProviderServices.cs:46-56`; `OpenAiStyleChatClient.cs:39,73,87-88,96-97,103`). The vendor body is never read on failure. The stored `LastFailure` and `LastTestMessage` come from this fixed set. Runner logs only service name and failure kind (`AssistantRunner.cs:124`). Safe.
- **Outbound**: HTTPS-only, public hosts only, no redirects, and the connect callback refuses private IPs after DNS (`AiProviderServices.cs:59-78`, `AssistantFeatures.cs:58-82`). A stored key cannot be sent to an internal host.
- **Writes**: owner-only via `RequireOwnerAsync` (`:139-142`); other platform-team members can only read.

#### Defects, ranked

1. **D1 (medium) Key ring unencrypted on non-Windows hosts.** `ProtectKeysWithDpapi` is Windows-only (`DependencyInjection.cs:80`). On Linux the Data Protection key ring sits as plain XML in `Storage:Root/data-protection-keys` beside the same storage that is backed up; a backup of the folder plus the database decrypts every AI key. Add certificate or cloud-KMS protection and exclude the ring from storage backups. The DPAPI machine scope also means a restore to another machine makes keys unreadable (handled by `KeyNeedsReentry`).
2. **D2 (medium) No money cost tracking or ceiling.** Tokens only; a paid key can burn spend with no figure in the console and no spend cap. Add input and output price per provider, a cost column, and a monthly spend limit.
3. **D3 (low, latent) Records print the secret.** `AiServiceConnection(…, string ApiKey)` (`ChatContracts.cs:14`) and `AiProviderInput(…, ApiKey, …)` (`AssistantFeatures.cs:22`) are positional records, so `ToString()` includes the key. Nothing logs them today, but one `{@Request}` log or exception message would. Override `ToString` or use a redacting wrapper. Also, in Development/Testing the 500 handler returns `ex.ToString()` (`Middleware.cs:57`).
4. **D4 (medium, main only) Workspace tools may send HR and payroll data to the outside service.** The HR exclusion (`PrivateModules`, `PrivateApprovalTypes`) exists only on the branch; on main, search, overview and approvals tools return whatever the user may see, including HR. Docs claim HR has no reader (that is true), but the other tools bypass that. Merge the branch change.
5. **D5 (medium) Only one vendor protocol.** vendors with only a native protocol are not supported; chat-completions-compatible endpoints for both vary on tool calls and streaming usage. Add a vendor field and an adapter per protocol before promising "Gemini, Grok, Anthropic, OpenAI".
6. **D6 (low) No per-tenant control.** Providers, model and daily token pool are global. On main a single workspace can exhaust the shared pool for everyone; the per-workspace daily cap and plan monthly cap only exist on the branch.
7. **D7 (low) Estimated usage and in-memory state.** Token counts are estimated when the vendor reports none, and the 429 cooldown is `IMemoryCache` (`AssistantRunner.cs:24,39`), so it resets per instance and per restart.
8. **D8 (low) No distinct "disabled" provider state** and the first provider silently acts as default; the console shows order only.
9. **D9 (info) Key hint** is the last 4 characters of an 8-or-more-character key (`AssistantFeatures.cs:93,262`); acceptable.

Not verified: tests were not executed; nothing was run against a database or a live provider; Linux hosting is assumed from the general deployment guidance, not confirmed in this repo.
