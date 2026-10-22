# 05 · Workspace (organization) settings and legal entities

Routes: `/admin/settings` (sections via `/admin/settings/:section`), `/admin/legal-entities`, `/admin/legal-entities/:id`, `/admin/business-units`, `/admin/modules`.
Spec: §2.2 (multi-entity model), §4.4 (Legal entities), §4.25 (Organization settings), §11.2 (`Organization`, `LegalEntity`), §10.3 (legal entity = accounting boundary), §12 (`/settings`, `/entities`, lock/reopen).

> Neutral naming: the spec's "Organization" is called **Workspace** in the UI. "Legal entity" stays, because it is an industry-neutral accounting concept. The spec places Legal entities under "Property & asset". Here they move to core **Administration**, and real-estate KPIs/columns (properties, portfolio value) are contributed by that module through extension points.

---
## A. Workspace settings — `/admin/settings`
Permission: `core.settings.manage` (view and edit). Users without it cannot reach the page.

```
Administration / Workspace settings
Workspace settings   Company profile and the rules every module follows.
┌ Sections (nz-menu inline, sticky) ┐ ┌ Company profile ─────────────────────────────┐
│ ● Company profile                 │ │ Display name*      [Acme Group            ]  │
│ ○ Regional settings               │ │ Group legal name   [Acme Holdings Ltd     ]  │
│ ○ Tax & invoicing                 │ │ Contact email      [admin@acme.com        ]  │
│ ○ Approvals                       │ │ Logo               [img] [Replace] [Remove]   │
│ ○ Alerts & reminders              │ └──────────────────────────────────────────────┘
│ ○ Documents                       │ ┌ Regional settings (read-only) ───────────────┐
│ ○ Modules                         │ │ Workspace ID       ORG-7F3K2      🔒        │
│ ○ Business units →                │
│ ○ Custom fields  →                │ │ Plan               Pro  [Manage plan]        │
│ ── Module settings ──             │ │ Country · currency AE · AED      🔒          │
│ ○ HR & payroll rules (module)     │ │ Time zone          Asia/Dubai                │
│ ○ <Other module> (module)         │ └──────────────────────────────────────────────┘
└───────────────────────────────────┘  … further section cards …
                                   ┌─────────────────────────────────────────────────┐
  sticky save bar (appears on dirty)│ You have unsaved changes  [Discard] [Save changes]│
                                   └─────────────────────────────────────────────────┘
```

* **Layout:** one scrolling page of `nz-card` sections with a sticky left `nz-menu` acting as a scroll-spy anchor (`nz-anchor`). This follows spec §4.25: cards with **one "Save changes" button**. On < lg, the section nav becomes an `nz-select` "Jump to section" at the top.
* **Module settings** sections are contributed by enabled modules (manifest `settingsSections[]`). Spec examples: "Payroll rules" belongs to HR & payroll. The real-estate module registers its lease-expiry window into *Alerts & reminders*. The cheques-due window is a **core Finance** alert, because cheques are a core payment instrument.

