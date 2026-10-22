# App shell — module-driven sidebar, header, command palette, breadcrumbs

> **Status: the module manifest in §2 is the accepted standard** (coordinator decision, 22 Oct 2026). It supersedes the draft `ErpModuleManifest` in `uiux/08-navigation-ia.md` §5. The good ideas from that draft (`apiBase`, `plans`, `countryPacks`, `roleTemplates`, `approvalDocumentTypes`, `reports`, `extensions`, `onboarding`) were merged in. The property renames are listed in `token-mapping.md` §4.

Owner: Advanced UI/UX · Aligns with `uiux/07-app-shell.md` (layout, header behaviour, states — authoritative for screen layout) and `uiux/00-shared-conventions.md` (vocabulary: **workspace** = SaaS customer, **legal entity**, **business unit**; "tenant" never appears in core UI). This file adds the **module manifest contract**, the component tree, interfaces, sizing tokens and responsive rules.

> Nothing in the shell knows a business type. Every menu item, KPI, widget, alert type, search group, command and term comes from an enabled module's manifest. Real estate and retail below are **worked examples only**.

---

## 1. Layout & sizing

```
┌──────────── nav (248 / 64) ─┬────────── header (56 / 48 compact) ─────────────────────────────────────────┐
│ [logo] Workspace name       │ [☰] [⌕ Search or jump to…  Ctrl K]   [Legal entity ▾] [🔔3] [?] [☾] (MJ)│
│ ─────────────────────────── ├──────────────────────────────────────────────────────────────────────┤
│ ★ FAVOURITES (opt.)         │ Breadcrumb  Group / List / Record                                     │
│ WORKSPACE                   │ Page header (title · status · context · actions)                      │
│  ▣ Overview                 │                                                                       │
│  ☑ Approvals            3   │ <router-outlet>                                                       │
│ FINANCE ▾                   │                                                                       │
│  ▸ Journals  (active pill)  │                                                                       │
│ <MODULE GROUPS…>            │                                                                       │
│ ADMINISTRATION ▸            │                                                                       │
│ ─────────────────────────── │                                                                       │
│ « Collapse                  │                                                                       │
└─────────────────────────────┴──────────────────────────────────────────────────────────────────────┘
```

| Token | Value | Notes |
|---|---|---|
| `--erp-nav-width` | 248 px | spec sidebar was 124–155 px (cramped labels); 248 fits 28-char labels + badge |
| `--erp-nav-width-collapsed` | 64 px | icon rail, 40 px items + 12 px padding |
| `--erp-header-height` | **56 px comfortable / 48 px compact** (confirmed ≥ 48 px) | spec ~32 px was too tight for 24–40 px targets and focus rings. 56 fits 40 px controls + the Ctrl K hint. Compact keeps 32 px controls at 48 px. Answers uiux T11 / Q11. |
| Nav item | height `--erp-nav-item-height` (40 / 34 compact), radius `control`, inline padding 12, icon 18 px, gap 12 | active = `nav-item-active-bg` pill + `nav-text-active` + weight 600 (reference style) |
| Section label | `overline` type, `nav-section-label`, uppercase (Latin) | collapsible groups show ▾/▸ |
| Header | `bg-surface`, bottom border `border-subtle`; gets `shadow-1` once content scrolls (IntersectionObserver sentinel) | |

### 1.1 Responsive behaviour

| Breakpoint | Sidebar | Header | Content |
|---|---|---|---|
| **xxl ≥ 1600** | expanded (248) | full search field (360 px) | max-width 1600 centred, page padding 24 |
| **xl 1200–1599** | expanded; user may collapse (persisted) | search 280 px | padding 24 |
| **lg 992–1199** | **collapsed rail by default** (64), expand overlays content (`shadow-3`) not push | search 220 px | padding 20 |
| **md 768–991** | hidden; ☰ opens `nz-drawer` (left in LTR / right in RTL, width 280) | search collapses to icon → opens command palette | padding 16 |
| **sm/xs < 768** | drawer (full-height, width min(320px, 86vw)) | logo mark, search icon, entity switcher moves into the drawer top, bell, avatar | padding 12, KPI 2-per-row (spec §3.6) |

Spec §3.6/4.32 hid search at narrow widths; we keep it as an icon that opens the full-screen command palette (improvement, aligned with uiux/07).

