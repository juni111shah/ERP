# Coordination notes

For: **Advanced UI/UX expert (theme and token owner)** and **Grok Bot (coordinator)**. From: UI/UX expert (screen layouts and IA). Sprint 0.

## A. Tokens and theme — **all resolved** (T1–T15)
The theme owner delivered every token this pack requested. The full name → value mapping, the light / dark / navy / compact values and the measured contrast pairs are in [`../uiux-advanced/token-mapping.md`](../uiux-advanced/token-mapping.md) (source: `../uiux-advanced/theme/tokens.scss`). This pack writes every token as `--erp-<name>` and never defines values.

| ID | Group | Status and pointer |
|---|---|---|
| T1 | Shell colours (`--erp-sider-*`, `--erp-header-bg`, `--erp-canvas-bg`, `--erp-surface-bg`, `--erp-border-subtle`) | **Resolved** (token-mapping §1 T1). Default = light sider with the green brand; the spec's navy is the **Classic** look via `<html data-nav-theme="dark">` (per-workspace setting). |
| T2 | Brand (`--erp-primary` → `--erp-color-primary`, hover/active/disabled, `--erp-accent`, `--erp-hero-*`) | **Resolved** (T2); white-label via `TenantThemeService.applyBranding()`. |
| T3 | Text (`--erp-text-primary`, **`--erp-text-muted`** (was `text-secondary`), `--erp-text-disabled`, `--erp-text-on-primary`, `--erp-link`) | **Resolved** (T3); muted darkened to pass AA. |
| T4 | Tones `--erp-tone-{success,neutral,warning,error,info,orange,plain}-{bg,fg,border,dot}` | **Resolved** (§2.2). Status tags **and** the fixed custom-field option palette (Green, Slate, Amber, Red, Blue, Orange, Grey). Tone `attention` → `orange`. |
| T5 | `--erp-kpi-accent-{gold,primary,info,warning,error}` | **Resolved** |
| T6 | `--erp-money-negative`, `--erp-money-zero` | **Resolved** |
| T7 | `--erp-focus-ring` (box-shadow ring) / **`--erp-focus-ring-color`** (colour), `--erp-highlight-bg`, `--erp-overlay-backdrop`, `--erp-skeleton-base`, `--erp-skeleton-shimmer` | **Resolved** |
| T8 | `--erp-chart-1` … `-8` | **Resolved** |
| T9 | `--erp-spacing-*`, `--erp-radius-*`, `--erp-elevation-*`, `--erp-page-padding`, `--erp-card-gap` | **Resolved** |
| T10 | `--erp-font-family`, `--erp-type-<role>` (font shorthand) | **Resolved** |
| T11 | `--erp-header-height` **56 comfortable / 48 compact**, `--erp-sider-width(-collapsed)`, `--erp-drawer-width-md/lg/xl` (640/880/1120), **`--erp-form-max-width`** (was `content-max-width`), `--erp-table-row-height-*` | **Resolved**; closes Q11. |
| T12 | `--erp-motion-*` (0 ms under reduced motion) | **Resolved** |
| T13 | Breakpoints = ng-zorro defaults (`--erp-bp-*` informational) | **Resolved** |
| T14 | `data-theme`, `data-density`, `dir`, `data-nav-theme`, white-label | **Resolved** |
| T15 | ng-zorro patches (menu pill white + navy, table, tags, steps, drawer footer, segmented, header height) | **Resolved** |

AA pairs of 10 §4 are verified in token-mapping §2.