### Fields per section
| Section | Label | Type | Control | Req. | Validation | Default / help (spec ref) |
|---|---|---|---|---|---|---|
| Company profile | Display name | text | `nz-input` | Yes | 2–100 | §4.25 |
| | Group legal name | text | `nz-input` | No | ≤ 200 | §4.25 |
| | Contact email | email | `nz-input` | No | Email | §4.25 |
| | Logo | file | `nz-upload` picture | No | PNG/SVG/JPG ≤ 1 MB (assumption) | New |
| Regional settings | Workspace ID | read-only | text with a lock icon + copy button | — | — | Replaces the spec's "Sign-in key" (§4.25). Sign-in is by email (01), so this is an identifier for support and API use only. |
| | Plan | read-only | `nz-tag` + link | — | — | §4.25 |
| | Country / currency | read-only | text | — | — | §4.25 "read-only" |
| | Time zone | select | `nz-select nzShowSearch` | Yes | IANA | Spec shows it read-only. **Assumption:** editable by admin, with a warning "Affects how dates display; doesn't change posted dates." |
| | Date format / Language | select | `nz-select` | No | — | New |
| Tax & invoicing | Standard tax rate | percent | `nz-input-number` with % suffix, precision 2 | Yes | 0–100 | 5.00 % (§4.25 "Standard VAT rate"). The label comes from the country pack: "Standard VAT rate" (AE) or "Sales tax rate" (PK). |
| | Default payment terms | days | `nz-input-number` with "days" suffix | Yes | 0–365 integer | 15 (§4.25) |
| | Document footer | textarea | `nz-input textarea` + `nz-character-count` | No | ≤ 1000 | "Printed at the bottom of invoices, e.g. bank details and payment instructions." (§4.25) |
| Approvals | Approval rules table | table | editable `nz-table` | — | — | Generic form of §4.25 thresholds. One row per **approvable document type registered by modules**, with columns *Document type*, *Requires approval* (`nz-select`: Always / Above amount / Never), *Threshold amount* (`nz-input-number` money, enabled when "Above amount"). The spec encodes this as "0 = always, −1 = never". The UI uses explicit options instead and maps them to the same values. Example rows: Vendor bill (above 10,000), Vendor payment (above 25,000), Payroll run (always), Manual journal (never). |
| | Segregation-of-duties note | static | `nz-alert info` | — | — | "Approvers can never approve their own requests." (§4.25) |
| Alerts & reminders | Rows contributed by modules | number | `nz-input-number` with "days" suffix | Yes | 0–365 | Generic table with columns *Alert*, *Warn within (days)*, *Module*. Core rows: "Legal entity licence expiry" (assumed 30 days) and "Cheque-due alert days" 7 days (Finance module setting, BA §11: workspace default, each legal entity can override it on the legal-entity form; covers incoming cheques due for deposit and outgoing post-dated cheques). Real-estate row: "Lease expiries within" 60 days, stored as a `realestate` module setting (§4.25). Values are saved as per-module settings (`OrgSetting`), never as columns on the core Organization (SQA denylist). |
| Documents | Maximum attachment size | MB | `nz-input-number` with "MB" suffix | Yes | 1–50 (assumed upper bound) | 10 (§4.25) |
| | Allowed file types | multi-select | `nz-select nzMode="tags"` | No | — | New; default pdf, png, jpg, docx, xlsx |
| Modules | see §C | | | | | |
| Business units | link card | — | — | — | — | Opens `/admin/business-units` (§D). Shows a count, e.g. "4 business units". |
| Custom fields | link card | — | — | — | — | Opens `/admin/custom-fields` (custom-fields.md) |
| HR & payroll rules *(module)* | Day basis for daily rate / Overtime multiplier / Working hours per day | number | `nz-input-number` | Yes | >0 | 30 / 1.25 / 8 (§4.25) + spec help text verbatim |

### Actions and states
| Item | Behaviour |
|---|---|
| Save changes (primary) | One `PUT /settings` for all dirty sections (spec). The button is enabled only when the page is dirty and valid. The toast reads "Settings saved". |
| Discard (secondary) | Reverts to the last loaded values. |
| Loading | Skeleton cards. The section nav is visible immediately. |
| Validation | Each invalid section shows a red dot in the left nav. Save scrolls to and focuses the first error. |
| Save error | `nz-alert error` in the sticky bar. Field errors are mapped to their fields. |
| 412 conflict | "Another admin changed settings." **Reload** (pattern 00 §2). |
| Unsaved and leaving | Route-leave guard modal. |

---
## B. Legal entities — `/admin/legal-entities`
Built on the master-data pattern (06) with `resourceKey: 'core.legalentity'`. Permissions (proposed core keys): `core.legalentities.read|manage`, plus the extra action `lockPeriod`. The spec maps entities to `portfolio.*`, a real-estate area, so this is re-keyed for core.