---

## 2. Module manifest contract

Every module (core included) ships a manifest. The platform loads manifests for **enabled** modules (`GET /platform/modules?enabled=true` → keys; the front-end code for each module is a lazy chunk that exports its manifest). Field names align with uiux/03, 04, 05 and custom-fields (`industries[]`, `permissions[]`, `settingsSections[]`, `searchProviders`, `customizable`, `layoutLockedFields`).

```ts
// libs/platform/module-manifest.ts
import type { Routes } from '@angular/router';
import type { Type } from '@angular/core';

/** i18n string: key into the module's translation bundle, with an English fallback. */
export interface I18nText { key: string; default: string }

/** Status tones = also the custom-field option palette (tokens --erp-tone-<tone>-bg/fg/border/dot). */
export type Tone = 'success' | 'neutral' | 'warning' | 'error' | 'info' | 'orange' | 'plain';

/** Permission requirement: user needs ANY of anyOf AND ALL of allOf.
 *  Keys are `<module>.<resource>.<action>`, ALL LOWER CASE, no separators inside a segment (approved Q4 / QA-6):
 *  module = 'core' | 'finance' | 'hr' | a module key ('realestate', 'retail'); resource = plural ('leases', 'customfields');
 *  action = read | create | edit | delete | export | approve | manage | <workflow verb> ('activate', 'deposit').
 *  e.g. 'finance.journals.read', 'realestate.leases.activate', 'core.customfields.manage'. */
export interface PermissionRequirement { anyOf?: string[]; allOf?: string[] }

export interface ModuleManifest {
  /** Stable key, lower case, no separators: 'core', 'finance', 'realestate', 'retail'. It is the permission module,
   *  the i18n/resource/id prefix and the API segment (/api/v1/m/<key>). */
  key: string;
  version: string;                       // semver; manifests are cached by key@version
  label: I18nText;
  description?: I18nText;
  isCore?: boolean;                      // cannot be disabled
  requires?: string[];                   // other module keys
  /** Industry descriptors surfaced by onboarding (uiux/03). Empty for horizontal modules. */
  industries?: { key: string; label: I18nText; icon: string }[];
  /** Decorative identity colour (#RRGGBB). Contrast-enforced via TenantThemeService.applyModuleAccent. */
  accent?: string;
  icon: string;                          // ng-zorro icon name (outline theme), e.g. 'home', 'shop'
  /** URL prefix, kebab-case; default = key. 'realestate' → 'real-estate' (QA-9). Must not be a reserved word
   *  (admin, auth, finance, parties, catalog, hr, reports, approvals, search, onboarding, print, m). */
  routePrefix?: string;
  /** Module API base; default `/api/v1/m/${key}` (approved D3). Core modules use `/api/v1`. */
  apiBase?: string;
  plans?: string[];                      // plan gating
  countryPacks?: string[];               // e.g. ['AE'] when the module needs a country pack

  routes: Routes;                        // mounted under /<routePrefix ?? key> (lazy: loadChildren), canMatch moduleEnabled(key)
  nav: NavContribution[];
  permissions: PermissionArea[];
  dashboard?: DashboardContribution;     // widgets + KPIs (specs/dashboard.md)
  alerts?: AlertTypeRegistration[];      // alert kinds the module raises (server builds them; UI renders)
  terminology?: TerminologyContribution;
  statusTones?: Record<string, Tone>;    // 'lease.active' → 'success' (status → tone only, never hex)
  searchProviders?: SearchProviderRegistration[];
  commands?: CommandRegistration[];      // command palette actions
  settingsSections?: SettingsSectionRegistration[];
  /** Record types (resourceKey, customizable, layoutLockedFields, numbering prefix). */
  resources?: ResourceRegistration[];
  roleTemplates?: { key: string; label: I18nText; permissions: string[] }[];
  approvalDocumentTypes?: { key: string; label: I18nText; resource: string }[];
  reports?: { slug: string; group: I18nText; label: I18nText; requires: PermissionRequirement }[];
  /** Add columns / KPIs / detail tabs / row actions / form sections to OTHER modules' resources (uiux/06 §8 extension points). */
  extensions?: { target: string; columns?: unknown[]; kpis?: string[]; rowActions?: unknown[];
                 detailTabs?: { id: string; label: I18nText; route: string; requires?: PermissionRequirement }[]; formSections?: unknown[] }[];
  onboarding?: { seedSteps?: string[] };
}

// ---------------------------------------------------------------- navigation
export interface NavContribution {
  /** Existing core group to join ('workspace', 'parties', 'catalog', 'finance', 'reports', 'admin' — uiux/08 §4;
   *  there are no Sales/Purchases groups: invoices, receipts, bills and payments live in Finance under /finance/...) or a new group this module defines (order 100–899). Max 3 items per module in a core group. */
  group: string;
  groupDef?: { label: I18nText; icon: string; order: number };   // only when creating a new group
  items: NavItem[];
}
export interface NavItem {
  id: string;                            // 'realestate.leases'
  label: I18nText;                       // may reference a term: { key: 'term.contract.plural', default: 'Leases' }
  icon: string;
  route: string;                         // '/real-estate/leases'
  order: number;                         // within the group; core uses multiples of 100
  requires?: PermissionRequirement;
  featureFlag?: string;
  /** Live count badge; resolved by BadgeRegistry (signal) – e.g. 'approvals.pending'. */
  badge?: { source: string; tone?: 'error' | 'info' | 'neutral'; ariaLabel: I18nText };
  children?: NavItem[];                  // max depth 2 (group → item → child)
  entityScoped?: boolean;                // true → hidden when the user has no allowed legal entity
  keywords?: string[];                   // extra command-palette matches
}

// ---------------------------------------------------------------- permissions
/** One row of the permission matrix (uiux/04 §D). Full key = `${module}.${resource}.${action}`. */
export interface PermissionArea {
  module: string;                        // 'realestate', 'finance', 'core' (lower case)
  resource: string;                      // plural, lower case, no separators: 'leases', 'customfields', 'tillsessions'
  label: I18nText;
  actions: string[];                     // 'read' | 'create' | 'edit' | 'delete' | 'export' | 'approve' | 'manage' | custom ('activate')
  defaultRoles?: Record<string, string[]>;   // role template key → actions
}

// ---------------------------------------------------------------- terminology / i18n
/**
 * Core screens use neutral term keys ('term.party.customer'). A module may
 * (a) relabel a core term *inside its own screens* (scope 'module'), or
 * (b) propose a workspace-wide relabel applied by its industry template (scope 'workspace').
 * Workspace admins can override any term (Settings → Terminology) — highest priority.
 */
export interface TerminologyContribution {
  terms: Record<string, TermForms>;      // key → forms
  scope: 'module' | 'workspace';
}
export interface TermForms {
  one: I18nText;                         // 'Tenant'
  other: I18nText;                       // 'Tenants'
  /** Optional per-language ICU or grammatical forms; ar needs dual + gender, ur needs oblique. */
  icu?: Record<string, string>;          // { ar: '{count, plural, one {مستأجر} two {مستأجران} few {...} other {مستأجرين}}' }
}

// ---------------------------------------------------------------- search & commands
export interface SearchProviderRegistration {
  id: string;                            // 'realestate.units'
  group: I18nText;                       // result group heading
  icon: string;
  requires: PermissionRequirement;
  endpoint: string;                      // server does the search; UI only renders groups
  order: number;
}
export interface CommandRegistration {
  id: string;                            // 'retail.new-sale'
  label: I18nText;
  icon?: string;
  shortcut?: string;                     // 'g s' (sequence) or 'mod+shift+s'
  requires?: PermissionRequirement;
  run: { route: string; queryParams?: Record<string, string> } | { action: string };
  section: 'create' | 'navigate' | 'action';
}

export interface SettingsSectionRegistration { id: string; label: I18nText; route: string; requires: PermissionRequirement; order: number }
export interface ResourceRegistration { key: string /* 'realestate.unit' (singular, lower case, Q12) */; label: I18nText; customizable: boolean; layoutLockedFields?: string[]; numberingPrefix?: string }
export interface AlertTypeRegistration {
  type: string;                          // 'realestate.contractexpiring'
  category: I18nText;                    // tag on the alert row ("Leasing")
  icon: string;
  defaultSeverity: 'critical' | 'high' | 'normal' | 'info';
  requires: PermissionRequirement;
  link: string;                          // route template '/real-estate/leases/:id'
}
// DashboardContribution: see specs/dashboard.md §2
export interface DashboardContribution { widgets: unknown[]; kpis: unknown[] }
```