## B. Resolved decisions (coordinator reply, 22 Oct 2026)
| ID | Decision | Where applied |
|---|---|---|
| D1 | **Approved, with one change.** HR & payroll is an optional module. Property, leasing, facilities and brokerage belong to the real-estate module. **Change:** cheques/PDCs are a **core Finance payment instrument** (`/finance/cheques`, permissions `finance.cheques.*`). The real-estate module only links cheques to leases and instalments. | 08 §3, §4, sitemap · 09 §2, §4–7 · 04 §D examples · 05 §A alerts |
| D2 | **Approved:** drawer is the default form mode for master data. | 06 §6 (unchanged) |
| D3 | **Approved:** namespaced UI routes. **Module APIs live under `/api/v1/m/{key}/`** (manifest `apiBase`, `ModuleHttpClient`). | 08 §2, §5 · 09 §1, §3, §8 |
| Q1 | **Resolved:** users sign in by email. One email can belong to several workspaces, and a **workspace picker** appears after login when there is more than one. **Switch workspace** is in the v1 **header** (07 §2, QA-4) and also in the user menu. UI word: **Workspace**; backend name: **Organization**. The spec's "Sign-in key" becomes a read-only **Workspace ID**. | 01 §A–B · 07 §2 · 00 §4 · 03 Step 1 · 05 §A |
| Q2 | **Resolved:** new workspaces come from **self sign-up** (name, email, password → verify email → onboarding wizard) **and** from **platform-admin creation** (invite the first admin). | 01a (new) · 03 "Who and when" |
| Q4 | **Approved:** `<module>.<resource>.<action>`. The two-column fallback is dropped. The spec's two-part keys are mapped to three-part keys (04 §D table). | 04 §D · 08 §2 · all permission examples |
| Q6 | **Resolved:** passwords need ≥ 10 characters with an upper-case letter, a lower-case letter and a digit. Reset tokens are single-use and expire after 30 min. Lockout: 5 failed attempts → 15 min. Round 3 adds "not equal to the email" and a generic lockout message. | 01 (policy, lockout countdown) · 01a · 02 (helper text, 5-item checklist, token states) |
| Q10 | **Resolved:** business units are in v1 as **data plus a selector** (a header context selector and Business unit fields on records). Strict data scoping comes later. | 00 §4 · 05 §D (new) · 07 §2 · 04 §B (default business unit) · 08 §3–4 · 10 §1 |

### Final decisions (coordinator, 22 Oct 2026, second reply)
| ID | Decision | Where applied |
|---|---|---|
| Q12 | **Resolved:** permission resources are plural (`finance.cheques`, `core.parties`), and record-type names are singular (`core.cheque`, `realestate.property`). | 04 §D, 08 §2 (unchanged) |
| Q-C1 | **Resolved:** cheques are both **incoming** (from customers) and **outgoing** (to vendors). A direction field, direction-specific statuses and actions, Incoming/Outgoing/All filters, and the outgoing permissions `issue`, `stop`, `void` (round 3: aligned to BA §8; `handOver`, `cancel`, `bulkClear`, `bulkDeposit` removed). Real-estate lease links apply to incoming cheques only. | 08 §4, §4a (new), sitemap · 04 §D · 09 §4–5 |
| S1 | **Resolved:** sign-up, reset and invite never reveal whether an account exists. Sign-up with an existing email shows the same neutral "check your email" screen, and the owner gets a "you already have an account" email. Admin invites show the same confirmation either way. | 01a §A–B, §D · 04 §B · 01 policy table · 02 (already neutral) |
| S2 | **Resolved:** the platform-admin back office is out of scope for now. Only the invitee's experience is specified. | 01a §D |
| Q6b | **Resolved:** a successful reset clears the lockout. Change-password failures count toward the lockout. The verification link lasts 24 h. The resend cooldown is 30 s. | 01 policy and states · 01a §B · 02 policy, §A, §C |
| BU1 | **Resolved:** one business unit per record. Required is a per-record-type setting, optional by default (Business units → Record types tab). | 05 §D |
| Q5 | **Resolved:** role templates are copied once at creation, with no inheritance, no template markers and no reset-to-template. | 04 §C–D |
| Q9 | **Resolved:** no "coming soon" industries. Onboarding shows only registered modules' industries plus "Other / General business". | 03 Step 2 · 08 §5 / 09 §1 (descriptor field `availability` removed) |