```
┌ HERO (eyebrow COMPANY STRUCTURE) ───────────────────────────────────────────┐
│ Legal entities                                         [+ Add legal entity] │
│ Each entity issues its own documents, keeps its own books and runs payroll. │
└─────────────────────────────────────────────────────────────────────────────┘
[KPI Active entities 2 · within your access] [KPI Open receivables 155,362.50]
[KPI slot ← module-contributed, e.g. RE: Properties 4, Asset value 112.9M]
┌ Entity directory ───────────────────────────────────────────────────────────┐
│ [🔍 Name, code, licence…][⌕] [Region ▾] [Status ▾]   Views▾  ⟳ ⤓ ⚙         │
│ Code     │ Legal entity        │ Region │ Trade licence        │ Tax reg. no.    │ Books locked to │ Status │ ⋮ │
│ ENT-0001 │ Acme Trading LLC    │ Dubai  │ DED-778812 · exp. 02 Dec 2026 ⚠ │ 100234567800003 │ 30 Sep 2026 │ Active │ ⋮ │
└─────────────────────────────────────────────────────────────────────────────┘
```

* Columns (spec §4.4, made generic): Code, Legal entity (bold), Region (label from country pack), Trade licence (no. · exp. date; ⚠ warning tag when within the alert window), Tax registration no., Books locked to (date or —), Status, ⋮. **Module columns:** the real-estate module adds Properties, Portfolio value and Open receivables via `columnExtensions` (08 §5). Core supplies Open receivables only if the Billing capability is enabled.
* Row ⋮ actions: **Edit**, **Lock / reopen period** (spec), **Deactivate** (assumption: blocked if the entity has open documents).
* There is no Delete for entities with postings. **Assumption:** entities are soft-deactivated only.
* Row click opens the detail page `/admin/legal-entities/:id`. Header: title, status, Region · Tax reg. Info grid of all fields. Tabs: *Bank accounts*, *Number sequences* (spec §10.3 "number sequences optionally per entity"), *Documents*, *History*. Modules can add tabs (`detailTabs` extension).

### Legal entity form — drawer (`nz-drawer` width lg)
Spec §4.4 field list. Sections are `nz-collapse`-free headed groups (`h3` inside the form).

```
┌ Add legal entity ──────────────────────────────────────── ✕ ┐
│ ESSENTIALS                                                  │
│ Legal name*            [                               ]    │
│ Code (optional)        [ENT-____ ] Leave blank to auto-number│
│ Region of registration*[Dubai ▾]   Status [Active ▾]        │
│ Phone (optional)       [      ]    Email (optional) [     ] │
│ Registered address (optional) [                          ]  │
│ TAX & LICENCE                                               │
│ Trade licence no. (opt.) [        ] Licence expiry (opt.) [📅]│
│ Tax registration no. (opt.) [               ] 15 digits     │
│ [ ] Registered for tax (VAT)                                │
│ PAYROLL REGISTRATION   (shown if HR module enabled)         │
│ Employer establishment id (opt.) [             ] 13 digits  │
│ Employer bank routing code (opt.) [         ] 9 digits      │
│ BANKING (create only)                                       │
│ [✓] Create an operating bank account                        │
│ Bank name (optional)   [e.g. Emirates NBD             ]     │
│ ── custom fields (section placement per custom-fields.md) ──│
├─────────────────────────────────────────────────────────────┤
│                                     [ Cancel ] [ Save ]     │
└─────────────────────────────────────────────────────────────┘
```