### 2.1 Resolution rules

* **Merge**: core groups and orders follow `uiux/08-navigation-ia.md` §4: `workspace` 10, `parties` 20, `catalog` 25, `finance` 50 (Invoices, Receipts, Bills, Payments, Accounts, Bank & cash, Journals, Cheques — all under `/finance/...`), optional HR module 600, `reports` 890, `admin` 900. There are **no Sales or Purchases groups** (QA-7). Modules define groups in 100–899 or inject up to 3 items into a core group. Duplicate `id` → last enabled module loses (logged, shown in admin "Module health").
* **Gating** (evaluated in a `computed()` from the session signals): `module enabled` ∧ `featureFlag on` ∧ `PermissionRequirement satisfied` ∧ (`!entityScoped` ∨ user has ≥1 allowed entity). Items failing are **removed**, not disabled (uiux/00 §4). Empty groups are removed. Route guards repeat the same check (`canMatch`) — UI hiding is convenience, the API is the authority.
* **Terminology priority**: workspace admin override > industry-template workspace terms > module-scope terms (only within that module's routes, via the route's `data.module`) > core default. Implemented by `TermService.resolve(key, count?)` and the pure pipe `{{ 'term.party.customer' | term: count }}`; module screens set `providers: [provideModuleTerms('real-estate')]` at route level.
* **Badges**: `BadgeRegistry.register('approvals.pending', signal<number>)`; SignalR `approvalCreated/approvalDecided` updates it; the nav item reads it.
* **Accent**: the module's routes render inside `<erp-module-outlet>` which calls `applyModuleAccent(host, manifest.accent)`; accent is used only for the page-header icon tile, KPI bars, chart series 1 when the module wants it, and the tiny group icon in the sidebar — never text, never primary buttons.