### QA review and alignment (round 3, 22 Oct 2026) — all resolved
| ID | Decision | Where applied |
|---|---|---|
| QA-1 | "Keep me signed in" = **30 days**. | 01 §A |
| QA-2 | Lockout message is generic with the retry time only: "Too many attempts. Try again in 14:32." The "attempts left" warning is **removed**. Rate-limit message uses the same wording. | 01 policy and states · 02 §C |
| QA-3 | No workspace field or selector on the login card; the picker appears only after a correct password (and 2FA). | 01 §A–B |
| QA-4 | Workspace switcher is in the v1 **header**; the user-menu entry stays as a secondary path (< md only path). | 07 §1–2 · 10 §1 · 08 sitemap |
| QA-5 | Reset links expire after **30 min** everywhere. Password must **not equal the email** (re-added). "Sign me out of other devices" is optional (default unchecked). | 01 policy · 01a · 02 |
| QA-6 | Permission keys `<module>.<resource>.<action>`, lower case, per the BA catalogue (`core.users.manage`, `core.roles.manage`, `core.settings.manage`, `core.audit.read`, `core.approvals.decide`, `core.parties.*`, `core.legalentities.*`, `core.customfields.manage|read`, `core.sensitivedata.read`, `finance.*`, `realestate.*`). All `admin.*`, `customization.*`, `sales.*`, `purchases.*`, `realEstate.*` and camelCase resources removed. | all files (04 §D catalogue) |
| QA-7 | Invoices, receipts, bills and payments are **core Finance** (`/finance/invoices|receipts|bills|payments`, `finance.*`). No Sales/Purchases groups. The RE module owns none of them. | 08 §1–4, sitemap, §5 · 09 §2, §5 · 04 §D |
| QA-8 | Cheque register is core Finance; RE links cheques to leases only through the source-link hook (no Lease/Instalment field on core Cheque). Lifecycles, CHQ/CHO numbering, cheque books and actions follow BA §8. | 08 §4a · 09 §4–5 · 04 §D |
| QA-9 | RE module key `realestate`, routes `/real-estate/`, DB schema `re`, API `/api/v1/m/realestate/`, permissions `realestate.*` (BA §9.1). | 08 §2, §5 · 09 |
| QA-10 / Q8 (limits) | **50 custom fields per record type**, **10 searchable/filterable** (indexed). Counter, limit-reached states, switches disabled at 10 with a tooltip. | custom-fields A.1, A.2.1, A.4, A.6 |
| AL-1 | Layout designer **and** field builder use **draft → publish** (draft badge, unpublished-changes badge, Publish… with version note, Discard draft, version history). "Save layout" removed. | custom-fields A.1, A.2, A.5 |
| AL-2 | Custom-fields permission `core.customfields.manage` (+ `core.customfields.read`). | custom-fields · 04 · 08 |
| AL-3 | Field keys match `^cf_[a-z0-9_]{1,60}$`; generated from the label, editable before first save, immutable after, inline validation. | custom-fields A.2.1, B.5 |
| AL-4 | v1 = the BA's 15 field types (Percent added; File/attachment moved to later releases). The picker shows only `GET /api/v1/metadata/capabilities` types. Type change only for the BA widenings. | custom-fields A.3, A.6, B · 06 |
| AL-5 | Module manifest standard = `../uiux-advanced/specs/app-shell.md` §2; 08 §5 now references it (`nav`, `alerts`, `dashboard.widgets`, `PermissionArea`). | 08 §5 · 09 §1 |
| AL-6 / Q7 | Saved views can be shared only by users with `core.settings.manage`. | 06 §3 |
| AL-7 / Q11 | Theme: light sider + green brand by default, navy via `data-nav-theme="dark"`; header 56 / 48 compact; `--erp-*` tokens; tones = option palette (`orange`, not `attention`). | 07 · 10 · 00 · 06 · custom-fields · §A |
| AL-8 | Cheque-due alert days are a Finance setting with a per-legal-entity override; lease-expiry days are a `realestate` setting; neither is a column on core Organization. | 05 §A, §B |

