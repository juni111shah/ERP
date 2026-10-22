# Dynamic (custom) fields — rendering, layout and the field builder

Owner: Advanced UI/UX · Generic: works for **any** resource of any module whose manifest declares `resources[].customizable: true`.
Builds on (does not replace): `uiux/custom-fields.md` (admin screens, options editor, validation/visibility tabs, type-change wizard, list/filter/export behaviour, metadata contract B.5) and BA `ba/core-domain.md` §8 (rules CF-*). Backend today: `fullstack/starter-code/.../CustomFields/*` (9 data types).
Styles: `theme/_dynamic-fields.scss` (included by `theme/styles.scss`).

**Coordinator decisions applied (22 Oct 2026):** (1) manage permission = `core.customfields.manage`; (2) API names match `^cf_[a-z0-9_]{1,60}$`; (3) **draft → publish** for both the field builder and the layout designer; (4) **v1 ships the 15 BA §8 types**, and the other 7 arrive in sprint 3, hidden by capability (§1.2).

This document adds: the **full TypeScript contract** (superset of uiux B.5), the **22 field types** (File/Image split → 23 rows) with edit/read/list rendering, the **3-column section grid**, the **custom-field marker**, the **builder experience** (palette · live canvas · properties panel · undo · draft/publish), and micro-interactions + a11y.

---

## 1. Metadata contract (TypeScript)

