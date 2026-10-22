# 04 · Users, roles and permission matrix

Routes: `/admin/users`, `/admin/users/:id`, `/admin/roles`, `/admin/roles/new`, `/admin/roles/:id`.
Spec: §2.3 (built-in roles), §4.26 (Users & roles), §7.1–7.3 (permission keys, matrix, security), §8 #7 (custom roles), §9 "Custom roles", §10.4 (JWT `perms`, `ents`, `ver`), §12 (`/users`, `/roles`).
Permission: `core.users.manage` for everything on these pages.

> The spec shows users and roles on one page, `/users`, with two tables. Here they are split into two sub-pages under **Administration → Users & roles**, presented as `nz-tabset` tabs that link to routes, so each one keeps its own URL, filters and saved views. The spec's roles are fixed (§4.26). Custom roles are a spec §9 "Should" and are designed here because the generic ERP needs them for modules.

---
## A. User list — `/admin/users`
Built on the generic master-data pattern (06-master-data-pattern.md) with `resourceKey: 'core.user'`.

```
Administration / Users & roles
Users & roles   Who can sign in, what they can do, and which legal entities they can see.
[ Users (12) | Roles (9) ]                                         [+ Invite user]
┌───────────────────────────────────────────────────────────────────────────┐
│ [🔍 Name or email…][⌕] [Role ▾] [Legal entity ▾] [Status ▾] Views▾  ⟳ ⤓ ⚙ │
├───────────────────────────────────────────────────────────────────────────┤
│ ☐ │ Name                 │ Roles               │ Legal entities │ Last sign-in       │ Status   │ ⋮ │
│ ☐ │ Demo Admin  (You)    │ [Workspace admin]   │ All            │ 22 Oct 2026, 16:27 │ Active   │ ⋮ │
│   │ admin@acme.com       │                     │                │                    │          │   │
│ ☐ │ Neha Kapoor          │ [Accountant]        │ 2 of 3         │ Never              │ Invited  │ ⋮ │
├───────────────────────────────────────────────────────────────────────────┤
│ 2 selected: [Assign role] [Deactivate] [Resend invite]       12 users ‹1› 25/page │
└───────────────────────────────────────────────────────────────────────────┘
```

### Columns
| Column | Content | Sort | Notes |
|---|---|---|---|
| ☐ | Bulk select | — | The checkbox is disabled on the "You" row for destructive bulk actions. |
| Name | Bold full name, "(You)" `nz-tag`, email as the muted second line | ✓ | Spec §4.26 |
| Roles | `nz-tag` per role, up to 2, then "+N" with a tooltip listing the rest | — | |
| Legal entities | "All" or "n of N" with a tooltip listing names | — | Spec "Legal entities" |
| Last sign-in | Timestamp or "Never" | ✓ | Spec format |
| Status | StatusTag: Active (success), Invited (neutral), Inactive (plain), Locked (error) | ✓ | Invited and Locked are **new** statuses (**assumption**) |
| Two-factor | ✓ / — | — | Optional column, hidden by default (spec §9 2FA) |
| ⋮ | Row actions | — | Pinned right |

### Filters
Search (name, email), Role (multi), Legal entity (multi), Status (multi).

### Row actions (⋮) and permissions
| Action | Shown when | Behaviour |
|---|---|---|
| Edit | always | Opens the user drawer in edit mode |
| View effective permissions | always | Drawer tab "Effective access" (see §B) |
| Reset password | status ≠ Invited | `nz-modal` confirm, then a result dialog that shows the temporary password once (monospace, **Copy** button, "Shown only once. The user must change it at next sign-in."). This designs the spec's uncaptured "Reset password result" (§8.1). Self-service email reset is also triggered: "Also email a reset link" checkbox, default on. |
| Resend invite | status = Invited | Toast "Invite sent" |
| Deactivate / Reactivate | not "You" | `nz-popconfirm`. Deactivate revokes sessions. |
| Unlock | status = Locked | Immediate action, then a toast |

### Bulk actions
Assign role (adds a role to all selected users, via an `nz-modal` with a role multi-select), Remove role, Deactivate (`NzModalService.confirm` danger), Resend invite. Bulk actions skip "You" and report "3 updated, 1 skipped (your own account)".

