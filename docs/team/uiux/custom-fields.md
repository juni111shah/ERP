# Custom fields (Studio-style), admin screen and how they appear in master data

Routes: `/admin/custom-fields`, `/admin/custom-fields/:resourceKey`, `/admin/custom-fields/:resourceKey/layout`.
Permission (**resolved**, BA CF-SEC-01/02): `core.customfields.manage` to create, edit, reorder, publish, deactivate and delete definitions; `core.customfields.read` to see definitions read-only (all controls disabled, no Publish). Managing fields for a record type also needs that type's `view` key (CF-SEC-05). Without `manage`, the **Customise fields** shortcut is hidden. Read access to field *values* follows the record's own permissions plus each field's visibility rules.
Spec basis: §9 "Custom roles and custom fields" ("custom fields (text, number, date, select) on … usable in filters and exports", Settings → Custom fields). This document follows BA `core-domain.md` §9 (the source of truth for types, limits, naming and lifecycle) and the Advanced UI/UX builder spec `../uiux-advanced/specs/dynamic-fields.md` (draft → publish, full contract) in an Odoo Studio pattern. Everything is industry-neutral. Any resource registered by core or a module ("entity" in this document = record type such as Item, Customer, Vendor, Employee, Legal entity, or a module resource) can be extended when its manifest sets `customizable: true`.