```ts
// libs/platform/fields/field-metadata.ts
import type { I18nText, PermissionRequirement, Tone } from '../module-manifest';

/** GET /api/v1/metadata/{resourceKey}/fields  (uiux B.5; ETag = version) */
export interface ResourceFieldMetadata {
  resourceKey: string;                  // '<module>.<recordtype>' singular, lower case (Q12): 'core.party', 'core.item', 'realestate.unit'
  version: number;                      // bumps on any publish → clients refetch (SignalR 'metadata.changed')
  tabs: TabMeta[];                      // empty → no tabs (single page of sections)
  sections: SectionMeta[];
  fields: FieldMeta[];                  // system + module + custom, already filtered for the caller
  limits: { maxCustomFields: number; used: number };
}

export interface TabMeta { id: string; label: string; order: number; visibleWhen?: Condition; icon?: string }

export interface SectionMeta {
  id: string;                           // slug, stable
  label: string;
  description?: string;
  tabId?: string;
  order: number;
  columns: 1 | 2 | 3;                   // max columns at ≥ xl (2 at md–lg, 1 below md)
  collapsible: boolean;
  defaultCollapsed?: boolean;           // remembered per user after first toggle
  visibleWhen?: Condition;
  source: FieldSource;                  // system sections can't be deleted
}

export type FieldSource = 'system' | 'module' | 'custom';

/** v1 (BA §8, 15): text longText number decimal currency percent date datetime boolean
 *  singleSelect multiSelect reference email phone url.
 *  Sprint 3 (7): richText time tags user file/image color computed — hidden unless in capabilities. */
export type FieldType =
  | 'text' | 'longText' | 'richText'
  | 'number' /* integer */ | 'decimal' | 'currency' | 'percent'
  | 'date' | 'datetime' | 'time'
  | 'boolean'
  | 'singleSelect' | 'multiSelect' | 'tags'
  | 'reference' | 'user'
  | 'file' | 'image'
  | 'email' | 'phone' | 'url'
  | 'color'
  | 'computed';

export interface FieldMeta {
  key: string;                          // form/list path: system 'name' · custom 'cf.cf_credit_limit'
  apiName: string;                      // immutable after first publish: 'cf_credit_limit' (CF-NAM-01/02)
  source: FieldSource;
  pack?: { module: string; version: string };      // module field packs (CF-PCK)
  type: FieldType;
  label: string;                        // already localised for the caller
  help?: string;                        // grey text under the field
  placeholder?: string;
  status: 'active' | 'archived' | 'converting';

  // ---- placement
  sectionId: string;
  order: number;
  span: 1 | 2 | 3 | 'full';             // columns occupied (clamped to section.columns)

  // ---- validation (UI mirrors server; server is authoritative — CF-VAL-01)
  required: boolean;
  unique?: { scope: 'workspace' | 'legalEntity' };
  min?: number | string;                // number/date/datetime/time (ISO for dates)
  max?: number | string;
  minLength?: number;
  maxLength?: number;
  precision?: number;                   // decimal 0–6, currency fixed by currency minor units
  pattern?: string;                     // text only; RE2-compatible subset (server uses .NET with timeout)
  errorMessage?: string;
  defaultValue?: unknown | { dynamic: 'today' | 'now' | 'currentUser' | 'currentEntity' };

  // ---- type-specific
  options?: FieldOption[];              // singleSelect | multiSelect | tags (suggestions)
  selectDisplay?: 'dropdown' | 'radio' | 'segmented' | 'checkboxes';
  maxSelections?: number;
  allowCreateTags?: boolean;            // tags: free entry allowed
  reference?: { targetResource: string; displayField: string; secondaryField?: string; lookupUrl: string; multiple: boolean; filter?: Condition[] };
  user?: { multiple: boolean; filterRoles?: string[] };
  file?: { maxFiles: number; maxSizeMb: number; accept: string[] };       // image: accept defaults to image/*
  image?: { aspect?: '1:1' | '4:3' | '16:9' | 'free'; maxPx?: number };
  currency?: { mode: 'entity' | 'workspace' | 'fixed'; code?: string };
  phone?: { defaultCountry?: string };
  richText?: { toolbar: ('bold' | 'italic' | 'underline' | 'list' | 'link' | 'heading')[]; maxLength: number };
  computed?: { expression: string; resultType: Exclude<FieldType, 'computed' | 'file' | 'image' | 'richText'>; dependsOn: string[] };

  // ---- visibility & permissions (CF-PRM; resolved server-side for the caller)
  visibleWhen?: Condition;
  readOnly: 'never' | 'always' | 'afterCreate' | 'afterPosting' | 'noPermission';
  canView: boolean;
  canEdit: boolean;
  sensitive?: boolean;
  masked?: boolean;                     // value arrives null; render '••••'

  // ---- other surfaces (uiux B.2–B.4)
  list: { availableAsColumn: boolean; visibleByDefault: boolean; sortable: boolean; searchable: boolean; width?: number };
  filter: { enabled: boolean; operators: FilterOperator[] };
  export: { include: boolean };
  print?: { show: boolean };

  // ---- audit (admin views only)
  audit?: { createdBy: string; createdAt: string; updatedAt: string; definitionVersion: number };
}

/** tone → the option's colour, from the shared tone palette (§2.1). Omitted = plain text, no chip. */
export interface FieldOption { key: string; label: string; tone?: Tone; active: boolean; order: number }

/** GET /api/v1/metadata/capabilities (cached per session; ETag; refetched on SignalR 'metadata.changed') */
export interface MetadataCapabilities {
  supportedFieldTypes: FieldType[];     // v1 server: the 15 BA §8 types
  maxCustomFieldsPerResource: number;
  features: { layoutDesigner: boolean; computedFields: boolean; fieldPacks: boolean };
}
export interface Condition { field: string; op: FilterOperator; value: unknown }
export type FilterOperator = 'eq'|'ne'|'contains'|'notContains'|'startsWith'|'gt'|'gte'|'lt'|'lte'|'between'
  |'in'|'notIn'|'hasAny'|'hasAll'|'hasNone'|'isEmpty'|'isNotEmpty'|'relative';

/** Record payload: custom values live under customFields (BA §8.5, uiux B.5). */
export interface WithCustomFields { customFields: Record<string, unknown> }

/** Builder-only: a draft of the whole layout + field definitions. */
export interface LayoutDraft {
  resourceKey: string;
  baseVersion: number;                  // published version the draft started from (conflict detection)
  draftRevision: number;                // increments on every autosave
  tabs: TabMeta[];
  sections: SectionMeta[];
  fields: FieldMeta[];                  // includes unsaved new fields (apiName editable until publish)
  updatedBy: string;
  updatedAt: string;
}
```

### 1.1 Backend alignment (gap list for fullstack/db)

| UI type | Backend `CustomFieldDataType` today | Needed |
|---|---|---|
| text, email, phone, url, color | `Text` | add a `Format` (`email`/`phone`/`url`/`color`) or separate enum values so the server validates |
| longText, richText | — | `LongText`, `RichText` (sanitised HTML, server-side allow-list) |
| number / decimal | `Number` / `Decimal` | ✓ |
| currency, percent | — | `Currency` (+ currency mode), `Percent` |
| date / datetime | `Date` / `DateTime` | ✓ |
| time | — | `Time` |
| boolean | `Bool` | ✓ |
| singleSelect / multiSelect | `Select` / `MultiSelect` | ✓ (+ option `Tone`, `Order`) |
| tags | — | `Tags` (string[] + optional suggestions) |
| reference | `Lookup` | ✓ (+ `multiple`, filter) |
| user | — | `Lookup` to `core.user` is enough, or `User` for UX |
| file, image | — | `File` / `Image` (document ids, BA: v1.x) |
| computed | — | `Computed` (BA: later phase) |
| span / columns / tabs / collapsible / visibility roles / readOnly / draft-publish | — | layout metadata entity + draft table |