## C. Still open (non-blocking; defaults are applied in the specs)
| ID | Item | Default used |
|---|---|---|
| Q3 | Backend endpoints these screens call that are not in spec §12 (onboarding, industries/modules, sign-up and verification, select-workspace, invitations, password policy, `X-Business-Unit-Id`, saved views, field metadata) | Proposed in each file; the backend agent owns the final shapes |
| Q8 (storage) | Custom-field storage | The DB agent decides (BA §9.7 `CustomFieldIndex`). Limits are resolved (QA-10). |
| X1 | Standard manifest still uses `key: 'real-estate'` + `keyCamel`, camelCase `PermissionArea` examples (`realEstate.leases.activate`), and core nav groups `sales` / `purchases`. | This pack uses `key: 'realestate'`, proposed `routePrefix: 'real-estate'`, lower-case keys and no sales/purchases groups (08 §5). Theme owner to update app-shell.md §2. |
| X2 | BA lists work orders and deals under separate modules (`facilities.workorders.*`, `brokerage.deals.*`); coordinator D1 put them in the RE module. | Kept in RE as `realestate.workorders.*` / `realestate.deals.*` (09 §4). |
| X3 | BA calls the unit "Branch" (`core.branches.*`); the coordinator decision uses **Business units**. | `core.businessunits.read|manage` (05 §D). |
| X4 | QA example `core.users.write` vs the BA's `core.users.manage`. | Followed the BA (`manage`). |
| X5 | BA Organization still lists a "sign-in key"; the decided login is email + workspace picker with a read-only Workspace ID. | 01, 03, 05 unchanged. |
| X6 | BA module API examples (`/api/v1/properties`) vs approved D3 `/api/v1/m/realestate/`. | Followed D3 / QA-9. |
| X7 | QA ruling: the view action key is **`.read`**; the BA catalogue (§3.3, §9.1, CF-SEC-02, BR-SEC-08) uses `.view` (e.g. `core.customfields.view`, `core.sensitivedata.view`, `realestate.leases.view`). | Followed QA: every key in this pack ends in `.read` (`core.customfields.read`, `core.sensitivedata.read`, `finance.journals.read`, `realestate.properties.read`…). The matrix column label stays **View**, and its key is `.read` (04 §D). The BA and the standard manifest (app-shell.md §2) need to switch to `.read`. |

## D. Hand-offs to other agents (FYI)
* **BA:** confirm the business rules flagged as *assumption* in 03, 04, 05 and custom-fields.md.
* **DB:** custom-field storage and metadata versioning; saved views table; onboarding state; role and permission tables for the three-part keys; one user ↔ many Organizations (memberships, invitations); BusinessUnit table (hierarchy) plus a single nullable `BusinessUnitId` on opted-in resources, with a per-record-type `Required` flag; core `Cheque` per BA §8 (`Direction`, CHQ/CHO numbering, cheque books, direction-specific statuses, generic **source links** rows). No module columns (e.g. LeaseId) on core tables; RE uses schema `re`.
* **Full-stack:** shared components and contracts in 06 §8, custom-fields.md §B.5 and 08 §5. Guards and route data in 08 §2.
* **SQA:** the state tables in each file are test oracles. Accessibility checks are in 10.

## E. Vocabulary guard (project-owner constraint)
Core screens never use: property, unit, lease, tenant, landlord, PDC, Ejari, rent. (Core Finance → Cheques uses neutral wording: "Cheques", with "post-dated" only as a descriptive date status.) "Tenant" in the SaaS sense is called **workspace** in the UI. Industry words live only in module screens (09).
