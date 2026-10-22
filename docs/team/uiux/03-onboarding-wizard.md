# 03 · Onboarding wizard (workspace setup)

Route: `/onboarding` with child routes `/onboarding/company`, `/onboarding/industry`, `/onboarding/legal-entities`, `/onboarding/modules`, `/onboarding/review`.

> **Spec gap.** The spec has no onboarding or sign-up flow. Its demo workspace is pre-seeded (§2.2, §14.1). This wizard is **new design**, built from fields the spec defines elsewhere: Organization settings (§4.25), Legal entities (§4.4), Table `Organization` (§11.2), Localization layer (§9), and Data import wizards (§9, used here only as an optional pointer). Provisioning is **resolved**. New workspaces come from **self sign-up** (01a-sign-up.md → this wizard) or from **platform-admin creation** (01a §D; the first admin is invited and lands here).

## Who and when
* **Self sign-up:** shown right after email verification (01a §B–C). Step 1 creates the `Organization` record (UI: workspace), and the user becomes its Workspace admin.
* **Existing user creating another workspace** (workspace picker → "Create a new workspace"): starts at Step 1 with no account step.
* **Platform-admin created:** the invited first admin lands here after accepting the invitation. Fields the platform admin pre-filled are shown already completed and editable.
* The wizard is shown while `organization.setupStatus != Complete`. Until then, the workspace shows the tag "Setup in progress" in the workspace picker (01 §B).
* A route guard redirects every app route to the step where setup was last saved. Other users who sign in before setup completes see `nz-result` "Your workspace is being set up by <admin name>."
* Requires `core.settings.manage` and `core.users.manage`.

## Shell and step navigation

```
┌────────────────────────────────────────────────────────────────────────────┐
│ ◆ Product            Set up your workspace           Saved 2 min ago · Exit│
├────────────────────────────────────────────────────────────────────────────┤
│ nz-steps (horizontal ≥ lg, vertical-compact "Step 2 of 5" < lg)             │
│ (1) Company ── (2) Industry ── (3) Legal entities ── (4) Modules ── (5) Review│
├────────────────────────────────────────────────────────────────────────────┤
│  Step title                                                                │
│  One-sentence explanation of why we ask                                    │
│  ┌──────────────────────────── step body ─────────────────────────────┐    │
│  │                                                                    │    │
│  └────────────────────────────────────────────────────────────────────┘    │
├────────────────────────────────────────────────────────────────────────────┤
│ [ Back ]                                [ Save & exit ]  [ Continue → ]    │  sticky footer
└────────────────────────────────────────────────────────────────────────────┘
```

| Behaviour | Rule |
|---|---|
| Step component | `nz-steps [nzCurrent]` with `nzStatus` per step: `finish` for valid and saved steps, `process` for the current step, `error` for a visited step that is now invalid (for example after a change in Industry), and `wait` for steps not reached. |
| Clicking a step | Allowed for any step ≤ furthest reached step (`nzClickable`). Forward jumps beyond the furthest step are disabled. |
| Continue | Validates the current step, saves it (`PUT /onboarding/{step}`, **proposed API**), then advances. The button shows `nzLoading` while saving. |
| Back | Goes to the previous step without validation. Dirty changes are auto-saved as a draft. |
| Save & exit | Saves a draft of the current step (no validation). A user with other workspaces goes to the workspace picker; anyone else is signed out to `/auth/login`. Opening this workspace again resumes on the saved step. |
| Auto-save | Debounced 2 s after the last change, and on `visibilitychange`. Header text reads "Saving…", then "Saved hh:mm", or "Couldn't save · Retry" (`--erp-color-error-text`). |
| Resume | On load, `GET /onboarding` returns `{currentStep, steps:{company:{status,data},…}}`. The wizard restores data and positions the user on `currentStep`. |
| Exit | "Exit" in the header behaves like Save & exit, after an `nz-modal` confirm. |