Also: API names must match **`^cf_[a-z0-9_]{1,60}$`**, as decided by the coordinator. The backend `FieldKey` regex (`^[a-z][a-zA-Z0-9_]{0,63}$`) should be tightened to match. Creating or editing fields and layouts requires **`core.customfields.manage`**.

### 1.2 Supported types by release — the capabilities list drives the UI

| Release | Types |
|---|---|
| **v1** (BA §8, 15 types) | Text, Long text, Integer (`number`), Decimal, Currency, Percent, Date, Date & time, Yes/No, Single select, Multi select, Reference (BA "Lookup"), Email, Phone, URL |
| **Sprint 3** (7 types) | Rich text, Time, Tags, User, File / Image (one BA type, two UI rows), Colour, Computed |

The UI never hard-codes this split. At builder start it reads `MetadataCapabilities.supportedFieldTypes`:
* Palette: types that are not supported are **not rendered at all**. There is no "Coming soon" teaser, because admins can't act on one.
* Type-change wizard and "Duplicate as…": the target list is filtered the same way.
* Rendering: a field whose type the client doesn't know, or that isn't in the list (for example, data restored from a newer server), renders read-only as plain text through `String(value)`. It logs once to telemetry and never throws.
* If the capabilities call fails, the builder falls back to the v1 list, and the form renderer still shows every field the metadata returns.

Each row in §2 carries a **v1** or **S3** marker.

---

## 2. Field types → ng-zorro controls (edit vs read vs list)

Common to all: `nz-form-item` > `nz-form-label [nzFor]` + `nz-form-control [nzErrorTip] [nzExtra]=help`; label row shows "(optional)" when not required (uiux/00 convention); `aria-describedby` = help + error ids. Read mode uses `.erp-field__value`; empty → "—" (`text-muted`). Masked → `••••` + lock icon + tooltip "Hidden for your role".

