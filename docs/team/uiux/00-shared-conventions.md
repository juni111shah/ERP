# 00 · Shared UI conventions (apply to every screen)

Status: Sprint 0 draft · Owner: UI/UX Expert · Source: spec §3.3, §3.4, §4 (global conventions), §10.2

These conventions are referenced by every wireframe file so they are not repeated. Visual values (colours, sizes, fonts) are **not** defined here. Only semantic token names are used, always written as `--erp-<name>`; the Advanced UI/UX expert (theme owner) defines them in `../uiux-advanced/theme/tokens.scss`, and the mapping of every name this pack uses is in [`../uiux-advanced/token-mapping.md`](../uiux-advanced/token-mapping.md) (T1–T15 resolved, coordination-notes §A). Renames applied: `text-secondary` → `text-muted`, `content-max-width` → `form-max-width`, `focus-ring` as a colour → `focus-ring-color`, tone `attention` → `orange`.

## 1. Page anatomy

```
┌──────────────────────────────────────────────────────────────────────────┐
│ Breadcrumb (detail / nested pages only)   Core / Users & roles / Edit    │
│ Page title  [Status tag]  · context line (muted)        [Sec] [Primary] │
│ Subtitle sentence (muted)                                                │
├──────────────────────────────────────────────────────────────────────────┤
│ (optional) Hero banner  OR  KPI card row (4–6 cards)                     │
├──────────────────────────────────────────────────────────────────────────┤
│ Card: toolbar + table / form / info grid                                 │
│ Tabs for related records (detail pages): … · Documents · History         │
└──────────────────────────────────────────────────────────────────────────┘
```

| Element | ng-zorro / shared component | Notes |
|---|---|---|
| Page header | `PageHeaderComponent` (wraps `nz-breadcrumb` + title row) | Spec §10.2 lists it as a shared block. Title then status tag then muted context line. Actions are right-aligned in this order: destructive (danger outline), secondary (default), primary. |
| Hero banner | `HeroBannerComponent` | Only on landing or overview pages (Overview dashboard, Legal entities). The spec (§3.3) also uses it on the real-estate Acquisitions page, so modules may use it too. Eyebrow, title, subtitle, and an action or KPI on the right. |
| KPI card | `KpiCardComponent` with `accent` input (`accent-gold`, `primary`, `info`, `warning`, `error`) | Shows label, value, hint. A KPI card is clickable when it filters the table below. In that case it is rendered as a `button`. |
| Info grid | `InfoGridComponent` (`nz-descriptions` underneath) | 4–6 columns on desktop, 2 on tablet, 1 on mobile. |
| Status tag | `StatusTagComponent` (`nz-tag`) | Status → semantic tone map from spec §3.4 (success / neutral / warning / error / info / orange / plain), rendered as `--erp-tone-<tone>-bg|fg|border`. These 7 tones are also the custom-field option palette (one list, resolved round 3). The status text is **always** shown, so colour is never the only signal. |
| Money | `MoneyPipe` | Uses the org currency code prefix (the spec uses "AED"; the generic ERP reads it from the org/entity currency). Right-aligned, thousands separators, 2 dp, `--erp-color-money-negative` with a minus sign, `--erp-color-money-zero` for zero. |
| Detail tabs | `nz-tabset` | Every detail page ends with the **Documents** and **History** tabs (spec §4 global conventions). |

## 2. Forms

* Layout: `nz-form` vertical labels, two columns on ≥ md and one column on < md (spec §3.3 "two-column form").
* Optional fields: the label suffix "(optional)" is shown in a muted style. Required fields get **no** asterisk, which matches the spec convention "fields without that marker are treated as required". Each required control still sets `aria-required="true"`.
* Hints go in muted text under the field (`nzExtra`). Errors use `nz-form-control [nzErrorTip]`, which displays them under the field.
* Validation timing: on blur and on submit. Validation does not run while the user types, except for live counters (for example 115 / 1000).
* On submit with errors: (1) an `nz-alert` error summary appears at the top of the form, listing each error as a link that focuses its field; (2) focus moves to the summary.
* Server errors: ProblemDetails `errors{field:[msg]}` are mapped onto the matching controls. Non-field errors go in the top `nz-alert`.
* Concurrency (HTTP 412, If-Match RowVersion, spec §12): an `nz-alert` warning reads "This record was changed by someone else since you opened it." with the actions **Reload latest** and **Copy my changes**. The form never overwrites silently.
* Dirty guard: closing a drawer, modal or page with unsaved changes opens `nz-modal` confirm "Discard unsaved changes?" with **Keep editing** (primary) and **Discard**.
* Shared validators (spec §10.2) come from the country pack, not from core. Example for the UAE pack: TRN = 15 digits, IBAN = AE + 21, Emirates ID, MOHRE 13/14 digits, routing code 9 digits. Other country packs replace the label and the validator (spec §9 "Pakistan market option").
* Save buttons show `nzLoading` while saving and are disabled while saving, which prevents double-submit.