### 2.2 Example A — Real estate module (EXAMPLE ONLY)

```ts
export const REAL_ESTATE_MANIFEST: ModuleManifest = {
  key: 'realestate', routePrefix: 'real-estate', version: '1.0.0', icon: 'home', accent: '#BFA66A',
  apiBase: '/api/v1/m/realestate',
  label: { key: 'realestate.module.label', default: 'Real estate' },
  requires: ['finance'],
  countryPacks: ['AE'],
  industries: [{ key: 'property-management', label: { key: 'realestate.industry.label', default: 'Property management' }, icon: 'home' }],
  routes: [{ path: '', loadChildren: () => import('./re.routes').then(m => m.RE_ROUTES) }],
  nav: [
    { group: 'realestate.portfolio', groupDef: { label: { key: 'realestate.nav.portfolio', default: 'Property & asset' }, icon: 'home', order: 150 },
      items: [
        { id: 'realestate.properties', label: { key: 'realestate.property.plural', default: 'Properties' }, icon: 'bank', route: '/real-estate/properties', order: 100, requires: { anyOf: ['realestate.properties.read'] }, entityScoped: true },
        { id: 'realestate.units', label: { key: 'realestate.unit.plural', default: 'Units' }, icon: 'appstore', route: '/real-estate/units', order: 200, requires: { anyOf: ['realestate.units.read'] }, entityScoped: true },
      ] },
    { group: 'realestate.leasing', groupDef: { label: { key: 'realestate.nav.leasing', default: 'Leasing' }, icon: 'file-text', order: 160 },
      items: [
        { id: 'realestate.leases', label: { key: 'term.contract.other', default: 'Leases' }, icon: 'file-text', route: '/real-estate/leases', order: 100, requires: { anyOf: ['realestate.leases.read'] } },
      ] },
  ],
  permissions: [
    { module: 'realestate', resource: 'properties', label: { key: 'realestate.perm.properties', default: 'Properties' }, actions: ['read', 'create', 'edit', 'delete', 'export'] },
    { module: 'realestate', resource: 'leases', label: { key: 'realestate.perm.leases', default: 'Leases' }, actions: ['read', 'create', 'edit', 'export', 'activate', 'renew', 'terminate'] },
  ],
  terminology: { scope: 'workspace', terms: {
    'term.party.customer': { one: { key: 'realestate.term.tenant', default: 'Tenant' }, other: { key: 'realestate.term.tenants', default: 'Tenants' } },
    'term.contract':       { one: { key: 'realestate.term.lease', default: 'Lease' },   other: { key: 'realestate.term.leases', default: 'Leases' } },
  } },
  statusTones: { 'lease.active': 'success', 'lease.draft': 'warning', 'lease.ended': 'error', 'unit.occupied': 'success', 'unit.vacant': 'plain' },
  alerts: [
    { type: 'realestate.contractexpiring', category: { key: 'realestate.alert.leasing', default: 'Leasing' }, icon: 'clock-circle', defaultSeverity: 'high', requires: { anyOf: ['realestate.leases.read'] }, link: '/real-estate/leases/:id' },
  ],
  // Cheques are a core Finance instrument (D1): the module only links them to leases.
  extensions: [{ target: 'core.cheque.list', columns: [{ key: 'realestate.lease', label: 'Lease' }] }],
  searchProviders: [{ id: 'realestate.units', group: { key: 'realestate.unit.plural', default: 'Units' }, icon: 'appstore', requires: { anyOf: ['realestate.units.read'] }, endpoint: 'search/units' /* relative to apiBase */, order: 300 }],
  commands: [{ id: 'realestate.newlease', label: { key: 'realestate.cmd.newLease', default: 'New lease' }, icon: 'plus', section: 'create', requires: { anyOf: ['realestate.leases.create'] }, run: { route: '/real-estate/leases/new' } }],
  resources: [{ key: 'realestate.unit', label: { key: 'realestate.unit.singular', default: 'Unit' }, customizable: true, layoutLockedFields: ['code', 'property'], numberingPrefix: 'UNT' }],
  reports: [{ slug: 'rent-roll', group: { key: 'realestate.reports', default: 'Real estate' }, label: { key: 'realestate.report.rentRoll', default: 'Rent roll' }, requires: { anyOf: ['realestate.reports.read'] } }],
  dashboard: { widgets: [/* specs/dashboard.md §7 */], kpis: [] },
};
```

