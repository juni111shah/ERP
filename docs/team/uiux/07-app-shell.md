# 07 · App shell: header, sider, breadcrumb, switchers, search, notifications, user menu

Spec: §3.3 (sidebar, header), §3.5 (dark mode), §3.6 / §4.32 (narrow layout), §4.28 (global search), §4.29 (bell), §4.30 (legal-entity switcher), §4.31 (avatar menu), §9 (real-time notification centre), §10.2 (signals session store), §10.3 (workspace from token).
Visual reference: `dashboard-reference.png` ("Monitoring Overview"). Layout cues taken from it: grouped, collapsible sider sections with sub-items; a compact header with a scope select and icons on the right; a KPI row; a status-card row; an "Alerts & …" panel with "View all"; a chart card with a range switch. **Theme (resolved, round 3):** the default theme is the reference's **light sider** with the green brand colour exposed as `--erp-color-primary`; the spec's navy sider ships as the **"Classic"** preset, enabled per workspace with `<html data-nav-theme="dark">` (independent of light/dark mode). The header height is `--erp-header-height`: **56 px comfortable, 48 px compact** (follows `data-density`; closes Q11). Tokens are written `--erp-<name>` and mapped in [`../uiux-advanced/token-mapping.md`](../uiux-advanced/token-mapping.md); this pack uses names only (coordination-notes §A).

## 1. Shell layout (≥ lg)

```
┌──────────────┬──────────────────────────────────────────────────────────────────────────┐
│ ◆ Product    │ [☰] [Workspace: Acme Group ⇅▾] [🔍 Search…  Ctrl K] [Legal entity: All ▾] [Business unit: All ▾] [🔔3] [?] [☾] (JA) │ header (--erp-header-height: 56 / 48 compact)
│ ACME GROUP   ├──────────────────────────────────────────────────────────────────────────┤
│──────────────│ Breadcrumb: Finance / Journals / JV-2026-000012                          │
│ WORKSPACE    │                                                                          │
│ ▣ Overview   │                         <router-outlet> page                             │
│ ☑ Approvals 3│                                                                          │
│ FINANCE    ▾ │                                                                          │
│   Chart of…  │                                                                          │
│   Journals ● │                                                                          │
│   Invoices…  │                                                                          │
│ <MODULE>   ▸ │                                                                          │
│ ADMIN      ▸ │                                                                          │
│──────────────│                                                                          │
│ « Collapse   │                                                                          │
└──────────────┴──────────────────────────────────────────────────────────────────────────┘
```