## 3. Universal states

| State | Pattern | ng-zorro |
|---|---|---|
| Initial load | Skeleton in the shape of the final layout. The spec saw 3–7 s loads (§8 #4), so skeletons are mandatory. | `nz-skeleton` (`[nzActive]=true`), table body rows via `nz-skeleton` paragraph |
| Refresh / paging | Table keeps its data and shows the `nzLoading` overlay | `nz-table [nzLoading]` |
| Empty (no data at all) | Illustration, sentence, and the primary create action when the user has permission | `nz-empty` with custom `nzNotFoundContent` + footer button |
| Empty (filtered) | "No results match your filters." plus a **Clear filters** link | `nz-empty` (simple) |
| Error (load) | Inline result with a retry action. The page chrome stays visible. | `nz-result nzStatus="error"` / `"warning"` (network) + **Retry** |
| Forbidden (403) | "You don't have access to this page." with **Go to Overview**. Hidden by guards in normal use. | `nz-result nzStatus="403"` |
| Not found (404) | "This record doesn't exist or was removed." with **Back to list** | `nz-result nzStatus="404"` |
| Success (mutation) | Toast "Saved", "User created", and similar messages | `NzMessageService.success` (aria-live polite) |
| Error (mutation) | Toast for non-field errors, field errors inline | `NzMessageService.error` / `nz-alert` |
| Destructive confirm | Popconfirm for row-level actions and a modal for bulk or irreversible actions | `nz-popconfirm`, `NzModalService.confirm({nzOkDanger:true})` |
| Disabled | Control disabled, with a tooltip that explains why (for example "Period locked to 30 Sep 2026") | `nz-tooltip` on a wrapper `span` |
| Read-only (no write permission) | Edit and create actions are hidden, not disabled. View mode is used. | `*hasPermission` directive (spec §7.3) |

## 4. Permissions in the UI

* Use the `*hasPermission="'core.users.manage'"` directive and route guards (spec §7.3). Actions the API would refuse are **hidden**. They are disabled with a tooltip only when the user has the permission but the record state blocks the action (locked period, own approval request, and similar).
* Maker-checker: the requester never sees Approve/Reject on their own request. Instead they see the tag "Your request" (spec §4.3, §7.3).
* Terminology in this pack:
  * **Workspace** is the SaaS customer company. **UI word: "Workspace". Backend/API/DB name: `Organization`** (spec §2.2 "Organization (tenant)", table `Organization` §11.2, JWT claim `org`). Specs say "workspace" for anything a user sees, and "Organization" only when they refer to API or data names. The word "tenant" is never used in core UI. One sign-in email can belong to several workspaces (01 §B).
  * **Legal entity** is a registered company inside the workspace that keeps its own books.
  * **Business unit** is a generic sub-division (branch, department, region, site). **In v1 it is data plus a selector only.** Records can carry a Business unit field, and the header context selector pre-fills it and offers it as a filter. It does **not** restrict data access; strict scoping by business unit comes in a later release.

## 5. Dates, numbers, text

* Dates display as `dd MMM yyyy` and timestamps as `dd MMM yyyy, HH:mm` (spec examples: "18 Oct 2026, 08:48"). They render in the org time zone (spec: Asia/Dubai for the demo).
* Record codes are bold in tables (PROP-0001, ENT-0001).
* Names have a secondary muted line under them where the spec shows one (for example "Company" under a party name, or an item's sub-type).
* **Vocabulary rule (project-owner constraint):** core screens use only industry-neutral terms. These are *workspace / company* (the SaaS customer account, never "tenant" in the UI), *legal entity*, *business unit*, *party* (customer, vendor, employee), *item*, *record*, and *document*. Industry terms (property, unit, lease, tenant, PDC, Ejari…) appear **only** inside screens that a module contributes. Industry-specific glossary terms (spec §2.1) are used verbatim inside their module's screens.
* Localised regulatory labels (TRN, Emirate, MOHRE, Emirates ID…) are **not** hard-coded. They come from the workspace's country pack (spec §9 "Localization layer"). The core form shows a generic label, e.g. "Tax registration no.", and the country pack overrides the label, placeholder and validator.