### 2.3 Example B — Retail module (EXAMPLE ONLY, proves generality)

```ts
export const RETAIL_MANIFEST: ModuleManifest = {
  key: 'retail', version: '1.0.0', icon: 'shop', accent: '#8E5BD9',
  apiBase: '/api/v1/m/retail',
  label: { key: 'retail.module.label', default: 'Retail' },
  requires: ['finance'],
  industries: [{ key: 'retail', label: { key: 'retail.industry.label', default: 'Retail & stores' }, icon: 'shop' }],
  routes: [{ path: '', loadChildren: () => import('./retail.routes').then(m => m.RETAIL_ROUTES) }],
  nav: [
    { group: 'retail.stores', groupDef: { label: { key: 'retail.nav.stores', default: 'Stores & POS' }, icon: 'shop', order: 140 }, items: [
      { id: 'retail.pos', label: { key: 'retail.pos.label', default: 'Point of sale' }, icon: 'shopping-cart', route: '/retail/pos', order: 50, requires: { anyOf: ['retail.tillsessions.create'] } },
      { id: 'retail.stores', label: { key: 'retail.store.plural', default: 'Stores' }, icon: 'shop', route: '/retail/stores', order: 60, requires: { anyOf: ['retail.stores.read'] }, entityScoped: true },
      { id: 'retail.promotions', label: { key: 'retail.promotion.plural', default: 'Promotions' }, icon: 'tags', route: '/retail/promotions', order: 70, requires: { anyOf: ['retail.promotions.read'] } },
    ] },
    { group: 'catalog', items: [
      { id: 'retail.replenishment', label: { key: 'retail.replenishment.label', default: 'Replenishment' }, icon: 'reload', route: '/retail/replenishment', order: 50, requires: { anyOf: ['core.items.read'] },
        badge: { source: 'retail.lowstock', tone: 'error', ariaLabel: { key: 'retail.lowStock.badge', default: 'items below reorder point' } } },
    ] },
  ],
  permissions: [
    { module: 'retail', resource: 'tillsessions', label: { key: 'retail.perm.pos', default: 'Point of sale' }, actions: ['read', 'create', 'approve'] },
    { module: 'retail', resource: 'stores', label: { key: 'retail.perm.stores', default: 'Stores' }, actions: ['read', 'create', 'edit'] },
    { module: 'retail', resource: 'promotions', label: { key: 'retail.perm.promotions', default: 'Promotions' }, actions: ['read', 'create', 'edit', 'approve'] },
  ],
  terminology: { scope: 'module', terms: {
    'term.party.customer': { one: { key: 'retail.term.shopper', default: 'Shopper' }, other: { key: 'retail.term.shoppers', default: 'Shoppers' } },
  } },
  statusTones: { 'order.open': 'neutral', 'order.paid': 'success', 'order.refunded': 'orange', 'stock.low': 'error' },
  alerts: [{ type: 'retail.lowstock', category: { key: 'retail.alert.stock', default: 'Stock' }, icon: 'inbox', defaultSeverity: 'high', requires: { anyOf: ['core.items.read'] }, link: '/catalog/items/:id' }],
  commands: [{ id: 'retail.newsale', label: { key: 'retail.cmd.newSale', default: 'New sale' }, icon: 'plus', shortcut: 'mod+shift+s', section: 'create', requires: { anyOf: ['retail.tillsessions.create'] }, run: { route: '/retail/pos' } }],
  resources: [{ key: 'retail.store', label: { key: 'retail.store.singular', default: 'Store' }, customizable: true }],
  extensions: [{ target: 'core.item.detail', detailTabs: [{ id: 'retail.storestock', label: { key: 'retail.tab.storeStock', default: 'Stock by store' }, route: 'store-stock' }] }],
  dashboard: { widgets: [/* specs/dashboard.md §8 */], kpis: [] },
};
```