### States
* Empty (only "You" exists): the table shows that row plus an inline `nz-empty` card below it: "Invite your team" with **Invite user**.
* Other states follow the pattern defaults (06 §7).

---
## B. User form — drawer (`nz-drawer`, width lg; full screen on < md)
Title: "Invite user", "Edit <name>" or "<name>" (view). Spec §4.26 uses a modal. A drawer is used here per the master-data pattern decision (see coordination-notes.md D2).

```
┌ Edit Neha Kapoor ───────────────────────────────────── ✕ ┐
│ [ Details | Effective access | Activity ]                 │
│ Full name*            [Neha Kapoor                     ]  │
│ Email (sign-in)*      [neha@acme.com      ] 🔒 locked     │
│ Roles*                [Accountant ×] [+ ]           ▾     │
│   ⓘ Accountant: Day-to-day billing, payables and journals.│
│     Cannot approve.                                       │
│ Legal entity access   (•) All legal entities              │
│                       ( ) Only selected entities          │
│                           [☐ ENT-0001 …  ☐ ENT-0002 …]    │
│ Default business unit (opt.) [Head office ▾]              │
│ ── Sign-in ──                                             │
│ Invite method         (•) Email invite link               │
│                       ( ) Set initial password            │
│ Initial password      [••••••••  ⟳ Generate]              │
│ [✓] Active (can sign in)                    (edit only)   │
│ [ ] Require two-factor authentication                     │
├───────────────────────────────────────────────────────────┤
│                                   [ Cancel ] [ Save ]     │
└───────────────────────────────────────────────────────────┘
```

| Label | Type | Control | Req. | Validation | Placeholder / help |
|---|---|---|---|---|---|
| Full name | text | `nz-input` | Yes | 2–100 | — |
| Email (sign-in) | email | `nz-input` | Yes | Email; unique in the workspace (async check) | Locked on edit (spec). Lock icon with the tooltip "Email can't be changed after the user is created." |
| Roles | multi-select | `nz-select nzMode="multiple"` with an option template showing the description | Yes (≥ 1) | — | Default `Viewer` (spec). Under the field, the description of each selected role (spec: "description of the selected role shown under the field"). |
| Legal entity access | radio | `nz-radio-group` | Yes | — | Default "All legal entities" (spec) |
| Selected entities | multi-checkbox | `nz-checkbox-group` (or `nz-select multiple` when > 8 entities) | If "Only selected" | ≥ 1 | Shown only when "Only selected entities" is chosen |
| Default business unit | select | `nz-tree-select` (single) | No | Must be an active business unit | **New (v1: data + selector only).** It pre-sets the user's header business-unit selector and the Business unit field on new records. It does **not** restrict what the user can see. Strict scoping by business unit comes later. Hidden when the workspace has no business units. |
| Invite method | radio | `nz-radio-group` | Create only | — | **New**: the email invite is the default. "Set initial password" keeps the spec behaviour ("Initial password", Add only). |
| Initial password | password | `nz-input-group` + Generate button | If method = password | Policy | "The user must change it at first sign-in." |
| Active (can sign in) | checkbox | `nz-checkbox` | No | — | Edit only, default checked (spec). Disabled for "You" with the tooltip "You can't deactivate yourself." |
| Require two-factor | checkbox | `nz-checkbox` | No | — | Phase: spec §9 2FA |

Tabs:
* **Effective access** (read-only): this tab shows the merged permission matrix for the user (union of roles), with a per-cell source tooltip such as "Granted by Accountant", and the entity scope. It reuses the matrix component (§D) in read-only mode.
* **Activity**: the last 20 audit events for the user (spec §4.27 data). Includes a link to the audit log, filtered to this user.

Actions:

| Action | Kind | Permission | Behaviour |
|---|---|---|---|
| Save / Send invite | Primary | `core.users.manage` | Create shows "Send invite" or "Create user". The toast reads "Invite sent to …". |
| Cancel | Secondary | — | Dirty guard applies |

