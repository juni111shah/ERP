# 09 · Example module: Real estate (how an industry module plugs in)

This file is **not part of the core IA**. It shows how the first industry module, built from the the reference demo spec, uses the manifest contract (08 §5). Any other industry module (trading, manufacturing, services…) follows the same steps. Industry terms (property, unit, lease, tenant, PDC, Ejari…) appear only here and inside this module's screens.

Spec sources: §1 scope table, §2.1 glossary, §4.5–4.16, §4.24 (screens), §6.8–6.11, §6.14–6.15 (reports), §7.1–7.2 (permissions/roles), §4.25 (alert settings), §10.2 (routes).

## 1. Manifest (abridged)

Module identifiers (BA `real-estate-module.md` §1, **resolved QA-9**): module key **`realestate`** · UI route prefix **`/real-estate/`** · API base **`/api/v1/m/realestate/`** · database schema **`re`** · permission keys **`realestate.<resource>.<action>`** · custom-field pack prefix `cf_re_`.

The manifest follows the standard `ModuleManifest` in [`../uiux-advanced/specs/app-shell.md` §2](../uiux-advanced/specs/app-shell.md#2-module-manifest-contract) (field names below are that file's; `routePrefix` is the one proposed addition, 08 §5).
```ts
export const REAL_ESTATE_MANIFEST: ModuleManifest = {
  key: 'realestate', routePrefix: 'real-estate',         // routes /real-estate/*; DB schema 're' (backend)
  label: { key: 'realestate.module.label', default: 'Real estate' }, icon: 'home', version: '1.0.0',
  apiBase: '/api/v1/m/realestate',                        // approved module API base
  requires: ['core', 'finance'],                          // invoices, receipts, ledger and cheques are core finance
  industries: [{ key: 'real-estate', label: { key: 'realestate.industry', default: 'Real estate' }, icon: 'home' }],
  countryPacks: ['AE'],                                   // Ejari/Tawtheeq registration labels are UAE-specific (spec §2.1)
  routes: REAL_ESTATE_ROUTES,                             // lazy loadChildren under /real-estate
  nav: [ /* §2 */ ], permissions: [ /* §4, PermissionArea rows, module 'realestate' */ ], roleTemplates: [ /* §4 */ ],
  resources: [
    { key: 'realestate.property', label: { key: 'realestate.property', default: 'Property' }, customizable: true, numberingPrefix: 'PROP' },
    { key: 'realestate.unit',     label: { key: 'realestate.unit', default: 'Unit' }, customizable: true },
    { key: 'realestate.lease',    label: { key: 'realestate.lease', default: 'Lease' }, customizable: true, numberingPrefix: 'LSE' },
    { key: 'realestate.acquisition', label: { key: 'realestate.acquisition', default: 'Acquisition' }, customizable: true },
    { key: 'realestate.workorder', label: { key: 'realestate.workorder', default: 'Work order' }, customizable: true, numberingPrefix: 'WO' },
    { key: 'realestate.deal',     label: { key: 'realestate.deal', default: 'Deal' }, customizable: true, numberingPrefix: 'DEAL' } ],
  extensions: [ /* §5 */ ], settingsSections: [], alerts: [ /* §6 */ ],
  reports: [ /* §7 */ ], dashboard: { widgets: [], kpis: [] /* §7 */ }, searchProviders: [ /* §7 */ ],
  statusTones: { 'lease.active': 'success', 'lease.expiring': 'orange', 'lease.terminated': 'neutral' },
};
```

## 2. Nav contributions (spec §1 sidebar groups, namespaced routes)
| Group (order) | Item | Route | Permission (`requires`) | Spec |
|---|---|---|---|---|
| **Property & asset** (110) | Properties | `/real-estate/properties` · `/:id` | `realestate.properties.read` | §4.5 |
| | Units | `/real-estate/units` · `/:id` | `realestate.units.read` | §4.6 |
| | Owners | `/real-estate/ownership` | `realestate.ownership.read` | BA 9.1 |
| | Acquisitions & funding | `/real-estate/acquisitions` · `/:id` | `realestate.acquisitions.read` (proposal; not in BA 9.1) | §4.7 |
| **Leasing & tenants** (120) | Tenants | `/parties/customers?view=tenants` (core screen + RE system view) | `core.parties.read` | §4.8. Implemented as a saved view, not a duplicate screen. |
| | Leases | `/real-estate/leases` · `/new` · `/:id` | `realestate.leases.read` | §4.9 |
| | Security deposits | `/real-estate/deposits` | `realestate.deposits.read` | BA 9.1 |
| | Registrations | `/real-estate/registrations` | `realestate.registrations.read` | BA 3.8 (country pack) |
| **Facilities** (130) | Work orders | `/real-estate/work-orders` · `/:id` | `realestate.workorders.read` (see coordination-notes C: BA uses `facilities.workorders.*`) | §4.16 |
| **Brokerage** (140) | Deals | `/real-estate/deals` · `/:id` | `realestate.deals.read` (BA uses `brokerage.deals.*`) | §4.24 |
| *(injected into core **Finance**, QA-7)* | Billing run | `/real-estate/billing-runs` | `realestate.billingrun.execute` (+ `finance.invoices.issue` to issue) | §4.11, BA RE-BIL-01 |

Invoices, receipts, bills, payments and cheques are **core finance** screens (08 §4). This module never owns them: it creates core invoices through the billing run and adds source links to core records (§5).

Simple master records (Units, Work orders) use the generic master-data page (06) with drawer forms. Leases, Acquisitions and Deals have full detail pages with tabs (spec §3.3). Their list pages still use `<erp-master-data-page>`.

## 3. Example: Properties list as a master-data config
```ts
export const PROPERTIES: MasterDataConfig<Property> = {
  resourceKey: 'realestate.property', title: 'Properties',
  subtitle: 'Buildings, villa compounds and commercial assets owned by your legal entities.',  // spec §4.5
  api: { list: 'properties', get: 'properties/:id', create: 'properties', update: 'properties/:id', remove: 'properties/:id', export: 'properties/export.csv' },
  // relative to apiBase → /api/v1/m/realestate/properties …
  codeField: 'code', titleField: 'name', entityScoped: true,
  permissions: { read: 'realestate.properties.read', create: 'realestate.properties.manage', edit: 'realestate.properties.manage', delete: 'realestate.properties.manage', export: 'realestate.reports.export' },
  searchPlaceholder: 'Search name, code, community…',
  columns: [
    { key: 'code', label: 'Code', sortable: true, pin: 'left' },
    { key: 'name', label: 'Property', subField: 'community', sortable: true },
    { key: 'legalEntityName', label: 'Entity' }, { key: 'type', label: 'Type' }, { key: 'region', label: 'Emirate' },
    { key: 'unitsLabel', label: 'Units (registered / total)' },
    { key: 'occupancy', label: 'Occupancy', type: 'decimal' /* progress bar cell template */ },
    { key: 'contractedRent', label: 'Contracted rent', type: 'currency' },
    { key: 'indicativeValue', label: 'Indicative value', type: 'currency' },
    { key: 'status', label: 'Status' } ],
  filters: [ { key: 'type', label: 'Type', type: 'singleSelect', quick: true, options: { source: 'enum:PropertyType' } },
             { key: 'region', label: 'Emirate', type: 'singleSelect', quick: true, options: { source: 'countryPack:regions' } },
             { key: 'status', label: 'Status', type: 'singleSelect', quick: true } ],
  rowClick: 'detail', detailRoute: r => ['/real-estate/properties', r.id],
  formMode: 'drawer', form: { sections: [ /* fields from spec §4.5 table */ ] },
  kpis: [], emptyState: { title: 'No properties yet', description: 'Add the buildings and assets your entities own.' },
  extensionPoint: 'realestate.property.list',
};
```

## 4. Permissions and role templates (BA `real-estate-module.md` §9.1)
| Resource (matrix row) | Keys | Spec area |
|---|---|---|
| Properties | `realestate.properties.read` / `.manage` | `portfolio` |
| Units | `realestate.units.read` / `.manage` / `.reserve` | `portfolio` |
| Owners | `realestate.ownership.read` / `.manage` | `investment` |
| Leases | `realestate.leases.read` / `.create` / `.edit` (Draft only) / `.activate` / `.renew` / `.terminate` / `.amend` | `leasing` |
| Security deposits | `realestate.deposits.read` / `.receive` / `.settle` / `.refund` | `leasing` |
| Registrations | `realestate.registrations.read` / `.manage` | `leasing` |
| Billing run | `realestate.billingrun.execute` | `billing` |
| Reports | `realestate.reports.read` / `.export` | `reports` |
| Acquisitions, Work orders, Deals | `realestate.acquisitions.*`, `realestate.workorders.*`, `realestate.deals.*` (**proposals**, kept under the RE module per D1; BA lists work orders and deals under `facilities.*` / `brokerage.*`) | `investment`, `facilities`, `brokerage` |
| PDC register | **Not in this module.** Cheques are core finance (`finance.cheques.*`, 08 §4a). Registering a lease's cheques needs `realestate.leases.edit` **and** `finance.cheques.create`. | `leasing` |
| Tenants | none; uses core `core.parties.*` | `leasing` |

Role templates contributed (`roleTemplates`): **Leasing manager**, **Facilities manager**, **Brokerage manager**. **Leasing manager** grants exactly the BA set (§9.1): all `realestate.*` except `realestate.ownership.manage`; `core.parties.manage`; `finance.invoices.read|create|issue`; `finance.receipts.read|create|allocate`; `finance.cheques.read|create|edit|deposit|replace|return`; the work-order keys; `finance.reports.read`. Clearing, bouncing and outgoing actions (`clear`, `bounce`, `issue`, `stop`, `void`) stay with finance roles. Finance manager and Accountant (core templates) get `realestate.*.read`, `realestate.billingrun.execute` and `realestate.deposits.*` when the module is enabled. Templates appear in Roles as "Built-in · Real estate" and disappear if the module is disabled; users who hold them keep the assignment. While the module is disabled its `realestate.*` grants have no effect, but core grants still apply. Templates are copied once (Q5).

## 5. Extensions into core screens
| Target (extension point) | Contribution | Spec |
|---|---|---|
| `core.legalentity.list` | Columns *Properties*, *Portfolio value*. KPIs *Properties*, *Indicative asset value*. | §4.4 |
| `core.legalentity.detail` | Tab *Properties* | §4.4 |
| `core.party` (customer role) | Category option "Tenant", detail tab *Leases*, and the *Statement* action. The *Cheques* tab is core. | §4.8 |
| `core.cheque` (core Finance) — **source-link hook only (QA-8, BA §3.7 / core §8.7)** | The module adds **no fields** to the core cheque (no Lease or Instalment column on `core.cheque`; SQA denylist). It implements the core source-link hooks: `DescribeSource` (renders the source-link row as "Lease LSE-2026-000123 · Instalment 3 of 4" with a link), `AllocateOnClear` (RE-PDC-04), `OnBounced` (RE-PDC-05), `OnReplaced` (RE-PDC-06) and `OnReturned` (RE-PDC-07). Lease links apply to **incoming** cheques only. On the core cheque detail, the generic **Source links** section shows these rows; the register's generic **Source module** filter lets users pick "Real estate · Lease". On the lease detail page, the **Cheques** tab embeds `<erp-data-table>` for core cheques filtered by source link (`sourceModule=realestate&sourceType=lease&sourceId=…`, `direction=incoming`), with a **Register cheques** action that opens the core bulk registration pre-filled from the rent schedule (RE-PDC-01). The incoming register prefix is renamed `PDC-YYYY-000000` by the RE template. | §4.9, §4.10 |
| `core.invoice` (core Finance) | Source links to lease / instalment (same hook pattern), the "From lease schedule" line source, and the *Billing run* entry point. Invoices stay core. | §4.11 |
| `core.bill` (core Finance) | Property / unit / work-order source links | §4.14 |
| Overview dashboard | KPI cards *Portfolio value*, *Occupancy*, *Leases expiring*, *Open work orders*, *Contracted annual rent*. Charts *Occupancy by property* and *Lease expiries*. | §4.2 |

## 6. Settings and alerts contributed
* Alerts & reminders row: "Warn about lease expiries within" 60 days (spec §4.25). The cheques-due window (7 days) is a core Finance alert.
* Approval document types: none beyond core in v1.
* Country-pack dependent labels: "Ejari / Tawtheeq no." (lease), "DEWA / ADDC premise no." (unit).

## 7. Reports, search and widgets
* Reports group **Portfolio**: Rent roll, Occupancy, Portfolio performance, Lease expiries. Group **Operations**: Maintenance costs, Brokerage commissions (spec §4.20, §6.8–6.11, §6.14–6.15).
* Search providers (`searchProviders`, endpoints under `/api/v1/m/realestate/`): Properties, Units, Leases, Work orders, Deals (spec §4.28 list). Core already provides Customers, Vendors, Invoices, Bills, **Cheques** and Employees.

## 8. Checklist for any new industry module
1. `ModuleManifest` (app-shell.md §2) with a lowercase `key`, `industries[]` (this makes it appear in onboarding), `requires[]`.
2. UI routes under `/<routePrefix>/…` (kebab-case of the key), with route `data` per 08 §2. API under `/api/v1/m/<key>/…` (`apiBase`). Own DB schema.
3. `nav` groups with order 100–899. At most 3 injections into core groups. Never own core finance documents.
4. `permissions` (`<key>.<resource>.<action>`, lower case), plus optional role templates.
5. Lists via `MasterDataConfig`. Mark resources `customizable` for custom fields.
6. Extensions into core screens instead of forking them; link to core records through source-link hooks, never by adding module columns to core tables.
7. Settings sections, alerts, approval types, reports, widgets and search providers, as needed.
8. Neutral core labels stay untouched. Industry vocabulary stays inside the module.