Same shell, same components; only manifests differ. A workspace with both enabled sees both sets. Retail's terms are `scope: 'module'`, so "Shopper" shows only on retail screens. Real estate's are `scope: 'workspace'` and apply workspace-wide only when real estate is the *primary industry* chosen at onboarding. Key formats follow uiux/08 §2:
* The module key is `realestate`: lower case, no separators, no camelCase twin. It is also the permission module, the id/i18n/resource prefix, and the API segment (`/api/v1/m/realestate/`).
* Routes use `routePrefix` (`/real-estate/`).
* Permissions are all lower case, e.g. `realestate.leases.read` and `realestate.leases.activate`.
* Resource keys are singular, e.g. `realestate.unit`.

---

## 3. Angular component tree

```
AppComponent (standalone)                       <div [dir]="theme.dir()">  (CDK BidiModule)
└─ AppShellComponent                            nz-layout; reads ShellStore signals
   ├─ SidebarNavComponent  (nz-sider)           [collapsed]=shell.collapsed()  — ≥ lg
   │   ├─ WorkspaceBrandComponent               logo (theme.logoUrl()) / mark when collapsed
   │   ├─ NavFavouritesComponent  (optional)
   │   ├─ NavGroupComponent *for group          nz-menu-group | li nz-submenu
   │   │   └─ NavItemComponent *for item        li nz-menu-item [routerLink] [nzTooltipTitle when collapsed] + nz-badge
   │   └─ NavCollapseToggleComponent
   ├─ MobileNavDrawerComponent  (nz-drawer)     < lg; reuses NavGroupComponent tree; contains EntitySwitcher on < md
   └─ nz-layout
      ├─ HeaderComponent  (nz-header)
      │   ├─ NavToggleButtonComponent           ☰ aria-expanded / aria-controls
      │   ├─ SearchTriggerComponent             looks like an input; opens CommandPaletteComponent
      │   ├─ EntitySwitcherComponent            nz-select (uiux/07 §2)
      │   ├─ BusinessUnitFilterComponent        optional
      │   ├─ NotificationBellComponent          nz-badge → NotificationCenterComponent (nz-drawer)
      │   ├─ HelpMenuComponent
      │   ├─ ThemeToggleComponent               aria-pressed
      │   └─ UserMenuComponent                  nz-avatar + nz-dropdown
      ├─ ShellBannersComponent                  offline / impersonation / maintenance (aria-live)
      ├─ nz-content
      │   ├─ BreadcrumbComponent                route data → nz-breadcrumb
      │   └─ <router-outlet>  → ModuleOutletComponent (accent + module terms) → feature pages
      ├─ RouteProgressBarComponent              2 px, primary
      └─ RouteAnnouncerComponent                visually hidden aria-live="polite" (page title on navigation)
CommandPaletteComponent  (CDK Overlay, global)  Ctrl/⌘+K, "/" ; z-index palette
ToastHostComponent                              NzMessage/NzNotification config (top-end)
```