Rules:
* **Inviting an email that already has an account** (it belongs to another workspace): the UI **never reveals this** (resolved S1). The admin sees the same form, the same "Invite sent to …" confirmation and the same "Invited" status. Behind the scenes, an existing account gets a workspace invitation (picker + email) instead of a new password. The "Set initial password" option is always shown, with the help text: "Applies only if this person doesn't already have an account; otherwise they'll get an invitation."
* **Last-admin guard:** the user cannot remove the Workspace admin role from the last active admin or deactivate them. The error reads "At least one active Workspace admin is required."
* Self-edit of your own roles: allowed only if another active admin exists. A warning `nz-alert` reads "You're changing your own access."
* After a role change, the server bumps `ver` (spec §10.4). The affected user's next request refreshes their permissions. The UI shows nothing extra.

---
## C. Role list — `/admin/roles`

```
[ Users | Roles (9) ]                                   [+ New role]
┌────────────────────────────────────────────────────────────────────┐
│ [🔍 Role name…]  [Type: All ▾ Built-in / Custom]           ⟳ ⤓ ⚙  │
│ Role               │ Description                    │ Users │ Type     │ ⋮ │
│ Workspace admin    │ Full access to the workspace…  │ 1     │ Built-in │ ⋮ │
│ Finance manager    │ Billing, payables, accounting… │ 2     │ Built-in │ ⋮ │
│ Site supervisor    │ Based on Viewer + Work items…  │ 3     │ Custom   │ ⋮ │
└────────────────────────────────────────────────────────────────────┘
```

* Columns: Role (bold), Description, Users (count, links to the user list filtered by role), Type (`nz-tag`: Built-in or Custom), Modules (badges of modules with any grant; optional column), Updated, ⋮.
* Row actions: **View/Edit** (built-in roles open read-only with **Duplicate**), **Duplicate** (a one-time copy, like "Based on"), **Delete** (custom only; blocked when Users > 0, with the message "Reassign the 3 users first", which links to the filtered user list).
* Built-in roles: the spec's 9 roles (§2.3) are seeded. **Neutral naming:** "Org admin" is shown as "Workspace admin". Roles named after real-estate modules (Leasing manager, Facilities manager, Brokerage manager) are **not core**. They are role templates contributed by the real-estate module (09-module-example-real-estate.md). Core seeds: Workspace admin, Finance manager, Accountant, HR manager (only if the HR module is enabled), Approver, Viewer.

---
## D. Role form and permission matrix — `/admin/roles/new`, `/admin/roles/:id`
This is a **full page**, not a drawer, because the matrix needs width.