## Step 1 · Company details
"Tell us about your company. This appears on documents and sets your defaults."

```
┌ Company profile ───────────────────────────────────────────────┐
│ Display name*            [Acme Group                        ]  │
│ Registered (group) legal name (optional) [Acme Holdings Ltd ]  │
│ Contact email*           [admin@acme.com                    ]  │
├ Regional settings ─────────────────────────────────────────────┤
│ Country*   [United Arab Emirates ▾]   Base currency* [AED ▾]   │
│ Time zone* [Asia/Dubai ▾]             Date format [dd MMM yyyy▾]│
│ Language   [English ▾]                                         │
│ Logo (optional) [ ⬆ Upload ]  PNG/SVG ≤ 1 MB                   │
└────────────────────────────────────────────────────────────────┘
```

| Label | Type | Control | Req. | Validation | Placeholder / help |
|---|---|---|---|---|---|
| Display name | text | `nz-input` | Yes | 2–100 chars | "e.g. Acme Group". Spec §4.25 "Display name". |
| Registered (group) legal name | text | `nz-input` | No | ≤ 200 | Spec "Group legal name". |
| Contact email | email | `nz-input type=email` | Yes | Email | Prefilled from the admin's email. Spec says Optional; it is made required here so that system notices have a recipient. **Assumption.** |
| Country | select | `nz-select nzShowSearch` | Yes | From the country list | This selects the **country pack** (tax labels, ID validators, regions; spec §9 Localization). Changing it re-labels later steps. |
| Base currency | select | `nz-select nzShowSearch` | Yes | ISO 4217 | Defaulted from Country. Spec "Country / currency" is read-only after setup, so a help text warns: "Can't be changed after setup." |
| Time zone | select | `nz-select nzShowSearch` | Yes | IANA | Defaulted from Country, or from the browser time zone when it is valid. |
| Date format | select | `nz-select` | No | — | Default `dd MMM yyyy` (spec convention). |
| Language | select | `nz-select` | No | — | English (Arabic and others come later; spec §9 "Arabic RTL"). |
| Workspace ID | read-only | text | — | — | Auto-generated, unique, and shown later in Settings. It replaces the spec's "Sign-in key" (§4.25), because sign-in is now by email (01). Used in support and API contexts only. Not shown in the wizard; listed here for completeness. |
| Logo | file | `nz-upload nzListType="picture"` | No | PNG/SVG/JPG, ≤ 1 MB (**assumption**) | Used in the sidebar and on printed documents. |

## Step 2 · Industry selection
"What does your business do? We'll suggest the right modules. You can add or remove modules any time."

```
┌────────────────────────────────────────────────────────────────┐
│ [🔍 Search industries…                     ]                   │
│ ┌──────────────┐ ┌──────────────┐ ┌──────────────┐ ┌──────────┐│ ← example cards; all come from module manifests
│ │ (icon)       │ │ (icon)       │ │ (icon)       │ │ (icon)   ││
│ │ Real estate  │ │ Trading &    │ │ Manufacturing│ │ Services ││
│ │ Property mgmt│ │ distribution │ │              │ │          ││
│ │ 6 modules    │ │ 4 modules    │ │ 5 modules    │ │ …        ││
│ │      (•)     │ │      ( )     │ │      ( )     │ │   ( )    ││
│ └──────────────┘ └──────────────┘ └──────────────┘ └──────────┘│
│ ┌──────────────────────────────────────────────────────────────┐│
│ │ ( ) Other / General business — core modules only             ││
│ └──────────────────────────────────────────────────────────────┘│
│ ▸ My industry isn't listed  → [ Tell us (optional) ______ ]     │
└────────────────────────────────────────────────────────────────┘
```