### 3.1 Key services & state

```ts
export interface ShellState {
  collapsed: boolean;                 // persisted per user ('erp.shell.collapsed')
  mobileNavOpen: boolean;
  openGroups: string[];               // persisted
  favourites: string[];               // nav item ids (max 8)
}

@Injectable({ providedIn: 'root' })
export class ModuleRegistry {
  readonly manifests = signal<ModuleManifest[]>([]);          // enabled only
  readonly navTree = computed<NavGroupVm[]>(() => buildNav(this.manifests(), this.session.permissions(), this.flags.enabled(), this.session.allowedEntities()));
  readonly commands = computed(() => this.manifests().flatMap(m => m.commands ?? []).filter(c => this.session.can(c.requires)));
  readonly alertTypes = computed(() => new Map(this.manifests().flatMap(m => m.alerts ?? []).map(a => [a.type, a])));
  readonly widgets = computed(() => this.manifests().flatMap(m => m.dashboard?.widgets ?? []));
  constructor(private session: SessionStore, private flags: FeatureFlagStore) {}
}

export interface NavGroupVm { key: string; label: string; icon: string; order: number; items: NavItemVm[] }
export interface NavItemVm  { id: string; label: string; icon: string; route: string; badge?: Signal<number>; badgeAria?: string; children?: NavItemVm[] }

export interface SessionStore {      // @ngrx/signals SignalStore (spec §10.2)
  user: Signal<{ id: string; name: string; email: string; initials: string }>;
  permissions: Signal<ReadonlySet<string>>;
  allowedEntities: Signal<{ id: string; name: string; code: string }[]>;
  currentEntityId: Signal<string | 'all'>;   // sent as X-Entity-Id (spec §4.30)
  can(req?: PermissionRequirement): boolean;
}
```

---

## 4. Sidebar behaviour

| Aspect | Spec |
|---|---|
| Expanded | group labels + items; groups with > 6 items or user preference become collapsible submenus (uiux/07 §3); open state persisted |
| Collapsed rail (64 px) | icons only, centred; group labels hidden and replaced by 1 px dividers; each item has `nz-tooltip` (placement end, 300 ms delay) with label + badge count; submenus open as `nz-menu` popovers (`nzMode="vertical"`) on hover/click/Enter; badge becomes a dot with count in tooltip and `aria-label` |
| Toggle | « button at the bottom and `Ctrl+B` (aria-keyshortcuts); width animates `--erp-duration-moderate` / `--erp-ease-standard`; labels fade out in the first 100 ms (no text squish) |
| Active state | longest-prefix route match (`/finance/journals/123` activates "Journals"); parent group auto-opens; `aria-current="page"` on the link |
| Mobile drawer | `nz-drawer` from inline-start, `nzWidth` per §1.1, mask click / Esc closes, focus trapped, returns focus to ☰; closes on navigation |
| Scroll | nav scrolls independently (spec §3.3); active item `scrollIntoView({ block: 'nearest' })` on load |
| Empty | if only core is enabled the nav shows Workspace + Admin; a nav with zero permitted items (viewer of nothing) shows "You don't have access to any area yet — ask your workspace admin." |
| Loading | 8 skeleton rows (icon circle + bar) while manifests resolve (< 300 ms typical; skeleton only after 150 ms to avoid flash) |
| Module chunk fails | item stays; route shows uiux/07 §6 "This section couldn't load" result |

---

## 5. Header

Behaviour per element is defined in `uiux/07-app-shell.md` §2; visual & interaction additions:

* **Entity switcher**: `nz-select` 220 px, prefix icon `apartment`, option rows = name + muted code; "All legal entities" first, divider, then entities. On change: optimistic rescope (all entity-scoped queries invalidated; KPI/list skeletons show), toast "Showing <Entity>" (`NzMessage`, 2 s). Keyboard: focus + type-ahead. Hidden when the workspace has one entity (static label).
* **Notifications**: bell `nz-badge [nzCount]` (max 99+), `aria-label="Notifications, 3 unread"`; drawer 400 px, tabs All / Approvals / Alerts / Mentions; items grouped "Today / Earlier"; new real-time item → bell does a 1-cycle 300 ms "ring" (rotate ±12°, disabled under reduced motion) and announces via `aria-live="polite"` "New notification: …" (critical = `assertive` + `nz-notification` toast).
* **Dark-mode toggle**: icon button (moon / sun), `aria-pressed`, tooltip "Dark mode"/"Light mode" (spec §3.5); click → `theme.toggleTheme()`; long-press/right-click or the avatar Preferences offers Light / Dark / System. The icon cross-fades + rotates 90° in `quick` (150 ms).
* **Avatar menu**: name, email, Preferences (Language, Density, Reduce motion, Digits), Change password, Sign out (uiux/07).