| # | Type | Edit (ng-zorro) | Read (detail/info grid) | List cell | Notes |
|---|---|---|---|---|---|
| 1 | **Text** <sub>v1</sub> | `input nz-input` (maxlength, counter after 80 % of max) | plain text, wraps | text, ellipsis + title | `dir="auto"` for mixed-script data |
| 2 | **Long text** <sub>v1</sub> | `textarea nz-input [nzAutosize]="{minRows:3,maxRows:10}"` + counter | paragraphs preserved (`white-space: pre-line`), "Show more" after 6 lines | first line, ellipsis; not sortable | default span `full` |
| 3 | **Rich text** <sub>S3</sub> | lightweight editor (TipTap/ngx-tiptap or Quill via `ngx-quill`) with `role="toolbar"` (B, I, U, list, link, H3); paste sanitised | sanitised HTML (DOMPurify + server allow-list) | plain-text excerpt | default span `full`; no images/scripts |
| 4 | **Integer** <sub>v1</sub> | `nz-input-number [nzPrecision]="0" [nzMin] [nzMax]` (`nzStep`=1), end-aligned | `DecimalPipe` digits per locale, tabular, end-aligned in grids | end-aligned, tabular | |
| 5 | **Decimal** <sub>v1</sub> | `nz-input-number [nzPrecision]="p"` with `nzFormatter`/`nzParser` (locale grouping) | formatted to precision | end-aligned | |
| 6 | **Currency** <sub>v1</sub> | `nz-input-group [nzAddOnBefore]="code"` + `nz-input-number` precision 2 (minor units) | `MoneyPipe` (code prefix, red negative, grey zero — spec §3.2) | end-aligned, tabular, totals bold in footers | currency from entity/workspace/fixed |
| 7 | **Percent** <sub>v1</sub> | `nz-input-number` + `nzAddOnAfter="%"`; stored as fraction or % (contract: value 12.5 = 12.5 %) | "12.5 %" (`PercentPipe` with locale) | end-aligned | |
| 8 | **Date** <sub>v1</sub> | `nz-date-picker` (`nzFormat` = workspace format `dd MMM yyyy`, `nzDisabledDate` from min/max) | `dd MMM yyyy` (uiux/00 §5) | same, sortable | locale calendar; inputs accept typed dates |
| 9 | **Date & time** <sub>v1</sub> | `nz-date-picker [nzShowTime]="{ nzFormat: 'HH:mm' }"` | `dd MMM yyyy, HH:mm` in workspace TZ + TZ abbreviation in tooltip | same | stored UTC |
| 10 | **Time** <sub>S3</sub> | `nz-time-picker nzFormat="HH:mm"` (12 h when locale prefers) | `HH:mm` | same | |
| 11 | **Yes / No** <sub>v1</sub> | `nz-switch` (default) or `label nz-checkbox` (layout option); switch has visible "Yes/No" text next to it | "Yes" / "No" text (+ check icon for Yes) | ✓ / — with `aria-label` "Yes"/"No" | never colour only |
| 12 | **Single select** <sub>v1</sub> | `nz-select nzShowSearch` (≤ 7 options and `selectDisplay`: `nz-radio-group` or `nz-segmented`) | `nz-tag` with option tone (text always shown) or plain text when no tone | tag | inactive options shown "(inactive)" on existing records |
| 13 | **Multi select** <sub>v1</sub> | `nz-select nzMode="multiple" [nzMaxMultipleCount]` (`checkboxes` display: `nz-checkbox-group`, in a `fieldset`) | tags wrap, "+3" overflow with popover | first 2 tags + "+N" | |
| 14 | **Tags** <sub>S3</sub> | `nz-select nzMode="tags"` (free entry if `allowCreateTags`, suggestions = options) | neutral tags | first 2 + "+N" | trimmed, case-insensitive dedupe |
| 15 | **Reference (lookup)** <sub>v1</sub> | `nz-select nzShowSearch nzServerSearch` (debounce 250 ms, min 1 char, shows code + name + secondary line), `nzMode="multiple"` when multiple; "+ Create new" footer if permitted (opens quick-create drawer) | link to target detail (`code · name`); "Restricted" (muted, lock) when no view right | link text | uses target's `displayField` |
| 16 | **User** <sub>S3</sub> | same as reference to `core.user` with `nz-avatar` (initials) in options | avatar + name (link to profile if permitted) | avatar 20 px + name | default "current user" supported |
| 17 | **File** <sub>S3</sub> | `nz-upload nzType="drag"` (button + drop zone; drop zone is an enhancement), list with progress, remove ×, `nzBeforeUpload` validates type/size | file chips (icon by type, name, size) + download; preview for PDF | 📎 count | Documents service; max size from settings (spec 10 MB) |
| 18 | **Image** <sub>S3</sub> | `nz-upload nzListType="picture-card"` + crop modal (aspect), alt-text input required | thumbnail(s) 64 px, `nz-image` preview with alt | 24 px thumbnail | alt text stored with the document |
| 19 | **Email** <sub>v1</sub> | `input nz-input type="email" autocomplete="off"` (`email` only on own profile) | `mailto:` link | link | |
| 20 | **Phone** <sub>v1</sub> | `nz-input-group` with country `nz-select` (flag + dial code, default country) + `input type="tel"`; normalised to E.164 on blur, shown formatted | `tel:` link, formatted per country, `dir="ltr"` | link | |
| 21 | **URL** <sub>v1</sub> | `input nz-input type="url"` with `https://` add-on hint; http/https only | link with external-link icon, `target="_blank" rel="noopener noreferrer"`, shows host only + full URL tooltip | host | |
| 22 | **Colour** <sub>S3</sub> | `nz-color-picker` (ng-zorro ≥ 17) with hex input + swatch; presets = viz palette | swatch + hex (`.erp-field__swatch`), name of the nearest preset for SR ("#2A78D6, blue") | swatch + hex | contrast warning if used as text colour (not allowed) |
| 23 | **Computed** <sub>S3</sub> | read-only even in edit mode: value text + `ƒx` icon + tooltip showing the formula in plain words; recalculates live as dependencies change (debounced 150 ms, "Calculating…" shimmer) | value formatted per `resultType` | per result type | server recomputes on save (authoritative) |