| Label (core / UAE pack) | Type | Control | Req. | Validation | Placeholder / help (spec §4.4) |
|---|---|---|---|---|---|
| Legal name | text | `nz-input` | Yes | 2–200; unique in the workspace (assumption) | — |
| Code | text | `nz-input` | No | ≤ 20; unique | "Leave blank to auto-number" (ENT-0001…) |
| Region of registration / *Emirate of registration* | select | `nz-select` | Yes, if the country pack defines regions | From the pack | Default = first region of the pack. UAE: Dubai (7 emirates). |
| Status | select | `nz-select` | No | Active / Inactive | Active |
| Trade licence no. | text | `nz-input` | No | ≤ 50 | — |
| Licence expiry | date | `nz-date-picker` | No | ≥ today on create (warning only, not blocking) | "Drives the licence-expiry alert" |
| Cheque-due alert days | number | `nz-input-number` + "days", placeholder shows the workspace default | No | 0–365; empty = use the workspace default (7) | Shown when Finance is enabled. Entity override of the Finance setting (BA §11). |
| Tax registration no. / *TRN (15 digits)* | text | `nz-input` | No (required if "Registered for tax" is checked, **assumption**) | Pack validator: UAE exactly 15 digits | — |
| Registered for tax / *VAT registered* | checkbox | `nz-checkbox` | No | — | "Standard-rated document lines need a tax-registered entity." |
| Phone | tel | `nz-input type=tel` | No | E.164-ish | — |
| Email | email | `nz-input` | No | Email | — |
| Registered address | textarea | `nz-input textarea` | No | ≤ 500 | "Printed on tax invoices" |
| Employer establishment id / *MOHRE establishment id (13 digits)* | text | `nz-input` | No | Pack: 13 digits | Section shown only when HR & payroll is enabled. Labels come from the pack. |
| Employer bank routing code (9 digits) | text | `nz-input` | No | Pack: 9 digits | — |
| Create an operating bank account | checkbox | `nz-checkbox` | No | — | Default checked; create only. "Creates a bank account and its ledger account." |
| Bank name | text | `nz-input` | No | ≤ 100 | "e.g. Emirates NBD" |

Actions: **Save** (primary) and **Cancel**. Edit opens the same drawer pre-filled (spec). The Code field is read-only after creation (assumption).

### Lock / reopen period dialog (`nz-modal`, width sm). Not captured in the spec (§8.1), so this is new design.
```
┌ Lock or reopen books — Acme Trading LLC ─────────── ✕ ┐
│ Books currently locked to: 30 Sep 2026                │
│ (•) Lock books up to   [31 Oct 2026 📅]               │
│ ( ) Reopen to          [31 Aug 2026 📅]  (or clear)   │
│ Reason*                [Month-end close October    ]  │
│ ⚠ No posting can be created or changed on or before   │
│   this date.                                          │
│                              [Cancel] [Lock books]    │
└───────────────────────────────────────────────────────┘
```
| Label | Control | Req. | Validation |
|---|---|---|---|
| Mode | `nz-radio-group` | Yes | — |
| Lock date | `nz-date-picker` | Yes | Must be after the current lock date and ≤ today (assumption) |
| Reopen to | `nz-date-picker` + "Clear lock" checkbox | Yes | Must be before the current lock date |
| Reason | `nz-input textarea` | Yes (assumption, for audit) | 5–300 |

The primary button label changes with the mode ("Lock books" or "Reopen books"). **Reopen** uses `nzOkDanger`. Calls `POST /entities/{id}/lock` or `/reopen` (spec §12). Server error example: "3 draft documents are dated in this period." Success toast. The new date appears in the table.

### States (legal entities)
Pattern defaults (06 §7), plus:
* **Empty:** cannot happen after onboarding (≥ 1 entity). If it does, the empty state reads "Add your first legal entity".
* **Restricted access:** the user sees only the entities they can access. The KPI hint reads "Within your access" (spec).

---
## C. Modules — `/admin/modules` (also the *Modules* section in Settings)
This is the post-onboarding counterpart of onboarding Step 4. It is a list of module cards (`nz-card` grid) showing name, description, industry tag, version, status (Enabled / Disabled / Not in plan), and **Enable** / **Disable** buttons.
* **Enable:** an `nz-modal` shows what will be added (menu items, roles, settings, seed data). Then **Enable**, followed by progress.
* **Disable:** `nz-modal nzOkDanger` reads: "Menu items and screens will be hidden. Data is kept and will be visible again if you re-enable." Disabling is blocked when other enabled modules depend on this one; the modal lists them.
* Permission: `core.settings.manage`.

---
## D. Business units — `/admin/business-units`
**v1 scope (coordinator decision): data plus a selector.** Business units (branches, departments, regions, sites; the workspace names them) exist as records. Users have a *default business unit*. A header context selector (07 §2) and a **Business unit** field on records let people tag and filter data. **Business units do not restrict access in v1**; strict data scoping comes in a later release. The UI never implies they do: there is no "access" wording, and the selector tooltip says "Pre-fills new records and filters lists".