### 5.1 Command palette (Ctrl/⌘ + K, "/")

```
┌──────────────────────────────────────────────────────────┐
│ ⌕  Search records, pages and actions…            Esc     │
├──────────────────────────────────────────────────────────┤
│ RECENT                                                   │
│   ⟲ INV-2026-000028 · Invoice · Acme LLC                 │
│ CREATE                                                   │
│   + New invoice                              N I         │
│ GO TO                                                    │
│   → Journals  (Finance)                      G J         │
│ <MODULE GROUPS from searchProviders>                     │
│   ▣ CUS-0012 Mr. Daniel Okafor · Customer                │
├──────────────────────────────────────────────────────────┤
│ ↑↓ navigate · ↵ open · ⌘↵ open in new tab · Tab filter   │
└──────────────────────────────────────────────────────────┘
```

* CDK Overlay, centred, width min(640px, 92vw), top 12vh; `shadow-4`, radius `overlay`; mask `bg-overlay` at 40 %.
* Sources merged: nav items (fuzzy on label + keywords), `commands[]`, recent records (local, 10), server `searchProviders` (debounce 200 ms, ≥ 2 chars, cancel in-flight with `switchMap`); results grouped and permission-filtered; entity scope chip "in: Harbour LLC" removable.
* Prefix filters: `>` commands only, `/` pages only, `#` records only, `@` people.
* ARIA: `role="dialog" aria-modal="true" aria-label="Command palette"`; input `role="combobox" aria-expanded aria-controls="cp-list" aria-activedescendant`; list `role="listbox"` with `role="group"` + `aria-labelledby` per section; options `role="option"`; result count announced politely ("12 results").
* States: empty query → recents + suggested commands; no results → "No matches for 'x'" + "Search all records" action; error → inline "Search is unavailable" + retry (commands still work offline).

### 5.2 Global keyboard map (shell)

| Keys | Action |
|---|---|
| `Ctrl/⌘ K`, `/` (when not typing) | command palette |
| `Ctrl/⌘ B` | collapse / expand sidebar |
| `G` then `O` / `A` / `N` | go to Overview / Approvals / Notifications |
| `Shift + ?` | keyboard shortcuts dialog |
| `Alt + Shift + D` | toggle dark mode |
| `Esc` | close the top-most overlay (palette → drawer → modal) |
| `F6` / `Shift+F6` | cycle landmarks: nav → header → main |

Module `commands[].shortcut` are registered in the same `HotkeyService` (conflicts rejected at registration, reported in Module health). Single-key shortcuts are disabled while focus is in an editable field and can be turned off in Preferences (WCAG 2.1.4).

---

## 6. Breadcrumbs

* `BreadcrumbComponent` builds from the router tree: each route may set `data: { breadcrumb: I18nText | ((data) => string), group?: string }`; the first crumb is the **nav group label** (from the manifest), then list, then record code (resolved from the route resolver, e.g. `INV-2026-000028`).
* `nav aria-label="Breadcrumb"` > `ol`; separators are CSS (`/`, mirrored chevron in RTL via `--erp-chevron-rotate`), `aria-current="page"` on the last.
* Overflow: > 4 crumbs collapse the middle into `…` (`nz-dropdown`); < md shows only "‹ Parent" back link.
* Typography `caption`, colour `text-muted`, links `primary-text` on hover/focus underline.

---

## 7. Shell states

As `uiux/07-app-shell.md` §6, with tokens: boot spinner on `bg-canvas` + logo (brand vars inlined in `index.html` so the spinner is already branded); route progress bar 2 px `primary`, indeterminate, appears after 120 ms; offline banner `warning-bg`/`warning-text` + icon, sticky under header, `role="status"`.