```
Administration / Users & roles / Roles / Site supervisor
Site supervisor  [Custom]                          [Delete] [Cancel] [Save role]
┌ Details ───────────────────────────────────────────────────────────────────┐
│ Role name* [Site supervisor      ]  Based on (template) [Viewer ▾] (info)   │
│ Description [Views everything; manages work items…                       ]  │
└─────────────────────────────────────────────────────────────────────────────┘
┌ Permissions ───────────────────────────────────────────────────────────────┐
│ [🔍 Filter resources…] [Module: All ▾] [ Show: All | Granted only ]         │
│ Quick presets: [No access] [Read all] [Full access]       Changes: 7 ●      │
├─────────────────────────────────┬──────┬──────┬──────┬──────┬───────┬──────┤
│ Module / Resource               │ View │Create│ Edit │Delete│Export│ Other│
├─────────────────────────────────┼──────┼──────┼──────┼──────┼──────┼──────┤
│ ▾ ▣ finance                     │  ☑   │  ▣   │  ▣   │  ☐   │  ☑   │  2/9 │ ← module row (tri-state)
│     Chart of accounts (accounts)│  ☑   │ [  Manage ☐  ]       │  —   │   —  │ ← resource declares `manage`
│     Journals (journals)         │  ☑   │  ☑   │  —   │  —   │  —   │ post☑ reverse☐ │
│     Cheques (cheques)           │  ☑   │  ☑   │  ☑   │  ☐   │  —   │ 7/10 ▾│
│ ▸ ☐ core                        │  ☐   │  ☐   │  ☐   │  ☐   │  ☐   │  0/6 │
│ ▸ ▣ <Module contributed>        │  …                                         │
├─────────────────────────────────┴──────┴──────┴──────┴──────┴───────┴──────┤
│ Special permissions                                                        │
│ ☐ Decide approvals (core.approvals.decide)  ☐ View audit log (core.audit.read)│
│ ☐ Workspace settings (core.settings.manage) ☐ Manage users (core.users.manage)│
│ ☐ Manage roles (core.roles.manage)  ☐ Manage custom fields (core.customfields.manage)│
│ ☐ View sensitive data (core.sensitivedata.read)                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Matrix model (module × resource × action)
* **Rows:** a **module** row (collapsible, `nz-table` tree data with `nzExpand`), then its **resources** (entities). Rows come from module manifests (`permissions: PermissionArea[]`, see 08 §5), so core has no hard-coded list.
* **Columns:** the BA standard actions (core-domain BR-SEC-01a) **View, Create, Edit, Delete, Export**. Column label **View** = action key **`.read`** (QA ruling, e.g. `finance.journals.read`); the other columns map to `.create`, `.edit`, `.delete`, `.export`. When a master-data resource declares **`manage`** (= read + create + edit + delete) instead of the separate actions, a single **Manage** checkbox spans the Create/Edit/Delete cells. Workflow verbs (`post`, `reverse`, `issue`, `void`, `decide`, `lock`, `reopen`, `deposit`, `clear`, `bounce`…) appear in the **Other** column as an `nz-checkbox-group` popover with a count badge ("7/10").
* **Cell values:** ☑ granted, ☐ not granted, `—` not applicable (the resource doesn't declare that action; the checkbox is not rendered, but `aria-label` text "not applicable" is present).
* **Key format (approved, Q4; QA-6):** `<module>.<resource>.<action>`, **all lower case**, as in the BA permission catalogue (`../ba/core-domain.md` §3.3, the source of truth for core and finance keys). The resource is plural with no separators (`legalentities`, `bankaccounts`, `customfields`, `workorders`); record-type names stay singular (Q12). Examples: `core.users.manage`, `finance.journals.create`, `finance.cheques.deposit`, `core.parties.read`, `realestate.leases.activate`. There are no two-part keys, no `area.read/write`, no `admin.*` and no `customization.*`.
* **Core catalogue used in this pack** (BA §3.3): `core.organization`, `core.legalentities`, `core.businessunits` (BA: `core.branches`, see coordination-notes), `core.users`, `core.roles`, `core.settings`, `core.audit`, `core.approvals` (request, decide), `core.parties`, `core.numberseries`, `core.customfields`, `core.sensitivedata`, `core.items` (UI proposal, not in BA); `finance.currencies`, `exchangerates`, `taxcodes`, `accounts`, `fiscalyears`, `periods` (lock, reopen, postsoftlocked), `journals` (read, create, post, reverse), `bankaccounts`, `invoices` (read, create, issue, void, credit), `receipts` (read, create, allocate, void), `bills` (read, create, submit, void), `payments` (read, create, cancel), `cheques`, `reports` (read, export).
* **Mapping from the spec's area keys (§7.1)**, used to seed built-in roles from the §7.2 matrix (BA mapping): read → `read`, write → every action of the resource. The special keys:

| Spec key | Key in this product |
|---|---|
| `approvals.decide` | `core.approvals.decide` |
| `admin.audit` | `core.audit.read` |
| `admin.settings` | `core.settings.manage` |
| `admin.users` | `core.users.manage` |
| (new) custom fields | `core.customfields.manage` |
| `admin.users` | also `core.roles.manage` |
| `billing.*` / `payables.*` | `finance.invoices.*`, `finance.receipts.*` / `finance.bills.*`, `finance.payments.*` (all core finance) |
| `reports.read` | `finance.reports.read` + each module's `<module>.reports.read` |
| `leasing.*` (PDC register) | `finance.cheques.*` (core). The real-estate module links cheques to leases only (09 §4). |

* **Example rows (Finance, core):** Chart of accounts (`finance.accounts`), Bank & cash (`finance.bankaccounts`), Journals (`finance.journals`; extra actions `post`, `reverse`), Invoices (`finance.invoices`; `issue`, `void`, `credit`), Receipts (`finance.receipts`; `allocate`, `void`), Bills (`finance.bills`; `submit`, `void`), Payments (`finance.payments`; `cancel`), and **Cheques** (`finance.cheques`, incoming and outgoing; BA §8.8 actions `view`, `create`, `edit`, `delete`, `deposit`, `clear`, `bounce`, `replace`, `return`, `issue`, `stop`, `void`; bulk deposit uses `deposit`; see 08 §4a).

### Inheritance and bulk toggles
| Mechanism | UI behaviour |
|---|---|
| Hierarchy (module → resource) | The module-row checkbox in each action column is tri-state (`nzIndeterminate`). Checking it grants that action on all child resources, and unchecking it clears them. The checkbox in the module **name** cell toggles all actions for the whole module. |
| Column toggle | Clicking a column header (a "View ▾" `nz-dropdown`) offers "Grant View on all resources" and "Clear View on all". |
| Implied permissions | Any action **implies `.read`** (column View; BA BR-SEC-01a). Checking any of them auto-checks View, which is then disabled with the tooltip "Required by Edit". Unchecking View clears the dependents after an `nz-popconfirm`. |
| Template (Based on) | **Resolved Q5: copy once at creation, no inheritance.** "Based on" appears only on the create form. Picking a template copies its grants into the new role as a starting point, and the user can then edit freely. The new role keeps no link to the template, so later changes to the template never affect it, and there are no "changed from template" markers or "Reset to template" action. The role detail shows a muted, informational line "Created from Viewer on 22 Oct 2026". Changing the template picker during creation (before saving) asks "Replace current selections with Finance manager's permissions?". |
| Users with several roles | Effective access = the union of all roles (most permissive), shown in User → Effective access. **Assumption** consistent with spec §10.4 claims. |
| Entity scope | **Not** set per role. It is set per user (Legal entity access), as in the spec. A role grants *what*, and the user's scope grants *where*. |
| Presets | No access / Read all (View + Export everywhere) / Full access. Each needs an `nz-modal` confirm when it would remove grants. |
| Special permissions | `core.approvals.decide`, `core.audit.read`, `core.settings.manage`, `core.users.manage`, `core.roles.manage`, `core.customfields.manage`, `core.sensitivedata.read`, as checkboxes (spec §7.1 + BA §3.3). Modules may add their own here. |
| Sensitive data | Masking is controlled by `core.sensitivedata.read` (BA BR-SEC-08). Modules may add narrower keys (e.g. `hr.salaries.read`). |

### Fields and actions
| Label | Type | Control | Req. | Validation |
|---|---|---|---|---|
| Role name | text | `nz-input` | Yes | 2–60; unique in the workspace |
| Based on | select | `nz-select` (built-in + custom roles) | No (create only; one-time copy) | — |
| Description | textarea | `nz-input textarea` (2 rows, counter 300) | No | ≤ 300 |
| Matrix cells | boolean | `nz-checkbox` | — | — |

| Action | Kind | Behaviour |
|---|---|---|
| Save role | Primary | `POST/PUT /roles`. When users are affected, `nz-modal` confirms: "This changes access for 3 users. They'll get the new permissions within a minute." |
| Cancel | Secondary | Dirty guard |
| Delete | Danger outline | Custom roles only, and only with 0 users |
| Duplicate | Secondary (built-in view) | Opens `/admin/roles/new?from=<id>` |

### States
| State | Display |
|---|---|
| Loading | Details skeleton plus 8 skeleton rows in the matrix |
| Built-in role | All controls disabled. Banner `nz-alert info`: "Built-in roles can't be edited. Duplicate it to customise." |
| Unsaved changes | "Changes: 7" badge in the toolbar, and the Save button is enabled. Route-leave guard. |
| Filtered empty | "No resources match." with **Clear filter** |
| Save error | `nz-alert error` at the top. Field errors on Name. |
| Module disabled | Rows for disabled modules are hidden. A footnote reads "2 modules are disabled for this workspace and not shown." |

### Accessibility for the matrix
* `nz-table` with a `<caption>` "Permissions for Site supervisor". The module and resource name cell is a `th scope="row"`, and the action headers are `th scope="col"`.
* Every checkbox has an `aria-label` like "Journals: Create". A tri-state module checkbox has the label "Finance: Create, partially granted" (`aria-checked="mixed"`).
* Keyboard: Tab enters the grid, the arrow keys move between cells (roving tabindex, an `ErpGridNav` directive), Space toggles, and Enter on a module row expands or collapses it.