* **The list is fully data-driven.** The cards come from `GET /platform/industries` (**proposed**). That endpoint aggregates the industry descriptors that registered modules declare in their manifest (`industries[]`, see 08-navigation-ia.md §5). Core has no hard-coded industry. Real estate appears only because the real-estate module registers it.
* Card fields: `key`, `label`, `description`, `icon`, `suggestedModules[]`, `sortOrder`. **Only industries of modules that are registered and installed are listed (resolved Q9).** There are no "coming soon", preview or disabled cards, and no "Notify me". The example cards beyond Real estate in the sketch appear only once such modules are registered.
* "Other / General business" is always present. It is pinned last and maps to core modules only.
* Rendered as an `nz-radio-group` of selectable cards (each card is a `label` wrapping an `input type=radio`). The cards are arranged in a CSS grid of 4/3/2/1 columns at xl/lg/md/xs.
* **Assumption:** there is one primary industry. Modules from other industries can still be added in Step 4. If only one industry module is registered (today: Real estate), the grid shows that card plus "Other / General business".

| Label | Type | Control | Req. | Validation | Help |
|---|---|---|---|---|---|
| Industry | single select (cards) | `nz-radio-group` of cards | Yes | One registered industry, or Other/General | "This only pre-selects modules." |
| Search industries | text | `nz-input` with search icon | No | — | Filters cards client-side by label and keywords. Filtered empty state: "No match. Choose *Other / General business* and add modules later." |
| Tell us your industry | text | `nz-input` | No | ≤ 100 | Shown when "My industry isn't listed" is expanded (`nz-collapse`). Product feedback only. |

States:

| State | Display |
|---|---|
| Loading | 8 skeleton cards (`nz-skeleton-element nzType="avatar"` + lines). |
| Error | `nz-result warning` with **Retry**. "Other / General business" stays selectable so the user is never blocked. |
| Change after Step 4 | `nz-modal` confirm: "Changing industry will reset your module selection to the suggestions for <new>. Keep custom selections?" with the options **Reset to suggestions** / **Keep my selection**. |

## Step 3 · Legal entities
"Add the companies that keep their own books. Each one issues its own documents and runs its own payroll." (wording adapted from spec §2.2)

```
┌────────────────────────────────────────────────────────────────┐
│ Legal entities (1)                              [ + Add entity ]│
│ ┌────────────────────────────────────────────────────────────┐ │
│ │ ENT-0001 (auto) · Acme Trading LLC · Dubai · Tax reg. ✓    │ │
│ │                                         [Edit] [Remove]    │ │
│ └────────────────────────────────────────────────────────────┘ │
│ ⓘ Start with one. You can add more later in Settings → Legal   │
│   entities, or import many at once.                            │
│ [✓] Use my company details for the first entity  (shortcut)    │
└────────────────────────────────────────────────────────────────┘
```

* Each row is an `nz-list` item. **Add entity** and **Edit** open the **same drawer form** as Settings → Legal entities (05-organization-legal-entities.md §B). Onboarding mode shows only the *Essentials* and *Tax* sections; *Payroll registration*, *Banking* and module-contributed sections are collapsed, with the note "You can complete these later".
* The shortcut checkbox prefills Legal name, Email and Address from Step 1.

| Rule | Detail |
|---|---|
| Minimum | At least 1 entity to continue. An empty-state `nz-empty` shows "Add your first legal entity" with a primary button. |
| Remove | `nz-popconfirm` "Remove <name>?" (nothing exists yet, so the removal is hard). |
| Code | Auto-number when blank (spec §4.4 "Leave blank to auto-number"). |
| Region label | Comes from the country pack. UAE: "Emirate of registration" with 7 options (spec §4.4). Others: "State / province", or hidden. |

## Step 4 · Modules
"Choose what you'll use. Core is always on. You can change this later in Settings → Modules."