ng-zorro structure: `nz-layout` > `nz-sider [nzCollapsible] [nzBreakpoint]="'lg'" [nzCollapsedWidth]` > `ul nz-menu nzMode="inline" nzTheme="light"` (default; the navy Classic look comes from `data-nav-theme="dark"` on `<html>`, styled by the theme's T15 `nz-menu` patch) (group = `nz-menu-group` or `li nz-submenu`); `nz-layout` > `nz-header` + `nz-content` (`nz-breadcrumb` + `router-outlet`).

## 2. Header elements

| Element | Control | Behaviour | Permission / scope |
|---|---|---|---|
| Sider toggle ☰ | icon button (`aria-expanded`, `aria-controls="app-sider"`) | ≥ lg: collapses the sider to icons. < lg: opens the sider as an `nz-drawer nzPlacement="left"`. | — |
| Workspace identity | Logo + workspace display name (small, letter-spaced caps per spec §3.3) at the sider top | Click goes to `/`. Identity only; switching lives in the header (next row). | — |
| **Workspace switcher (v1, QA-4)** | Header control, first after ☰: `button nz-dropdown` showing the current workspace name (truncated at ~24 chars, full name in tooltip) + ⇅▾ | **Shipped in v1 in the header**, not only in the user menu. The dropdown lists the user's workspaces (current one ticked, each with its Workspace ID muted), pending invitations with a badge, "See all workspaces…" (opens the picker modal, 01 §B) and "Create a new workspace". Picking one shows the "Switching workspace" state (§4). With only one workspace and no invitations it renders as a static label (no chevron) so the header layout stays stable. < md: it collapses into the user menu. `aria-label="Workspace: Acme Group. Switch workspace"`, `aria-haspopup="menu"`. | any |
| Global search | `nz-input` + `nz-autocomplete`, opened with `Ctrl/⌘ K` or `/` | Debounced 250 ms, min 2 chars → `GET /search?q=` (spec §12). Results are grouped by record type (spec §4.28: "grouped drop-down results"). Each group is provided by a module (`searchProviders`), and groups and items respect permissions and the legal-entity scope. Each item shows the record type icon, bold code, name and a muted context line. ↑/↓ moves through results, Enter opens the record, and "See all results for 'x'" goes to `/search?q=`. Recent searches are shown on focus when the query is empty. Empty result: "No matches for 'x'". Error: "Search is unavailable" + retry. < md: an icon button opens a full-screen search overlay (an improvement on the spec, which hides search entirely). | any |
| Legal-entity switcher | `nz-select` (width ~220, `nzShowSearch` when > 7) | Options: "All legal entities", then the entities the user may access (spec §4.30). Changing it rescopes every KPI, list and report. The value persists per user and is sent as `X-Entity-Id`. A confirmation toast reads "Showing Acme Trading LLC". If an open drawer has unsaved changes, a dirty-guard modal appears first. Hidden when the workspace has exactly 1 entity (shown as a static label instead). Pages that are not entity-scoped (Users, Settings) show the switcher disabled, with the tooltip "Not applicable on this page". | per-user allowed entities |
| Business-unit selector (context) | `nz-tree-select` (width ~200, searchable), placed next to the legal-entity switcher. Label: "Business unit: All" or the unit name. | **v1 = context selector, not access scoping** (coordinator decision). Options are "All business units" plus active units (hierarchy). If a legal entity is selected, units of other entities are shown greyed under "Other entities". The value persists per user and starts from their *default business unit* (04 §B). Effects: (1) it **pre-fills** the Business unit field on new records; (2) lists of resources that have a business-unit field get a removable filter chip "Business unit: X ×" (05 §D), and removing the chip doesn't change the header. Tooltip: "Pre-fills new records and filters lists. It doesn't limit what you can access." Hidden when the workspace has no business units. Sent as the `X-Business-Unit-Id` header (**proposed**) for defaults and filters only. | any |
| Notifications 🔔 | `nz-badge [nzCount]` on an icon button (`aria-label="Notifications, 3 unread"`) | Opens an `nz-drawer` (right, width sm) "Notifications" with tabs **All / Approvals / Alerts / Mentions** (spec §9 notification centre). Spec v1 behaviour (bell = count of approvals you can decide, tooltip "Approvals waiting for you", click → `/approvals`) is kept in the Approvals tab and its "View all approvals" link. Items show an icon, title, a muted context line, relative time, an unread dot and a chevron link to the record. Actions: **Mark all read** and ⚙ **Notification settings**. Real-time updates come over SignalR, and new critical items also raise an `nz-notification` toast. Empty: `nz-empty` "You're all caught up". | approvals tab needs `core.approvals.decide` |
| Help ? | `nz-dropdown`: Keyboard shortcuts (opens a modal listing them), Help centre, What's new | — | — |
| Theme ☾/☀ | icon button `aria-pressed`, tooltip "Dark mode" / "Light mode" (spec §3.5) | Toggles `data-theme` and persists per user. It also follows the system preference until the user sets one. | — |
| User menu (avatar) | `nz-avatar` (initial) + `nz-dropdown` | The header of the menu shows name, email and the **current workspace** name (spec §4.31). Items: My profile, Security (change password, 2FA), Preferences (language, date format, density, default business unit), Change password, **Switch workspace** (a secondary entry to the same picker as the header switcher; shown when the user has more than 1 workspace or pending invitations; required < md where the header switcher is hidden), **Create a new workspace** (→ onboarding, 01a §C), Sign out. Sign out calls `POST /auth/logout` and goes to `/auth/login`. | — |

## 3. Sider menu
* Groups render as section labels (`nz-menu-group`, spec §3.3) when expanded at ≥ xl. They render as collapsible `nz-submenu` with the reference-image style (▾/▸) when a group has more than 6 items, or when the user prefers collapsed groups. **Assumption:** groups are collapsible and the open state persists per user.
* The active item uses `--erp-color-nav-item-active-bg` / `--erp-color-nav-text-active`. A count badge (`nz-badge` small) is optional per item (e.g. Approvals pending).
* Collapsed (icon-only) mode: group labels hide, items show tooltips (`nzTooltipTitle`), and submenus open as popovers.
* Items are filtered by permission and by enabled modules (see 08 §4). A group with no visible items is hidden.
* "Pinned" (**assumption**, optional): users can star up to 8 items into a **Favourites** group at the top.

## 4. Breadcrumb
* Driven by route `data.breadcrumb` (string or resolver). Pattern: *Group label / Resource list / Record code*. Example: "Finance / Journals / JV-2026-000012" (spec §3.3: "Invoices / INV-2026-000028").
* Each segment except the last is a link. The last one has `aria-current="page"`.
* Shown on detail, nested and admin pages. List pages may hide it (spec shows it on detail pages). **Assumption:** always rendered for consistency, but visually muted.

## 5. Page title and document title
`document.title` = "<Record or page> · <Workspace> · <Product>". It updates on every navigation, and the new title is announced by the router focus handler (10 §2).

## 6. Shell states
| State | Display |
|---|---|
| App boot / silent refresh | Full-screen centred `nz-spin` with the logo. No blank page (spec §8 #1). |
| Lazy module loading | Top progress bar (2 px, `--erp-color-primary`) plus a content skeleton |
| Module chunk failed to load | `nz-result warning` "This section couldn't load. Check your connection." + **Reload** |
| Offline | Sticky `nz-alert warning` banner under the header |
| Session about to expire (refresh failed) | `nz-modal` "Your session has ended" + **Sign in again** (preserves the return URL) |
| Switching workspace | Full-screen `nz-spin` "Opening Beta Trading LLC…". Shell state (entity, business unit, caches, SignalR connection) resets, then the user lands on `/`. |
| Impersonation / support access (future) | Coloured banner (`--erp-color-warning-bg` / `--erp-color-warning-border`) "You're viewing as …" + **Exit** |
| Setup incomplete | The guard redirects to `/onboarding` (03) |