Built on the master-data pattern (06), `resourceKey: 'core.businessunit'`. Permissions: `core.businessunits.read|manage`. Custom fields are supported (`customizable: true`).

```
Administration / Business units
Business units   Branches, departments or sites you use to organise records.   [+ New business unit]
┌───────────────────────────────────────────────────────────────────────────────┐
│ [🔍 Name or code…][⌕] [Legal entity ▾] [Status ▾]   Views▾  ⟳ ⤓ ⚙   [Tree|List] │
│ Code   │ Name                 │ Parent        │ Legal entity     │ Manager    │ Status │ ⋮ │
│ BU-001 │ ▾ Head office        │ —             │ Acme Trading LLC │ J. Ahmed   │ Active │ ⋮ │
│ BU-002 │   └ Dubai branch     │ Head office   │ Acme Trading LLC │ S. Khan    │ Active │ ⋮ │
└───────────────────────────────────────────────────────────────────────────────┘
```
* **Tree/List** toggle (`nz-segmented`). Tree view uses `nz-table` tree data (`nzIndentSize`) to show the hierarchy.
* Row actions: Edit, Add child unit, Deactivate/Reactivate, Delete (only when the unit is unused; otherwise "Deactivate instead", see 06 §6).

### Business unit form (drawer, width md)
| Label | Type | Control | Req. | Validation | Placeholder / help |
|---|---|---|---|---|---|
| Name | text | `nz-input` | Yes | 2–100; unique among siblings | "e.g. Dubai branch" |
| Code | text | `nz-input` | No | ≤ 20; unique in the workspace | "Leave blank to auto-number" (BU-001) |
| Parent unit | select | `nz-tree-select` | No | Cannot be itself or one of its descendants | "None (top level)" |
| Legal entity | select | `nz-select` | No | An active entity | "Optional. Used as the default legal entity when this unit is selected." (**assumption**) |
| Manager | lookup | `nz-select nzServerSearch` (users) | No | Active user | — |
| Status | select | `nz-select` | No | Active / Inactive | Active |
| Description | textarea | `nz-input textarea` | No | ≤ 300 | — |

### Business-unit field on records
* **One business unit per record (resolved BU1).** Core and module resources may declare `businessUnit: true` in their resource definition. The form then gets a single-value **Business unit** field (`nz-tree-select`, no multiple), pre-filled from the header selector, or from the user's default if the header shows "All". Only active units can be chosen; an inactive unit already on a record is shown greyed with "(inactive)".
* Lists get a **Business unit** column (hidden by default) and a quick filter. When the header selector is set to a unit, lists apply it as a removable **filter chip** ("Business unit: Dubai branch ×"), so it is a visible convenience filter, not a hidden scope.
* **Required is a per-record-type setting, optional by default (resolved BU1).** It is configured on this page under the **Record types** tab:

```
[ Units | Record types ]
Record type          │ Module        │ Uses business unit │ Required
Bills                │ Purchases     │ ✓                  │ [ off ]
Journals             │ Finance       │ ✓                  │ [ on  ]
Work orders          │ Real estate   │ ✓                  │ [ off ]
```
  * The tab is an `nz-table` listing every resource that declares `businessUnit: true`, with an `nz-switch` in the Required column. Permission: `core.businessunits.manage`. Changes save immediately, with a toast.
  * Turning Required **on** for a record type that already has records without a unit: `nz-modal` "Existing records without a business unit (128) stay valid. The field becomes required when they're next edited." with **Turn on**.
  * When Required is on, the form field loses "(optional)" and validates "Select a business unit". When the header selector is set, the field is pre-filled so users rarely have to pick.
  * This is the same metadata flag as the custom-field `required` setting (custom-fields.md B.5): `FieldMeta.required` for the system field `businessUnitId`.

### States
Pattern defaults (06 §7). Empty: `nz-empty` "No business units yet. Business units are optional. Add them if you organise work by branch, department or site." with **+ New business unit**. While no units exist, the header selector and the record fields are hidden.