```
┌ Core (always included) ──────────────────────────────────────────┐
│ ✓ Dashboard & approvals  ✓ Finance & accounting  ✓ Parties       │
│ ✓ Users & roles  ✓ Audit log  ✓ Documents                        │
├ Suggested for Real estate ─────────────────────────── [Select all]│
│ [✓] Assets & portfolio        Track assets/locations…  (requires: –)│
│ [✓] Contracts & billing       …                         (requires: Assets)│
│ [ ] Facilities                …                                   │
├ Other available modules ──────────────────────────────────────────┤
│ [ ] HR & payroll              Employees, attendance, payroll      │
│ [ ] Payables                  Vendors, bills, payments            │
└───────────────────────────────────────────────────────────────────┘
 Summary: 4 modules selected · Sidebar preview ▸
```

* Data-driven from `GET /platform/modules` (**proposed**). Each module manifest exposes `key`, `label`, `description`, `industry?`, `requires[]`, `tier/plan`, and `isCore`.
* Control: grouped `nz-checkbox-wrapper` cards. Core modules are rendered checked and disabled, with the tooltip "Included in every workspace".
* **Dependencies:** checking a module auto-checks its `requires[]` modules. A message (`nz-message info`) reads "Also added Assets & portfolio (required by Contracts & billing)". Unchecking a module that others require opens an `nz-popconfirm` that lists the dependents, which are unchecked too if the user confirms.
* **Plan gating:** a module not included in the plan shows a "Plan: Pro" `nz-tag` and is disabled, with an "Upgrade" link. **Assumption:** the spec lists `Plan` (Demo) without plan rules.
* **Sidebar preview** (`nz-collapse` panel) renders the resulting `nz-menu` tree read-only, so the admin sees the navigation that will result.
* Which modules count as core and which as industry modules is a proposal (see 08-navigation-ia.md §3). The coordinator needs to confirm it.

## Step 5 · Review & finish

```
┌ Company ──────────────────────────────────────────── [Edit] ┐
│ Acme Group · admin@acme.com                                 │
│ United Arab Emirates · AED · Asia/Dubai · English           │
├ Industry ─────────────────────────────────────────── [Edit] ┤
│ Real estate                                                 │
├ Legal entities (2) ───────────────────────────────── [Edit] ┤
│ ENT-0001 Acme Trading LLC · Dubai                           │
├ Modules (5) ──────────────────────────────────────── [Edit] ┤
│ Core + Assets & portfolio, Contracts & billing, …           │
├─────────────────────────────────────────────────────────────┤
│ ⚠ Base currency and country can't be changed later          │
│ Next: [✓] Invite teammates after setup                      │
└─────────────────────────────────────────────────────────────┘
                          [ Back ]   [ Finish setup ]
```

* Each section uses `nz-descriptions`. **Edit** jumps to its step, and Continue then returns the user to Review.
* Steps with a validation problem show `nz-alert error` in their section, and **Finish setup** is disabled with the tooltip "Fix the highlighted sections".
* **Finish setup** calls `POST /onboarding/complete` (proposed). It provisions the chart of accounts from the country pack, the number sequences, the default roles, and the module seeds. Progress appears in an `nz-modal` (not closable) with `nz-steps` vertical items such as "Creating ledgers ✓", "Setting up modules …", fed by SignalR progress (pattern from spec §13 billing-run progress).
* **Success:** `nz-result success` reads "Your workspace is ready." Primary: **Go to dashboard**. Secondary: **Invite teammates** (→ `/admin/users?invite=1`) and **Import data** (→ Settings → Import data, spec §9).
* **Failure:** `nz-result error` shows the failed step and **Retry**. Completed steps are idempotent (backend assumption).

## Global states (all steps)

| State | Display |
|---|---|
| Initial load | Skeleton of the steps bar and form. |
| Save error | Header shows "Couldn't save · Retry", and Continue stays on the step with `nz-alert error`. |
| Validation | Field-level errors and the error summary (00 §2). |
| Session expired mid-wizard | Data is already auto-saved. After re-login the user resumes on the same step. |
| Offline | `nz-alert warning` sticky: "You're offline. Changes will save when you reconnect." Continue is disabled. |
