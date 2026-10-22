# UI/UX: screen layouts and information architecture (Sprint 0)

Owner: UI/UX expert · Frontend: Angular + ng-zorro-antd · Source spec: `../the reference demo-Complete-Build-Spec.pdf` / `../spec.txt` (section numbers cited as "spec §x").
Product: a **general-purpose ERP**. The core is industry-neutral, and industry features come from pluggable modules. Real estate is the first *example* module (09). Design tokens and theme are owned by the Advanced UI/UX expert. These specs reference semantic token names only.

| File | Contents |
|---|---|
| [00-shared-conventions.md](00-shared-conventions.md) | Page anatomy, form rules, universal states, permissions in the UI, vocabulary rule (workspace, legal entity, business unit, party, item, record) |
| [01-login.md](01-login.md) | Approved auth policy; sign in by email (generic 5-attempt / 15-min lockout message with countdown, 30-day "keep me signed in", 2FA, session expiry); **workspace picker** only after a correct password (multiple workspaces, invitations) |
| [01a-sign-up.md](01a-sign-up.md) | Self sign-up (name, email, password with policy checklist → verify email → onboarding) and the platform-admin creation / invite path |
| [02-password-recovery.md](02-password-recovery.md) | Forgot password (request → email confirmation → reset → success) with 30-min single-use tokens and the 5-rule checklist (incl. not equal to email); optional sign-out of other devices; change password; forced change |
| [03-onboarding-wizard.md](03-onboarding-wizard.md) | 5-step workspace setup: company, data-driven industry selection (+ Other/General), legal entities, modules, review; save/resume |
| [04-users-roles-permissions.md](04-users-roles-permissions.md) | User list and drawer form, role list, role form with module × resource × action permission matrix (bulk toggles, inheritance) |
| [05-organization-legal-entities.md](05-organization-legal-entities.md) | Workspace settings (sectioned, single save), legal-entity list, drawer and lock/reopen dialog, module management, **business units** (v1: data + selector) |
| [06-master-data-pattern.md](06-master-data-pattern.md) | Reusable list and drawer-form pattern: filter bar, saved views, table, column settings, bulk actions, states, **Angular component contract** |
| [custom-fields.md](custom-fields.md) | Studio-style custom fields: admin screens (field list, add/edit drawer, type picker, options editor, validation, visibility, layout designer), BA rules (15 v1 types via capabilities, 50 fields / 10 indexed limits, `cf_` keys, draft → publish, deactivate/delete, widen-only type changes), and integration into forms, columns, filters, views, export/import, plus the metadata contract |
| [07-app-shell.md](07-app-shell.md) | Header, sider, breadcrumb, legal-entity switcher, business-unit context selector, global search, notification drawer, **header workspace switcher**, user menu, theme (light default / navy via `data-nav-theme`), shell states |
| [08-navigation-ia.md](08-navigation-ia.md) | IA principles, UI route / API (`/api/v1/m/{key}/`) / permission naming, approved core vs module split (invoices, receipts, bills, payments and cheques in core Finance), core menu tree, cheque coverage per BA §8, **module manifest = reference to uiux-advanced app-shell.md §2**, mermaid sitemap |
| [09-module-example-real-estate.md](09-module-example-real-estate.md) | Example module: how real estate plugs in (key `realestate`, `/real-estate/`, schema `re`, manifest, nav, BA permission keys, role templates, cheque source-link hooks, reports) |
| [10-responsive-accessibility.md](10-responsive-accessibility.md) | Breakpoint behaviour per element, keyboard and focus, ARIA, contrast pairs (by `--erp-*` name) the tokens must satisfy, RTL |
| [coordination-notes.md](coordination-notes.md) | Tokens T1–T15 (**resolved**, see `../uiux-advanced/token-mapping.md`), **resolved decisions** (D1–D3, Q1, Q2, Q4–Q6b, Q7, Q9–Q12, Q-C1, S1, S2, BU1, QA-1…QA-10, AL-1…AL-8), cross-team conflicts X1–X6, remaining non-blocking items, hand-offs |

Reading order: 00 → 08 → 07 → 06 → custom-fields → 01, 01a, 02–05 → 09 → 10 → coordination-notes.

Spec gaps covered by new design (flagged in each file): sign-up and workspace picker, business units, onboarding wizard and industry selection, self-service password reset screens, custom roles and the fine-grained matrix, saved views, custom fields, lock/reopen dialog, temporary-password result dialog, module management.