**Rules at a glance (resolved, round 3):**
| Rule | Value | Source |
|---|---|---|
| Types in v1 | 15: Text, Long text, Integer, Decimal, Currency, Percent, Date, Date & time, Yes/No, Single select, Multi select, Reference (lookup), Email, Phone, URL | BA §9.3; File/attachment and 6 other types arrive later and stay hidden until `GET /api/v1/metadata/capabilities` lists them |
| Field limit | **50 custom fields per record type** (active + inactive count; deleted don't) | BA CF-LIM-01 |
| Indexed limit | **10 per record type** flagged Searchable, Filterable, Sortable or Unique | BA CF-LIM-01a |
| Field key | `^cf_[a-z0-9_]{1,60}$`, generated from the label, editable until the field is first saved (published), immutable after | BA CF-NAM-01/02 |
| Type change | Not allowed, except the lossless widenings Text → Long text, Integer → Decimal, Single select → Multi select | BA CF-TYP-01/02 |
| Lifecycle | **Deactivate** (default, keeps values) → **Delete** only when inactive, typed key confirmation, values purged after 30 days; pack fields can't be deleted | BA CF-LIF-01/02 |
| Publishing | Field builder **and** layout designer edit a server-side **draft**; users see the published version until **Publish…** | dynamic-fields.md §5.6 |

---
## A. Admin screens

### A.1 Layout: entity picker + field list — `/admin/custom-fields/:resourceKey`

```
Administration / Custom fields / Customers
Custom fields   Add your own fields to any record type. They appear in forms, lists, filters and exports.
┌ Record types ───────────────┐ ┌ Customers  (core · Parties)            [Form layout ▸] [+ Add field] ┐
│                             │ │ 18 / 50 fields · 7 / 10 searchable or filterable   ● Draft: 3 unpublished changes [Publish…] │
│ [🔍 Search record types…  ] │ │ [🔍 Search fields…] [Type ▾] [Show: Active | Inactive | All] [Source ▾]│
│ ▾ Core                      │ ├──────────────────────────────────────────────────────────────────────┤
│   Customers           (3)   │ │ ⠿ │ Label            │ Field key         │ Type        │ Section     │ Req │ Status   │ ⋮ │
│   Vendors             (0)   │ │   │ 🔒 Name          │ name              │ Text        │ General     │ ✓   │ System   │   │
│   Items               (5)   │ │   │ 🔒 Code          │ code              │ Text        │ General     │     │ System   │   │
│   Legal entities      (1)   │ │   │ 🔒 Email         │ email             │ Email       │ Contact     │     │ System   │   │
│   Employees (HR)      (0)   │ │ ⠿ │ Industry segment │ cf_segment        │ Single select│ Additional │     │ Active   │ ⋮ │
│ ▾ <Module name>             │ │ ⠿ │ Credit limit     │ cf_credit_limit   │ Currency    │ Finance     │     │ Active   │ ⋮ │
│   <Module resource>   (2)   │ │ ⠿ │ Old ref          │ cf_old_ref        │ Text        │ —           │     │ Inactive │ ⋮ │
└─────────────────────────────┘ └──────────────────────────────────────────────────────────────────────┘
```

| Element | Control | Notes |
|---|---|---|
| Record-type picker | `nz-tree` (grouped by module; `nzSearchValue` filter) with custom-field counts as a `nz-badge` | Data comes from the resource registry (`customizable: true`). < lg: becomes an `nz-select` at the top of the page. |
| Field list | `nz-table` (no pagination; one resource rarely has > 200 fields) | **System fields** are shown greyed with a 🔒 icon and a "System" tag. They cannot be edited, deactivated or deleted. Their *section placement* and *label override* can only be changed if the resource allows it (**assumption: not allowed in v1**). **Custom fields** can be edited. |
| Columns | Drag handle (custom fields only, reorders within the list = default form order), Label, Field key (monospace, `cf_…`), Type (icon + name), Section, Required (✓), Show in list (✓), Indexed (🔍 when Searchable/Filterable/Sortable/Unique), Status (`nz-tag`: Active success / Inactive plain / System neutral / **Draft** warning for unpublished new or changed fields), Source (System / Custom / Pack:<module>), Records with value (count, lazy-loaded; optional column), ⋮ | |
| Row ⋮ | Edit, Duplicate, Move to section…, Deactivate / Reactivate, Delete (danger; only on inactive custom fields, never on pack fields) | See A.6. |
| Filters | Search label/field key, Type (multi), Status segmented, Source (System / Custom / Module X) | |
| Header actions | **+ Add field** (primary) opens the drawer (A.2). **Form layout ▸** opens the layout designer (A.5). | |
| Limit counters | Header line: "**18 / 50** fields · **7 / 10** searchable or filterable" (`nz-progress` small, text always shown; turns warning tone at 45 / 9). The values come from `MetadataCapabilities.maxCustomFieldsPerResource` and the indexed count. | At 50, **+ Add field** and **Duplicate** are disabled with the tooltip "This record type has reached 50 custom fields. Deactivated fields count too; delete one to free a slot." See the limit-reached states in A.6. |
| Draft bar | "● Draft: 3 unpublished changes" badge (`nz-badge` dot, warning tone) + **Publish…** + ⋮ (Discard draft, Version history). Hidden when there is no draft. | Shared with the layout designer: one draft per record type covers fields and layout (A.5). |

### A.2 Add / edit field — drawer (`nz-drawer`, width lg, with live preview pane on ≥ xl)

```
┌ Add field to Customers ──────────────────────────────────────────────────────── ✕ ┐
│ ┌ Settings ─────────────────────────────────────┐ ┌ Live preview ───────────────┐ │
│ │ [ 1 Type | 2 Basics | 3 Options | 4 Validation | 5 Visibility ]  (nz-tabset)│ │
│ │ BASICS                                        │ │ Additional information       │ │
│ │ Label*            [Credit limit           ]   │ │ Credit limit                 │ │
│ │ Field key*        [cf_credit_limit        ]   │ │ [AED |        50,000.00 ]    │ │
│ │                   auto from label; locked     │ │ Max amount this customer can │ │
│ │                   after first save            │ │ owe.                         │ │
│ │ Help text         [Max amount this customer…] │ │                              │ │
│ │ Placeholder       [                       ]   │ │ ── as a list column ──       │ │
│ │ Section*          [Finance ▾]  (+ New section)│ │ Credit limit │ 50,000.00     │ │
│ │ Show in list by default [ ]                   │ │                              │ │
│ │ Searchable [ ]  Filterable [✓]  (7 / 10 used) │ │ ── as a filter ──            │ │
│ └───────────────────────────────────────────────┘ │ Credit limit [≥ ▾][     ]    │ │
│                                                    └──────────────────────────────┘ │
├──────────────────────────────────────────────────────────────────────────────────────┤
│ [Deactivate]                         [Cancel] [Save to draft] / [Save & add another] │
└──────────────────────────────────────────────────────────────────────────────────────┘
```
* On create, the tab order is **Type → Basics → Options (select types only) → Validation → Visibility**. On edit, the drawer opens on Basics, and the Type tab shows the current type read-only, with **Widen type…** only where BA CF-TYP-02 allows it (A.6).
* **Save to draft** writes the definition to the record type's draft (A.1 draft bar). Nothing reaches end users until **Publish…**. A new, never-published field shows the **Draft** tag in the list.
* The **live preview** (right pane, `aria-live="polite"` off by default; it updates as the user types) renders the field with the *same* `<erp-dynamic-form>` renderer used in real forms. It shows three renderings: form control, list cell, and filter control. On < xl the preview moves into a **Preview** tab.

#### A.2.1 Basics
| Label | Type | Control | Req. | Validation | Help |
|---|---|---|---|---|---|
| Label | text | `nz-input` | Yes | 1–60; unique per resource (case-insensitive) | Shown on forms, columns, filters and exports |
| Field key | slug | `nz-input` (monospace) | Yes | Must match `^cf_[a-z0-9_]{1,60}$`; unique per record type; keys of deleted fields are reserved for 90 days (CF-NAM-03). Module pack fields use `cf_<module>_` (e.g. `cf_re_`). | Auto-generated from Label (lower snake case, `cf_` prefix, accents stripped) until edited manually. **Editable before the first save, immutable after** (read-only with 🔒 and tooltip "Field keys can't change after saving. Imports, the API and saved views depend on them."). Inline validation as the user types (`nzErrorTip`): "Use cf_ followed by lower-case letters, digits or underscores (max 60)." / "This key is already used on Customers." / "This key belonged to a deleted field and is reserved until 12 Jan 2027." |
| Help text | text | `nz-input` | No | ≤ 200 | Shown under the field |
| Placeholder | text | `nz-input` | No | ≤ 80 | Not shown for Yes/No |
| Section | select | `nz-select` + "New section…" option (`nz-modal`: Section name, Tab (optional)) | Yes | — | Default "Additional information" |
| Show in list by default | checkbox | `nz-checkbox` | No | — | Column visible in the system default view |
| Searchable | switch | `nz-switch` | No | Counts towards the limit of 10 indexed fields | Text, Email, Phone only. Adds the field to the list search box and global search (BA §9.5). |
| Filterable | switch | `nz-switch` | No | Counts towards the limit of 10 | Adds the field to "+ Filter" (B.3). Sortable and Unique (A.4) count too. |
| **At the indexed limit** | — | — | — | — | When 10 fields of this record type are already Searchable/Filterable/Sortable/Unique, the off switches are **disabled** and wrapped in `nz-tooltip`: "10 of 10 searchable or filterable fields are in use on Customers. Turn one off to use it here." (also read by screen readers via `aria-describedby`). Switches already on stay usable. |

### A.3 Field-type picker (Type tab)
`nz-radio-group` of cards in a grid of 3 columns, grouped. Each card has an icon, a name and a one-line description. Search is at the top.

**Capabilities-driven (resolved):** on open, the picker reads `GET /api/v1/metadata/capabilities` (`MetadataCapabilities.supportedFieldTypes`, cached per session, refetched on SignalR `metadata.changed`; contract in `../uiux-advanced/specs/dynamic-fields.md`). Types the server doesn't list are **not rendered at all** (no "coming soon" cards). If the call fails, the picker falls back to the 15 v1 types below.

v1 = the BA's 15 types (BA §9.3), exactly:

| # | Group | Type (contract value) | Stored as | Default control in forms | Notes |
|---|---|---|---|---|---|
| 1 | Text | **Text** (`text`) | string ≤ 255 | `nz-input` | Optional max length, regex pattern. Searchable allowed. |
| 2 | | **Long text** (`longText`) | string ≤ 4000 | `nz-input textarea` (auto-size) + counter | Not sortable. "contains" filter only. |
| 3 | Number | **Integer** (`number`) | int64 | `nz-input-number nzPrecision=0` | Min/max |
| 4 | | **Decimal** (`decimal`) | decimal(18,p) | `nz-input-number` | Precision 0–6 (default 2) |
| 5 | | **Currency** (`currency`) | decimal(18,2) + currency | `nz-input-number` with currency add-on | Currency = record's legal-entity currency, or the workspace base currency (fixed per field, **assumption**). Formatted via `MoneyPipe`. |
| 6 | | **Percent** (`percent`) | decimal(9,4) | `nz-input-number` with "%" suffix | Stored as a percentage value (12.5 = 12.5 %); min/max default 0–100 |
| 7 | Date & time | **Date** (`date`) | date | `nz-date-picker` | Min/max date, or relative ("not in the past") |
| 8 | | **Date & time** (`datetime`) | datetime (UTC) | `nz-date-picker nzShowTime` | Displayed in the workspace time zone |
| 9 | Choice | **Yes / No** (`boolean`) | bool | `nz-switch` (or `nz-checkbox`; layout option) | Filter: is yes / is no / is empty |
| 10 | | **Single select** (`singleSelect`) | option key | `nz-select` (≤ 7 options may render as `nz-radio-group`; layout option) | Options editor (A.3.1) |
| 11 | | **Multi select** (`multiSelect`) | option key[] (≤ 50 values) | `nz-select nzMode="multiple"` | Options editor; max selections optional |
| 12 | Relation | **Reference (lookup)** (`reference`) | id of target record | `nz-select nzShowSearch nzServerSearch` (remote lookup by code/name) | Target record type chosen from the registry (`nz-select`, required, **immutable after save**, CF-TYP-03). v1 single value. Renders as a link to the target's detail page; "Restricted" when the user can't view the target. |
| 13 | Contact | **Email** (`email`) | string | `nz-input type=email` | Renders as a `mailto:` link. Searchable allowed. |
| 14 | | **Phone** (`phone`) | string | `nz-input type=tel` | Optional country-pack format; `tel:` link. Searchable allowed. |
| 15 | | **URL** (`url`) | string | `nz-input type=url` | http(s) only; opens in a new tab with `rel="noopener"` |

**Not in v1 (moved to v1.x / sprint 3, shown only once capabilities list them):** File / attachment, Image, Rich text, Time, Tags, User, Colour, Computed (dynamic-fields.md §1.2).

#### A.3.1 Options editor (single/multi select)
```
OPTIONS                                                  [ + Add option ]
⠿  ● [Retail            ]  key: retail        [🎨 ▾]  Active [on]   ⋮
⠿  ● [Wholesale         ]  key: wholesale     [🎨 ▾]  Active [on]   ⋮
⠿  ○ [Legacy            ]  key: legacy        [🎨 ▾]  Active [off]  ⋮   (deactivated; 12 records use it)
[ ] Sort options alphabetically in forms   Default value: [Retail ▾]
[ Paste list… ]  (bulk add: one option per line)
```
| Feature | Behaviour |
|---|---|
| Add | Appends a row with the label focused. The key is auto-generated from the label, editable until saved, then immutable (CF-LIF-03). |
| Rename | Editing the label keeps the key, so existing data and saved views stay valid. A muted note says so. |
| Reorder | CDK drag handle, plus a keyboard alternative: a ⋮ menu with "Move up / Move down". The order is used in forms and in filter option lists. |
| Colour | `nz-dropdown` of the **fixed tone palette** (not brandable): `success`, `neutral`, `warning`, `error`, `info`, `orange`, `plain`, shown to users as Green, Slate, Amber, Red, Blue, Orange, Grey. Each tone renders with `--erp-tone-<tone>-bg`, `-fg`, `-border` and `-dot` (token-mapping.md §2.2; all fg/bg pairs ≥ 4.5:1 in light and dark). These are the same tones as status tags. No tone = plain text, no chip. Renders as an `nz-tag` (`.erp-tone[data-tone]`) in lists. |
| Deactivate | `nz-switch`. A deactivated option is hidden from new selections but still shown (greyed, "(inactive)") on records that have it. It stays filterable. |
| Delete option | Only allowed when no record uses the option (CF-LIF-03). Otherwise the menu shows "Deactivate instead", or **Merge into…** (`nz-modal`: pick a target option; the backend rewrites values in a job). |
| Validation | Labels are unique within the field, 1–60 chars. At least 1 active option is required. Maximum 200 options (BA CF-LIM-01b). |
| Paste list | `nz-modal` with a textarea: one option per line. Duplicates are skipped and reported. |

### A.4 Validation, defaults, visibility and permissions
**Validation tab** (only options relevant to the type are shown):

| Setting | Applies to | Control | Notes |
|---|---|---|---|
| Required | all | `nz-switch` | Making an existing field required when records have no value prompts: "142 records have no value. They'll be flagged as incomplete when edited, but not blocked from viewing." The options are **Set a default for existing records** or **Require only on new and edited records** (**assumption**: no blocking migration). |
| Unique | text, email, phone, URL, integer, decimal | `nz-switch` (counts towards the 10 indexed fields; disabled at the limit with the A.2.1 tooltip) | Scope: whole workspace / per legal entity (when the resource is entity-scoped). Enabling it runs a duplicate check (`nz-spin`), and if duplicates exist the result shows "7 duplicate values found [View]". The setting cannot be enabled until the duplicates are fixed. |
| Min / Max | integer, decimal, currency, percent, date, datetime, text length, long text length, multi select count | `nz-input-number` / `nz-date-picker` pairs | Min ≤ Max is validated |
| Pattern | text | `nz-input` regex + "Test value" input with ✓/✗ | Custom error message field |
| Default value | all types | Control of the field's own type | Applies to **new** records only |
| Error message | all | `nz-input` | Optional override, e.g. "Enter a value between 1 and 36" |

**Visibility tab:**

| Setting | Control | Behaviour |
|---|---|---|
| Show on form | `nz-radio-group`: Always / Only when… / Never (API only) | "Only when…" opens a condition builder: another field + operator + value (single condition in v1, **assumption**), e.g. *Type = Company*. |
| Read-only on form | `nz-radio-group`: Never / Always / After creation | |
| Who can see this field | `nz-radio-group`: Everyone who can view the record / Only selected roles (`nz-select multiple` roles) | Hidden fields are removed from the form, list, filters, export and API responses for those users. |
| Who can edit this field | Everyone who can edit the record / Only selected roles | Others see the field read-only. |
| Sensitive | `nz-checkbox` | Masks the value (••••) unless the user has `core.sensitivedata.read` (BA BR-SEC-08, CF-PRM-02). Masked fields are excluded from list search. |
| Include in exports | `nz-checkbox` (default on) | |
| Show in quick-create / onboarding import templates | `nz-checkbox` | |
| Sortable | `nz-switch` | Scalar types only. Counts towards the 10 indexed fields (CF-LIM-01a); disabled at the limit with the same tooltip. |
| Applies to | `nz-radio-group`: All legal entities / Selected entities (`nz-select multiple`) | BA CF-SEC-04 |

### A.5 Form layout designer — `/admin/custom-fields/:resourceKey/layout`
Studio-style drag-and-drop arrangement of fields into **tabs → sections → 1/2-column grid**, with a live preview.

```
Customers · Form layout   ● Draft · saved 10:42 · 3 unpublished changes   [Preview as: Admin ▾] [Desktop|Mobile] [Discard draft] [Publish…] [⋮ Version history]
┌ Field palette ─────────┐ ┌ Canvas (live preview, real renderer) ───────────────────────────────┐
│ [🔍 Search fields…]    │ │ [ General | Finance | + Add tab ]                                   │
│ UNPLACED (2)           │ │ ┌ Section: General ──────────────── [2 cols ▾] [✎] [⋮] ┐            │
│ ⠿ Old ref (inactive ✗) │ │ │ ⠿ Name 🔒        │ ⠿ Code 🔒                            │            │
│ ⠿ Region code          │ │ │ ⠿ Industry segment ▾  │ [ drop here ]                  │            │
│ SYSTEM                 │ │ └────────────────────────────────────────────────────────┘            │
│ ⠿ Name 🔒 (placed)     │ │ ┌ Section: Additional information ─────────────────────┐            │
│ CUSTOM                 │ │ │ ⠿ Credit limit                                        │            │
│ ⠿ Credit limit (placed)│ │ └────────────────────────────────────────────────────────┘            │
│ [+ New field]          │ │ [ + Add section ]                                                  │
└────────────────────────┘ └────────────────────────────────────────────────────────────────────┘
```
| Feature | Behaviour |
|---|---|
| Drag and drop | Angular CDK drag-drop. Fields can be dragged from the palette or within the canvas between sections and tabs. A drop placeholder shows the target slot. Fields can span 1 or 2 columns (toggle on hover). |
| Keyboard alternative (required) | Selecting a field (Enter) opens "Move to…": `nz-modal` with Tab, Section and Position (`nz-select`s), plus ↑/↓ buttons. Every drag operation has this keyboard path. Moves are announced via `aria-live` ("Credit limit moved to Finance, position 2"). |
| System fields | Can be moved into any section **if the resource allows layout changes for system fields**. Otherwise they are pinned (🔒, not draggable), and custom fields arrange around them. **Assumption:** modules decide this per resource (`layoutLockedFields[]`). |
| Sections and tabs | Add, rename (inline ✎), set columns (1/2), reorder (drag the header or use the ⋮ Move up/down), delete (only when empty; otherwise the modal "Move 3 fields to…"). Section visibility can use the same condition builder as fields. |
| Field quick settings | Clicking a field on the canvas opens the field drawer (A.2) on the right without leaving the designer. |
| Preview as | Role selector. It applies visibility and permission rules, so the admin sees what a given role sees. The Desktop/Mobile toggle renders at the md and xs breakpoints. |
| Draft (resolved, same as the field builder) | There is **no direct save**. Every change autosaves to the record type's server-side **draft** (`PUT /api/v1/metadata/{resource}/draft`, debounced 1 s). The bar shows "● Draft · saving…" → "● Draft · saved 10:42" and an **unpublished-changes badge** ("3 unpublished changes", warning tone, also on the Custom fields list header and the record-type tree). End users keep seeing the **published** layout. The draft persists across sessions, so the route guard only warns while an autosave is in flight. Undo/redo (↶ ↷) work within the draft. |
| Publish… | Primary. Opens a review modal: grouped change summary (fields added, moved, made required with the CF-VAL-03 impact count, options deactivated), blocking validation errors with "Go to field", and an optional **version note** (`nz-input textarea`, ≤ 200). **Publish** bumps the version (`✓ Published v12`) and clients refetch via SignalR `metadata.changed`. Disabled with a tooltip while there are validation errors. |
| Discard draft | Confirm modal listing the change count; returns to the published version. |
| Version history | ⋮ → list of published versions (author, date, version note) with **Preview** and **Restore as draft**. |
| Concurrency | Another admin published or edited the draft → 412 banner with **Rebase my draft** / **Discard mine** (dynamic-fields.md §5.6). |
| Reset | ⋮ → "Reset to module default layout" (`nz-modal` confirm). This creates a draft; custom fields move to "Additional information". Publish to apply. |

### A.6 States (admin screens)

| State | Display |
|---|---|
| Loading (record types) | `nz-skeleton` tree (8 lines) |
| Loading (field list) | 6 skeleton rows. The record-type header renders immediately. |
| Empty (no custom fields) | System fields are listed. Below them: `nz-empty` "No custom fields yet. Add fields to capture information specific to your business." with **+ Add field**. |
| Empty (no customizable record types) | `nz-result info` "No record types can be customised yet." (only possible with the minimal module set) |
| Error (load) | `nz-result error` + **Retry** in the panel that failed. The other panel stays usable. |
| Save error | `nz-alert error` in the drawer. Field errors on Label/Field key ("This key is already used on Customers."). |
| Save to draft success | Toast "Saved to draft. Publish to make it available in forms, lists, filters and exports." |
| Publish success | Toast "Published v12. Changes are live for everyone." |
| Concurrency | Another admin changed the field: 412 → conflict alert (00 §2). |
| **Deactivate a field** (default action) | `nz-modal`: "Deactivate *Credit limit*? It will be hidden from forms, lists, filters, imports and the API write path. **Values in 142 records are kept** and you can reactivate it any time." Saved views referencing it are listed ("Used in 3 saved views") and auto-adjusted. Primary **Deactivate**. Inactive fields still count towards the 50 limit (CF-LIM-01). |
| **Delete a field** | Only for **inactive** custom fields; never for module-pack fields (menu item hidden, tooltip "Pack fields can be deactivated, not deleted"). `nz-modal nzOkDanger` with: (1) impact summary "142 records have a value · used in 3 saved views · 1 import template"; (2) a checkbox "Export the values first" (default on; triggers a CSV download job); (3) a typed confirmation "Type `cf_credit_limit` to delete"; (4) **Delete permanently** (danger), enabled only when the text matches. Copy: "Values are removed now and purged after 30 days. The key stays reserved for 90 days." (CF-LIF-02, CF-NAM-03). |
| **Field referenced elsewhere** | If another field's visibility condition, a section condition or a module rule uses the field, deactivation and deletion are blocked: `nz-result warning` lists the references, each linking to its fix. |
| **Type change** | **Not allowed** after the field is first saved (BA CF-TYP-01). The Type tab shows the type read-only with the help text "To use another type, create a new field and copy the values." and a **Create field and copy values…** link (guided bulk migration). |
| **Widen type** (only BA CF-TYP-02) | **Widen type…** appears only for Text → Long text, Integer → Decimal, and Single select → Multi select. `nz-modal` confirm: "Change *Segment* from Single select to Multi select? This can't be reversed. Existing values are kept." The change goes to the draft, is audited and bumps the version on publish. All other targets are not offered at all. |
| Reference target | Immutable after save (CF-TYP-03). |
| **Field limit reached (50)** | **+ Add field** and **Duplicate** disabled with tooltip. `nz-alert info` above the list: "Customers has 50 of 50 custom fields (inactive fields count). Delete an inactive field to add another." with a **Show inactive** link. A 51st create via API/import is rejected with the same message. |
| **Indexed limit reached (10)** | Counter shows "10 / 10 searchable or filterable" in warning tone. Searchable, Filterable, Sortable and Unique switches that are off are disabled with the tooltip from A.2.1. `nz-alert info` in the drawer: "Turn off Searchable or Filterable on another field to use it here." |

---
## B. How custom fields appear in the generic master-data pattern (06)

### B.1 Drawer form rendering
* `<erp-dynamic-form>` merges `config.form.sections` (module-defined) with layout metadata (A.5) and custom field definitions from `FieldRegistryService`.
* Placement: by `sectionId` and `order`. A custom section or tab created in the designer is rendered as a normal section or tab. Fields with a missing or unknown section fall into the auto section **"Additional information"** (last).
* Control: the type → ng-zorro control mapping from the A.3 table. `required`, `min`/`max`, `pattern`, `unique` (async validator, `GET /records/{resource}/unique?field=&value=`, **proposed**), `default` (create only), conditional `hidden`, `readOnly` (role-based or after creation), and masking are all applied by the renderer.
* View mode: values render as text, tags (select, using the option tone), or links (reference, email, URL, phone). Empty values show "—".
* Errors are mapped from ProblemDetails `errors["cf_credit_limit"]`, exactly like system fields.

### B.2 Table columns
* Every active custom field the user may see is listed in **Column settings → Custom fields** (grouped and searchable). It is visible by default only if "Show in list by default" is set.
* Column key is `cf.<fieldKey>` (e.g. `cf.cf_credit_limit`, matching the BA API `sort=cf.cf_credit_limit`). Sortable only when the field's **Sortable** flag is on (scalar types: Text, Integer, Decimal, Currency, Percent, Date, Date & time, Yes/No, Single select by option order). Never sortable: Long text, Multi select, Reference (sorting by display name deferred).
* Alignment and format follow the type: numbers and money right-aligned, dates use the date pipe, selects use `nz-tag` with the option tone (text always shown), Yes/No uses ✓/— with an `aria-label`, Percent shows "12.5 %".
* Inactive fields disappear from column settings, and saved views drop them (06 §3 broken-view rule).

### B.3 Filter operators per type (filter bar "+ Filter" and saved views)
| Type | Operators | Value control |
|---|---|---|
| Text, Email, Phone, URL | contains, does not contain, is, is not, starts with, is empty, is not empty | `nz-input` |
| Long text | contains, does not contain, is empty, is not empty | `nz-input` |
| Integer, Decimal, Currency, Percent | =, ≠, >, ≥, <, ≤, between, is empty, is not empty | `nz-input-number` (two for "between") |
| Date, Date & time | is, before, after, between, relative (today, last 7/30 days, this month, last month, this year, next N days), is empty, is not empty | `nz-date-picker` / `nz-range-picker` / `nz-select` relative |
| Yes/No | is yes, is no, is empty | `nz-segmented` |
| Single select | is any of, is none of, is empty, is not empty | `nz-select multiple` (inactive options listed last, labelled "(inactive)") |
| Multi select | has any of, has all of, has none of, is empty, is not empty | `nz-select multiple` |
| Reference | is any of, is none of, is empty, is not empty | remote `nz-select multiple` |

* Chips read like "Credit limit ≥ 50,000" or "Segment is any of Retail, Wholesale".
* Only fields with **Filterable** on appear in "+ Filter". Saved views store filters by field key + option **keys** (not labels), so renaming fields or options doesn't break views.
* API filter shape (BA §9.5): `?cf.cf_segment=retail`, ranges `?cf.cf_credit_limit.gte=50000` (**proposed** operator suffix), sort `sort=cf.cf_credit_limit`.

### B.4 Export and import
* **Export (CSV/XLSX):** visible custom columns are included in "Export current view". "Export all columns" includes every custom field the user may see, except fields with "Include in exports" off, and sensitive fields unless the user has the permission. Headers: Label. In XLSX, a second hidden header row holds the field keys for round-trip import. Select values export as labels (multi joined with "; "), references as code + name. Dates use ISO in CSV and native dates in XLSX.
* **Import (spec §9 import wizard: upload → map → validate → import):** custom fields appear as mapping targets (grouped as "Custom fields"). Auto-mapping matches by label or field key. Validation applies the same rules (required, unique, min/max, options). Unknown select values can be either *created as new options* (admin only) or *rejected*. References are matched by code. The import template download includes custom fields.

### B.5 Metadata contract consumed by the generic component
```ts
// GET /api/v1/metadata/{resourceKey}/fields   (proposed; cached per resource, invalidated via SignalR 'metadata.changed')
export interface ResourceFieldMetadata {
  resourceKey: string;                 // 'core.party'
  version: number;                     // published version; bumps on every publish → clients refetch (BA customFieldsVersion)
  sections: SectionMeta[];             // merged module default + admin layout
  tabs: { id: string; label: string; order: number }[];
  fields: FieldMeta[];                 // system + custom, already filtered by caller's visibility
}
export interface SectionMeta { id: string; label: string; tabId?: string; order: number; columns: 1 | 2; visibleWhen?: Condition; system: boolean }

/** v1 = the BA's 15 types. The full union (22 incl. later types) is in ../uiux-advanced/specs/dynamic-fields.md;
 *  the UI shows only MetadataCapabilities.supportedFieldTypes (GET /api/v1/metadata/capabilities). */
export type FieldTypeV1 = 'text' | 'longText' | 'number' /* Integer */ | 'decimal' | 'currency' | 'percent'
  | 'date' | 'datetime' | 'boolean' | 'singleSelect' | 'multiSelect' | 'reference' | 'email' | 'phone' | 'url';

export interface FieldMeta {
  key: string;                         // system: 'name'; custom: 'cf.cf_credit_limit' (column/filter path)
  apiName: string;                     // 'cf_credit_limit' (custom, ^cf_[a-z0-9_]{1,60}$, immutable) / 'name' (system)
  source: 'system' | 'custom' | 'module';  // module = field-pack field (cf_<module>_*), can't be deleted
  label: string; help?: string; placeholder?: string;
  type: FieldType;                     // FieldType from dynamic-fields.md; v1 values = FieldTypeV1
  status: 'active' | 'inactive';      // published state; drafts live in LayoutDraft (dynamic-fields.md)
  sectionId: string; order: number; span: 1 | 2;
  required: boolean; unique?: { scope: 'workspace' | 'legalEntity' };
  min?: number | string; max?: number | string; precision?: number; maxLength?: number; pattern?: string; errorMessage?: string;
  defaultValue?: unknown;
  options?: { key: string; label: string; tone?: Tone; active: boolean; order: number }[];  // Tone = 'success'|'neutral'|'warning'|'error'|'info'|'orange'|'plain'
  reference?: { targetResource: string; displayField: string; lookupUrl: string; multiple: false };
  currency?: { mode: 'entity' | 'workspace' | 'fixed'; code?: string };
  visibleWhen?: Condition; readOnly: 'never' | 'always' | 'afterCreate' | 'noPermission';
  canEdit: boolean;                    // resolved for current user
  sensitive?: boolean; masked?: boolean; // masked=true => value arrives as null + display '••••'
  list: { availableAsColumn: boolean; visibleByDefault: boolean; sortable: boolean; searchable: boolean };
  filter: { enabled: boolean; operators: FilterOperator[] };   // enabled = Filterable flag
  // sortable + searchable + filter.enabled + unique → counts towards the 10 indexed fields per record type
  export: { include: boolean };
}
export interface Condition { field: string; op: FilterOperator; value: unknown }
export type FilterOperator = 'eq'|'ne'|'contains'|'notContains'|'startsWith'|'gt'|'gte'|'lt'|'lte'|'between'
  |'in'|'notIn'|'hasAny'|'hasAll'|'hasNone'|'isEmpty'|'isNotEmpty'|'relative';
```
* Record payloads carry custom values under `customFields: { cf_credit_limit: 50000, cf_segment: 'retail' }` (BA §9.5). The table reads `cf.<fieldKey>` as a path into that object. **Assumption:** the backend stores them as JSON plus indexed projections. The DB agent owns this decision.
* `MasterDataConfig.includeCustomFields` (06 §8) toggles B.1–B.4 per resource.
* Modules declare in their manifest which resources are customizable (`customizable: true`, `layoutLockedFields`), so custom fields stay industry-neutral and work for any module.

### B.6 Entry points
* Admin: **Administration → Custom fields**.
* In context: on any master-data page, users with `core.customfields.manage` see **⚙ Columns → "Customise fields…"** and a drawer-header ⋮ entry **"Customise this form"**. Both deep-link to the designer for that resource, and return to the list afterwards.