(The brief's 22 types, with File and Image split into two rows = 23 rows. "Integer" is `number` in the contract for uiux compatibility. <sub>v1</sub> = ships in v1; <sub>S3</sub> = sprint 3, hidden until it appears in capabilities.)

### 2.1 Option colours = the status tone palette

Select, multi-select and (later) tag options use the **same 7 tones as status tags**. That way one set of AA-checked colours covers every chip in the product. In the options editor (uiux A.3.1) the colour picker is an `nz-radio-group` of 7 swatches. Each swatch is a chip that shows the user-facing name, with "None" first (no tone = plain text). The swatch radio has `aria-label` "Colour: Green". The chosen chip renders with `class="erp-tone" data-tone="<tone>"` (tokens.scss). Options without a tone render as plain text in read mode, and as a `plain` tag inside multi-value cells.

| Tone (API) | Shown to users as | Light bg / fg / border | fg:bg | Dark bg / fg / border | fg:bg |
|---|---|---|---|---|---|
| `success` | Green | #F6FFED / #237804 / #B7EB8F | 5.44 | #162312 / #73D13D / #274916 | 8.51 |
| `neutral` | Slate | #EEF2F6 / #3D4D5C / #CBD5E0 | 7.73 | #1F2A36 / #A9B8C7 / #33445A | 7.19 |
| `warning` | Amber | #FFFBE6 / #874D00 / #FFE58F | 6.53 | #2B2111 / #FFC53D / #594214 | 10.02 |
| `error` | Red | #FFF2F0 / #CF1322 / #FFCCC7 | 5.10 | #2A1215 / #FF7875 / #58181C | 6.85 |
| `info` | Blue | #E6F4FF / #0958D9 / #91CAFF | 5.50 | #111A2C / #69B1FF / #15325B | 7.73 |
| `orange` | Orange | #FFF7E6 / #AD4E00 / #FFD591 | 5.09 | #2B1D11 / #FFA940 / #593815 | 8.55 |
| `plain` | Grey | #F4F6F8 / #434A55 / #E2E5EA | 8.25 | #2A2E35 / #C9CED6 / #3A3F47 | 8.62 |

* Tokens: `--erp-tone-<tone>-bg|fg|border|dot`. `dot` is a ≥ 3:1 solid used for list-cell dots and for chart legends that need the option colour.
* Borders are decorative. The text label always carries the meaning (WCAG 1.4.1), so borders are exempt from 1.4.11.
* The palette is fixed and **not** tenant-brandable, so an admin can never pick an unreadable combination. Free hex colours are reserved for the `color` field type (S3).
* Option tones are data. A tone keeps its name across themes, so "Red" is still the `error` tone in dark mode, with the dark values.

**Read vs edit mode switch**: detail pages render read mode (`InfoGridComponent`); "Edit" opens the drawer/page in edit mode (uiux/06 master-data pattern). Inline edit (optional per resource): click/Enter on a value turns it into the control (`fast` fade), Enter/blur saves (optimistic for non-financial fields — micro-interactions §6), Esc cancels.

---

## 3. Form layout

### 3.1 Structure
`Tabs (optional) → Sections → responsive grid → fields`.

* **Tabs** when the resource has > 4 sections or > 24 visible fields, or when the admin adds them: `nz-tabset` (`nzLinkRouter` so tabs are deep-linkable `?tab=finance`); tabs with validation errors show an error dot + count ("Finance · 2 errors"), and on submit the first erroneous tab is activated.
* **Sections**: `.erp-form-section` cards; header is a `<button aria-expanded aria-controls>` when `collapsible`; meta on the right ("3 of 8 filled", or "2 errors" in `error-text`); collapsed state remembered per user + resource; sections containing errors auto-expand on submit.
* **Grid** (`.erp-field-grid[data-columns]`, fields `[data-span]`):

| Viewport | Max columns | Span behaviour |
|---|---|---|
| < md (768) | 1 | all fields full width |
| md – lg (768–1199) | 2 | span 1 = half; span 2/3/full = full row |
| ≥ xl (1200) | section `columns` (1–3) | span 1 = one column; span 2 in a 3-column section = two-thirds; span 3/full = full row |

* Default spans by type: long text, rich text, file, image, multi-select with `checkboxes` → `full`; others → 1.
* Drawer forms (uiux/06) are narrower: drawer `lg` (≥ 720 px) allows 2 columns, `md` drawers 1 column — the grid uses a **container query** on the drawer body (`@container (inline-size < 640px)` → 1 column) rather than viewport width.
* Vertical rhythm: `--erp-form-row-gap` (16 / 12 compact), label→control 4 px, help/error 4 px below.

### 3.2 List / table rendering of custom fields
As uiux/custom-fields B.2 plus: column header shows the label (with `cf.` key in the column-chooser secondary line); width defaults by type (text 180, number/currency 120 end-aligned, date 120, boolean 80 centred, select 140, reference 200, user 160, file 80); numeric columns tabular; truncated cells get `title` + full value in the row's accessible name; sorting/filter per type (B.3); in admin "Edit layout" mode a small info dot marks custom columns (§4).

---

## 4. Distinguishing custom fields from built-in ones

**Goal**: invisible to everyday users (a custom field is just a field), obvious to admins designing forms.

| Context | Treatment |
|---|---|
| End users (default) | **No marker.** Same label, control and spacing as built-in fields. |
| Admin "Edit layout" / builder / field list (`data-field-origin="visible"` on the form container, set when the user has `core.customfields.manage` **and** toggles "Show field origins" — default on inside the builder, off elsewhere) | 16 px `.erp-field-origin` chip after the label: icon `appstore-add` on `info-bg`, colour `info-text` (6.16:1). Tooltip (`nz-tooltip`, focusable chip `tabindex=0`): "Custom field · `cf_credit_limit` · created by Maya Ali, 12 Oct 2026". Visually-hidden text "(custom field)" is appended to the label for SRs **only in this mode**. A 2 px dotted `info-border` rule on the field's inline-start edge helps scanning. |
| Module pack fields | same chip with icon `api` and tooltip "Module field · Real estate pack v1.0" (example) |
| System fields (builder only) | 🔒 `lock` icon (muted) + tooltip "Built-in field — can be moved but not edited" or "…pinned" when in `layoutLockedFields` (uiux A.5) |
| Table columns (admin mode) | 6 px `info-solid` dot after the column title + same tooltip in the column chooser |
| Print/PDF | never marked |

Colour is never the only cue (icon + tooltip + SR text); no badges in end-user mode (noise).

---

## 5. Field builder (Studio-style)

Route: `/admin/customize/:resourceKey` (deep links from uiux B.6 "Customise this form"). Merges uiux A.5 (layout designer) with A.2 (field drawer) into a single 3-pane workspace on ≥ xl; on lg–xl the properties pane becomes the uiux A.2 drawer; < md read-only preview + "Open on a larger screen to edit".

```
┌ bar ─────────────────────────────────────────────────────────────────────────────────────────────┐
│ ← Parties · Customise form    ● Draft · saved 10:42   [↶][↷]  Preview as [Admin ▾] [▭ Desktop|▯ Mobile] [Discard draft] [Publish…] │
├ palette (264) ─────┬ canvas (live preview, real renderer) ───────────────┬ properties (340) ───────┤
│ ⌕ Search types/fields │ [ General | Finance | + Tab ]                       │ Credit limit  ⓘ custom │
│ NEW FIELD           │ ┌ General ▾ ─────────── [3 cols ▾] [⋮] ┐            │ ─ Basics ───────────── │
│  Aa Text            │ │ ⠿ Name 🔒   │ ⠿ Code 🔒  │ ⠿ Type 🔒  │            │ Label  [Credit limit ] │
│  ¶  Long text       │ │ ⠿ Credit limit ⓘ  (selected)        │            │ API name cf_credit_limit│
│  #  Integer  …      │ │ [ drop here ]                        │            │ Type   Currency (locked)│
│ EXISTING (unplaced) │ └───────────────────────────────────────┘            │ Help   [            ]  │
│  ⠿ Region code      │ [ + Add section ]                                   │ ─ Validation ──────────│
│ SYSTEM              │                                                     │ ─ Visibility ──────────│
│  ⠿ Name 🔒 (placed) │                                                     │ ─ Layout: span [1|2|3] │
└────────────────────┴─────────────────────────────────────────────────────┴────────────────────────┘
```

### 5.1 Palette
* Groups (uiux A.3 grouping): Text (Text, Long text, Rich text, Email, Phone, URL) · Number (Integer, Decimal, Currency, Percent) · Date & time (Date, Date & time, Time) · Choice (Yes/No, Single select, Multi select, Tags, Colour) · Relation (Reference, User) · Media (File, Image) · Advanced (Computed). Only types listed in `capabilities.supportedFieldTypes` render (§1.2). In v1 the groups are Text (Text, Long text, Email, Phone, URL), Number, Date & time (Date, Date & time), Choice (Yes/No, Single select, Multi select) and Relation (Reference). A group with no supported types is hidden.
* Each item: icon + name + one-line description in tooltip; `button` elements (Enter/Space = "add to selected section at end"), draggable (CDK `cdkDrag` with `cdkDropListConnectedTo` all section lists; palette list uses `cdkDropListSortingDisabled` and copies on drop).
* "Existing fields" group lists unplaced/archived fields (archived shown muted with "Restore"); "System" group lists built-ins (placed ones show "placed", click scrolls to it on canvas).
* Search filters types + existing fields; limit banner when `used ≥ maxCustomFields` (uiux A.6).

### 5.2 Canvas (live preview)
* Uses the **real** `DynamicFormComponent` in `designMode` (inputs inert, values = sample/default values), so what you see is what users get, including density, theme and RTL (toggle in bar: LTR/RTL preview).
* "Preview as" role selector applies visibility/permission rules (uiux A.5); Desktop/Mobile renders the canvas in a container at 1280 / 375 px (container queries drive the grid).
* Selecting a field (click / Enter / Space): `aria-selected="true"`, outline `primary`, properties panel loads it. Hover shows drag handle ⠿ and quick actions (span toggle 1/2/3, duplicate, archive).
* Sections: drag by header handle; inline rename (✎ / F2); columns `nz-select` 1/2/3; ⋮ Move up/down, Collapse by default, Visibility condition, Delete (only empty; else "Move N fields to…" modal).
* Tabs: add / rename / reorder (drag or ⋮) / delete-when-empty.
* Drop zones: each section is a `cdkDropList` (`cdkDropListOrientation="mixed"` on Angular CDK ≥ 18.1 for wrapping grids; fallback: vertical list with span rendering); empty section shows `.erp-fb-empty-drop` "Drag a field here or press Enter on a field type".

### 5.3 Properties panel
Tabs (uiux A.2/A.4 content): **Basics** (label, API name, type (locked after publish), help, placeholder, default), **Options** (select types — uiux A.3.1 editor), **Validation**, **Visibility & permissions**, **Layout** (section, span, show in list, filterable, searchable, include in exports, show on print). Changes apply to the canvas **live** (debounced 150 ms) and go into the draft + undo history. Field-level errors appear inline and as a red dot on the field in the canvas.

### 5.4 Drag & drop with keyboard alternatives (WCAG 2.1.1, 2.5.7)

| Pointer | Keyboard | Menu (single pointer, no drag) |
|---|---|---|
| Drag palette item onto a section | focus palette item → Enter → added at end of the selected section (or the first section) and selected | palette item ⋮ → "Add to…" (section picker) |
| Drag field within/between sections | focus field → **Space** to pick up (`data-grabbed`, announced "Credit limit grabbed. Position 4 of 6 in General. Use arrow keys to move, Space to drop, Escape to cancel.") → ↑/↓ (←/→ in grid order; mirrored in RTL) moves; Ctrl+↑/↓ jumps to previous/next section; Space drops ("Dropped in Finance, position 2"); Esc cancels (returns) | field ⋮ → Move up / Move down / Move to… (modal: tab, section, position) — uiux A.5 |
| Resize span | focus field → `[` / `]` decrease/increase span (announced "Span 2 of 3 columns") | Layout tab → span segmented |
| Reorder sections/tabs | focus header → Space pick up → ↑/↓ → Space | ⋮ Move up/down |

All moves are undoable and announced via the polite live region.

### 5.5 Undo / redo
* History stack of **operations** (add, move, resize, property change, delete, section/tab ops), max 100, grouped (typing in a property = one entry per 1 s pause).
* Ctrl/⌘+Z undo, Ctrl/⌘+Shift+Z or Ctrl+Y redo; bar buttons ↶ ↷ with tooltips "Undo: Move Credit limit" (`aria-label` includes the action), disabled when empty.
* Undo selects and scrolls to the affected field, flashing it (`primary-subtle`, 600 ms).
* History survives autosave but not "Discard draft" or publish.

### 5.6 Draft vs publish
* Every change **autosaves to a server-side draft** (`PUT /api/v1/metadata/{resource}/draft`, debounced 1 s, `If-Match: draftRevision`). Status in the bar: "● Draft · saving…" → "● Draft · saved 10:42" (`warning-text` dot = unpublished) → after publish "✓ Published v12" (`success-text`). Saving failure: "⚠ Not saved — Retry" (`error-text`), changes kept locally (IndexedDB) until it succeeds.
* End users keep seeing the **published** version until publish. **Draft → publish is the rule for both the field builder and the layout designer** (coordinator decision). uiux A.5's "Save layout" becomes "Publish…", with the same confirm modal. One draft per resource covers both field definitions and layout, so a publish is atomic.
* **Publish…** opens a review modal: change summary grouped (3 fields added, 2 moved, 1 made required → "142 records have no value" impact from CF-VAL-03, 1 option deactivated), validation result (blocking errors listed with "Go to field"), optional note; **Publish** (primary). Publishing bumps `version`, emits `metadata.changed`; open forms of other users show a non-blocking banner "This form was updated. Reload to see changes." (never mid-edit swaps).
* **Discard draft** → confirm modal (lists change count). **Version history** (⋮): list of published versions with author/date/note, "Preview" and "Restore as draft".
* Concurrency: another admin publishes or edits the same draft → 412 → banner "Maya published changes 2 min ago" with **Rebase my draft** (auto-merge non-conflicting ops; conflicts listed) / **Discard mine**. Soft lock indicator: "Maya is also editing" avatar chip.

### 5.7 Name validation (label → API name)
* API name auto-generated from the label: transliterate (Arabic/Urdu labels → Latin via ICU-style map, else fallback `field_N`), lower snake case, prefix `cf_` (module packs `cf_<module>_`), regex **`^cf_[a-z0-9_]{1,60}$`** (coordinator decision: `cf_` plus 1–60 lower-case letters, digits or underscores). Generation also collapses repeated `_` and trims a trailing `_` for neatness, but validation accepts anything that matches the regex.
* Editable until first publish (then read-only with lock + tooltip "API names can't change after publishing — integrations depend on them", CF-NAM-02).
* Live checks (debounced 300 ms; inline, `aria-live` polite): required; regex; unique within the resource (including archived and **reserved keys of deleted fields for 90 days**, CF-NAM-03); not a reserved/system word (`id`, `name`, `code`, `status`, `createdAt`, … and any system field `apiName` of the resource); label 1–60 chars and unique within the section (warning only, not blocking).
* Messages: "API name already used by *Credit limit (archived)*", "Start with cf_ then use up to 60 lower-case letters, numbers and underscores", "This name is reserved".

### 5.8 Builder states

| State | Display |
|---|---|
| Loading | bar renders; palette 6 skeleton rows; canvas skeleton of 2 sections × 4 fields; properties panel empty |
| Empty (no custom fields) | canvas shows system sections + a dashed prompt card "Add your first field: drag a type from the left, or press Enter on one." |
| No selection | properties panel: illustration + "Select a field to edit its properties" + shortcuts list |
| Limit reached | palette "New field" group disabled with `nz-alert info` (uiux A.6) |
| Validation errors | red dot on fields + count in bar "2 issues" (click → next issue); Publish disabled with tooltip |
| Load error | `nz-result error` in the failing pane + Retry; other panes usable |
| Autosave error | bar status + toast with Retry; local backup |
| Converting field | field locked "Converting…" tag (uiux A.6) |
| Mobile | read-only preview + notice |
| Unsaved navigation | there is no unsaved state (autosave); leaving shows nothing unless an autosave is in flight ("Saving… leave anyway?") |

---

## 6. Micro-interactions & accessibility

| Interaction | Motion (tokens) | A11y |
|---|---|---|
| Section collapse/expand | `grid-template-rows` 0fr↔1fr + chevron rotate, `base` / `standard` | `button[aria-expanded][aria-controls]`; heading level h3 inside the form |
| Field error appears | fade + 2 px drop, `quick` / `decelerate` | `aria-invalid="true"`, error id in `aria-describedby`; summary on submit |
| Computed value recalculates | shimmer on value ≤ 150 ms then cross-fade `quick` | `aria-live="polite"` on computed values only when the user changed a dependency (throttled) |
| Palette hover | border/bg `fast` | buttons with names "Add Text field" |
| Drag pick-up / move / drop | lift `quick`, siblings shift `base` / `standard`, drop settle `base` | keyboard path §5.4, announcements, `aria-roledescription="draggable field"` |
| Selection | outline appears instantly; panel content cross-fade `quick` | `aria-selected`; fields are buttons in a list (`role="list"`) so screen-reader browse mode works; keyboard drag mode is announced and exits on Space/Esc |
| Autosave status | dot colour + text change, no motion | `role="status"` (polite): "Draft saved" at most every 10 s |
| Undo flash | `primary-subtle` 600 ms | announce "Undid: Move Credit limit" |
| Publish | modal `slow` / `emphasized`; success toast | focus to modal heading; on success focus returns to bar status |
| Inline edit (read mode) | value→control cross-fade `fast` | Enter to edit, Esc cancel, focus stays on control, then returns to value |
| Origin chip tooltip | `quick` fade | chip focusable (`tabindex=0`, `aria-describedby` tooltip) only in admin mode |
| Reduced motion | all above instant; drag preview without lift/scale | — |

Other a11y rules: visible labels (no placeholder-only); required = `aria-required` + "(optional)" convention for the others; radio/checkbox groups in `fieldset/legend`; reference/user selects are `combobox` with `aria-busy` while searching and result count announced; file upload is a real button ("Upload files, max 10 MB each, PDF or images") with the list of files as a `ul` and per-file remove buttons ("Remove invoice.pdf"); image fields require alt text; colour picker exposes hex text input; target sizes ≥ 24 px in compact; all builder actions reachable without a pointer; RTL mirrors drag directions and chevrons; tested with NVDA/JAWS/VoiceOver per `accessibility.md` §9.
