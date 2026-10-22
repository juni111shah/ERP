# Dashboard — a widget grid hosting module-registered widgets

Owner: Advanced UI/UX · Spec: §4.2 (Overview), §3.3 (KPI cards, hero), §3.6 (reflow), §9 (dashboard builder = Could, real-time KPIs = Must), §10.2 / §10.7 (ngx-echarts, SignalR `kpiInvalidated`, 60 s cache) · Visual reference: `dashboard-reference.png`.

> The Overview is **not** a fixed layout. Core renders a `WidgetGridComponent`; enabled modules register widgets and KPIs in their manifest (`dashboard` contribution). A default layout per *industry template* / role is shipped, and users may personalise. The real-estate and retail layouts below are **worked examples**.

---

## 1. Reference → anatomy

The reference (“Monitoring Overview”) shows, top to bottom: page title + scope select in the header; **a row of 4 KPI cards** (coloured inline-start accent bar, label, big coloured value, delta line "↑ 2.3% vs yesterday"); **a row of 3 status panels** (title, status dot + word "Operational/Normal/Warning", big value "42/45 Online", muted explanation); **an Alerts list** (severity icon, title, timestamp, small tag, count badge, "View all ↗") beside **a bar chart card** (title, segmented series switch Cash / Transactions / Failures, download icon, range chips Last 7 days / Last 30 days / …).

We keep that rhythm as four **widget kinds**: `kpi`, `status`, `list` (alerts/to-dos), `chart`, plus `hero` (spec §3.3) and `custom` (module component).

---

## 2. Registration contract

```ts
// libs/platform/dashboard.ts
import type { Type, Signal } from '@angular/core';
import type { I18nText, PermissionRequirement, Tone } from './module-manifest';

export type WidgetKind = 'kpi' | 'status' | 'list' | 'chart' | 'hero' | 'custom';
export type WidgetSize = 'xs' | 'sm' | 'md' | 'lg' | 'xl';   // grid spans, see §3

export interface DashboardContribution {
  kpis: KpiRegistration[];
  widgets: WidgetRegistration[];
  /** Default layouts this module proposes (used when it is the primary industry or for a role). */
  layouts?: DashboardLayoutPreset[];
}

export interface KpiRegistration {
  id: string;                               // 'finance.receivables'
  label: I18nText;                          // may use term keys
  /** Server metric key; values come from the batched dashboard query. */
  metric: string;
  format: 'money' | 'number' | 'percent' | 'count' | 'duration';
  /** Hint line under the value: static text or a template with metric fields: '{overdue|money} overdue'. */
  hint?: I18nText;
  delta?: { compareTo: 'previousPeriod' | 'previousDay' | 'target'; goodDirection: 'up' | 'down' | 'neutral' };
  accent?: 'primary' | 'info' | 'warning' | 'error' | 'success' | 'accent-gold' | 'module' | 'viz-1' | 'viz-2' | 'viz-3' | 'viz-4' | 'viz-5';
  /** Click-through (KPI renders as a link/button; uiux/00 §1). */
  link?: { route: string; queryParams?: Record<string, string> };
  requires: PermissionRequirement;
  entityScoped: boolean;                    // rescoped by the entity switcher
  refresh: { invalidateOn?: string[]; pollSeconds?: number };  // SignalR topics, e.g. ['posting.created']
}

export interface WidgetRegistration {
  id: string;                               // 'finance.billed-vs-collected'
  kind: WidgetKind;
  title: I18nText;
  description?: I18nText;                   // shown in the widget picker and as aria-description
  defaultSize: WidgetSize;
  allowedSizes: WidgetSize[];
  requires: PermissionRequirement;
  entityScoped: boolean;
  /** kind-specific config */
  status?: StatusWidgetConfig;
  list?: ListWidgetConfig;
  chart?: ChartWidgetConfig;
  kpiIds?: string[];                        // kind 'kpi' = a KPI strip of 1–6 registered KPIs
  component?: () => Promise<Type<unknown>>; // kind 'custom' (lazy); receives WidgetContext input
  refresh: { invalidateOn?: string[]; pollSeconds?: number };
}

export interface StatusWidgetConfig {
  metric: string;                           // returns { value, total?, state: 'ok'|'degraded'|'critical'|'unknown', note }
  stateLabels: Record<'ok' | 'degraded' | 'critical' | 'unknown', I18nText>;   // "Operational", "Warning"…
  valueTemplate: I18nText;                  // '{value}/{total} let'
}
export interface ListWidgetConfig {
  source: 'alerts' | 'approvals' | 'metric';
  metric?: string;
  alertTypes?: string[];                    // filter to these registered alert types (default: all permitted)
  maxItems: number;                         // 5–8 visible, rest via "View all"
  viewAllRoute: string;
}
export interface ChartWidgetConfig {
  type: 'bar' | 'grouped-bar' | 'stacked-bar' | 'horizontal-bar' | 'line' | 'area' | 'donut';
  metric: string;                           // returns { categories: string[], series: { key, label, data: number[] }[] }
  series: { key: string; label: I18nText; color?: 'viz-1' | 'viz-2' | 'viz-3' | 'viz-4' | 'viz-5' | 'viz-6' | 'viz-7' | 'viz-8' | 'module' }[];
  /** Segmented switch like the reference (Cash / Transactions / Failures): alternative metrics. */
  views?: { key: string; label: I18nText; metric: string }[];
  ranges?: ('7d' | '30d' | '90d' | '12m' | 'ytd' | 'custom')[];
  valueFormat: KpiRegistration['format'];
}

export interface DashboardLayoutPreset {
  id: string;                               // 'realestate.default'
  label: I18nText;
  forRoles?: string[];                      // built-in role keys
  items: DashboardLayoutItem[];
}
export interface DashboardLayoutItem { widgetId: string; size: WidgetSize; order: number }

/** Runtime context passed to every widget component. */
export interface WidgetContext {
  entityId: Signal<string | 'all'>;
  range: Signal<string>;
  data: Signal<WidgetData>;                 // from DashboardStore
  refresh(): void;
}
export type WidgetData =
  | { state: 'loading' }
  | { state: 'ready'; value: unknown; asOf: string; stale?: boolean }
  | { state: 'empty'; reason: 'no-data' | 'not-configured' }
  | { state: 'error'; message: string; retryable: boolean }
  | { state: 'forbidden' };
```

### 2.1 Data flow

* **One batched request per dashboard** (spec §4.2: "one GET returning all KPIs"): `POST /api/v1/dashboard/query { entityId, range, metrics: string[] }` → `{ [metric]: value | error }`. Each metric is computed by its owning module's server handler; per-metric errors don't fail the batch (a widget shows its own error state). Cached ~60 s per entity (spec §10.7).
* **Real-time**: SignalR `kpiInvalidated { metrics[] , entityId }` → `DashboardStore` refetches only those metrics (debounced 2 s); values update with the count-up micro-interaction; a stale indicator (`asOf`) shows if the refetch fails.
* **Entity switcher** rescopes `entityScoped` widgets; non-scoped widgets (e.g. Approvals) stay.
* **Layout resolution**: user personal layout > role preset > primary-industry preset > core default (`core.default`: Approvals list + Alerts list + whatever KPIs the enabled modules mark as `featured`). Unknown/forbidden widget ids in a saved layout are silently dropped (and reported in Module health).

---

## 3. Grid

12-column CSS grid, `gap: var(--erp-grid-gap)` (16 / 12 compact), page padding `--erp-page-padding`, `grid-auto-flow: row dense`, rows auto-height (no fixed heights except chart bodies).

| Size | ≥ xl (1200+) | lg (992–1199) | md (768–991) | sm (576–767) | xs (< 576) |
|---|---|---|---|---|---|
| `xs` (single KPI) | 3 cols (4 per row) | 3 (4/row) | 6 (2/row) | 6 (2/row, spec §3.6) | 12 |
| `sm` (status panel) | 4 (3/row) | 4 | 6 | 12 | 12 |
| `md` (list, small chart) | 4 | 6 | 12 | 12 | 12 |
| `lg` (chart) | 8 | 6 | 12 | 12 | 12 |
| `xl` (hero, KPI strip, wide table) | 12 | 12 | 12 | 12 | 12 |

* A **KPI strip** widget (`kind: 'kpi'`, size `xl`) lays out its 1–6 KPIs internally: `repeat(auto-fit, minmax(200px, 1fr))` → 6 / 4 / 3 / 2 / 1 per row by width; spec's "two rows of six" = two strips. Value text never truncates; long values shrink by one type step (`kpi` 24 → 20) via container query `@container (inline-size < 220px)`.
* ≥ xxl: grid max-width 1600 centred.
* Chart body heights: 280 px (lg/md size), 220 px on < md.
* Reflow order = layout `order`; on < md lists move above charts if the layout says `priority: 'high'` (alerts are actionable).

```html
<section class="erp-widget-grid" aria-label="Overview widgets">
  @for (item of layout(); track item.widgetId) {
    <erp-widget-host [registration]="reg(item.widgetId)" [size]="item.size"
                     [style.--_span-xl]="span(item.size,'xl')" … />
  }
</section>
```

---

## 4. Widget anatomy

### 4.1 KPI card (`KpiCardComponent`, inputs `kpi`, `value`, `accent`)

```
┌─▌───────────────────────────┐   ▌ accent bar 3 px, inline-start (reference) — top in spec §3.3;
│ ▌ LABEL                 ⓘ   │     we use inline-start (reference) and keep `accentPosition: 'top'` as an input
│ ▌ AED 155,363               │   value: `kpi` type, tabular, colour text-heading (NOT accent colour —
│ ▌ ↑ 4.2% vs last month      │     reference colours values, which fails contrast for pink/amber on white)
│ ▌ overdue 54,563            │   hint: caption, text-muted
└─────────────────────────────┘
```

| Part | Token | Rule |
|---|---|---|
| Container | `bg-surface`, `border-subtle`, radius `card`, `shadow-1`, padding `--erp-card-padding`, min-height 112 | whole card is a link when `link` set (`<a>` with visible focus ring, hover `shadow-2` + border `border-default`) |
| Accent bar | `--erp-kpi-accent-width` 3 px, `var(--erp-color-<accent>)`; `module` → `module-accent` | decorative |
| Label | `label`, `text-secondary`, 1 line, ellipsis + full text in tooltip/title | |
| Value | `kpi` (24/32, 700, tabular), `text-heading`; money via `MoneyPipe` with compact notation ≥ 1M ("AED 112.9M") and the exact value in tooltip + `aria-label` | negative → `money-negative` |
| Delta | `caption`; arrow icon + signed % + "vs …"; colour `trend-up`/`trend-down` according to `goodDirection` (a rise in Overdue is bad → red) ; neutral = `text-muted` | arrow + sign so colour isn't the only cue |
| Hint | `caption`, `text-muted` | from `hint` template |
| ⓘ | optional definition popover (formula from spec §4.2 table) | |

### 4.2 Status panel (`StatusPanelComponent`)

```
┌──────────────────────────────┐
│ Title                        │  title-sm, text-heading
│ ● Operational                │  8 px dot (status solid) + word (status text)
│ 42/45 Online                 │  kpi type
│ 3 machines need attention    │  caption, text-muted
└──────────────────────────────┘
```

State → tone: `ok` → success, `degraded` → warning, `critical` → error, `unknown` → neutral. Dot also gets a shape cue in forced-colors / for colour-blind users: ok = filled circle, degraded = triangle, critical = square (icon set `check-circle` / `warning` / `close-circle` at 12 px is used instead of a dot when the user enables "Show status shapes" — default **on** for critical).

### 4.3 Alerts & to-dos list (`AlertListComponent`)

Rows (spec §4.2 right column + reference): severity icon (critical `close-circle` error-solid, high `exclamation-circle` attention-solid, normal `info-circle` info-solid, info `bell` neutral-solid) · title (body, 2 lines max) · muted meta line (record code · relative time) · category tag (`nz-tag`, from `AlertTypeRegistration.category`) · urgency tag (Action needed = error, Upcoming = warning, For information = neutral) · count badge when grouped · chevron (mirrored in RTL). Whole row is one link (`<a>`), 56 px min height (44 compact). Header: title + count + "View all ↗" (`aria-label="View all alerts"`). Max 5–8 rows; scroll inside the card only on ≥ lg (spec: "scrollable"), else "View all".

### 4.4 Chart card (`ChartCardComponent`)

Header: title (`title-sm`) · optional `nz-segmented` view switch (reference: Cash / Transactions / Failures) · ⋮ menu (Download PNG, Download CSV, View as table) · range chips (`nz-segmented` or `nz-radio-group nzButtonStyle="solid"` small; reference: Last 7 / 30 / 90 days). Body: chart. Footer (optional): legend if > 1 series (also clickable to toggle series, keyboard operable), "as of" time.

**Library: ngx-echarts (Apache ECharts 5)** — already chosen by spec §4.2/§10.2. Reasons: canvas+SVG renderers (SVG for crisp print/PDF), built-in `aria` module (auto-generated chart descriptions + decal patterns for colour-blind users), RTL axis support, dataZoom for long series, tree-shakable (`echarts/core` + used charts only, ~150 KB gz), works with Angular 17+ standalone (`provideEchartsCore({ echarts })`), no licence cost. (Alternatives considered: ng2-charts/Chart.js — lighter but weaker a11y/large data; Highcharts — best a11y but commercial licence; AG Charts — good but licence for enterprise features.)

Chart theming: a `ChartThemeService` reads computed CSS vars (`viz-*`, `viz-grid`, `viz-axis`, `text-muted`, font family) on theme/brand/direction change and calls `setOption` (no re-mount). Defaults: bars `barMaxWidth: 28`, radius `[4,4,0,0]` (mirrored for horizontal/RTL), grid lines `viz-grid` dashed, axis labels `caption` `viz-axis`, tooltip = `bg-surface-raised` + `shadow-2` + tabular numbers, `aria: { enabled: true, decal: { show: userPrefs.patterns } }`, animation `duration-slower` 400 ms `cubicOut`, disabled under reduced motion. Each chart has a **"View as table"** toggle rendering the same data in an `nz-table` (accessibility spec §6).

### 4.5 Hero banner (optional, spec §3.3)

Rounded `radius-card`, gradient from `brand-900` to `brand-700` (tenant-derived, so it follows the brand; spec's navy→bronze is the "Classic navy" preset), eyebrow `overline` in `brand-100` (4.90:1 on brand-700; brand-200 would be 4.01 ✗), title `title`/`page-title` white (6.12:1 on brand-700, 12.5:1 on brand-900), up to 2 inline KPIs end-aligned; stacks under the text < md (spec §3.6). Text contrast verified against the *lightest* gradient stop.

---

## 5. States (every widget)

| State | Visual | Behaviour |
|---|---|---|
| **Loading (first)** | skeleton in the widget's shape: KPI = label bar 40 % + value bar 60 % × 24 px + hint bar 50 %; status = dot + 2 bars; list = 5 rows (icon circle + 2 bars); chart = 6–8 grey bars of varying heights + axis line | skeleton appears after 150 ms (avoid flash), shimmer `duration-skeleton`; `aria-busy="true"` on the widget, one polite announcement for the whole grid "Loading overview" |
| **Refreshing** | keep values; 2 px indeterminate bar at card top; values cross-fade when new | no layout shift |
| **Empty – no data** | KPI: value "—" + hint "No data yet"; chart: `nz-empty` simple + one sentence ("No activity in the last 30 days") + optional primary action from registration (e.g. "Create first invoice") if permitted; list: "You're all caught up" with check icon (success-text) | |
| **Empty – not configured** | `nz-empty` + "Set up <feature>" link for admins only; others see "Not available yet" | |
| **Error** | inline compact error inside the card: `warning` icon, "Couldn't load <title>", **Retry** link-button; other widgets unaffected | auto-retry once after 5 s with back-off; error details in a ⓘ tooltip (correlation id) |
| **Stale** | muted "Updated 12 min ago" + ⟳ icon in header | when real-time refetch failed |
| **Forbidden** | widget is not rendered at all (permission filtered); in edit mode shows "Hidden for your role" placeholder | |
| **Whole dashboard empty** (fresh workspace) | onboarding checklist widget (core) "Get started: add a legal entity, invite users, enable modules" | |
| **Offline** | values kept, stale badge on all | |

---

## 6. Personalisation (spec §9 "Dashboard builder", Could → designed now, built later)

"Customise" button in page header → edit mode: widgets get a drag handle (CDK drag-drop in the grid, keyboard: select + `Ctrl+↑/↓` to move, `Ctrl+←/→` to resize between `allowedSizes`, announced via `aria-live`), ✕ remove, "+ Add widget" opens a picker drawer listing permitted registrations grouped by module, with description + preview thumbnail. Save / Cancel / "Reset to default". Layout is stored per user (`PUT /api/v1/me/dashboard-layout`), optionally shared as a role preset by admins.

---

## 7. Worked example A — Real estate (EXAMPLE ONLY; spec §4.2)

Default layout `realestate.default` (primary industry = property management):

| Order | Widget (registered by) | Kind / size | Spec mapping |
|---|---|---|---|
| 1 | `realestate.heroportfolio` (realestate) | hero / xl | "Portfolio intelligence, in one view." + CONTRACTED RENT, ACTIVE LEASES |
| 2 | KPI strip 1 (mixed) | kpi / xl | Portfolio value (re, accent-gold) · Occupancy (re, success) · Receivables (finance, warning; hint "overdue {overdue}") · Collected this month (finance, primary; hint "billed {billed}") · NOI year to date (re, info) · Cash at bank (finance, viz-1) |
| 3 | KPI strip 2 | kpi / xl | Cheques in custody (finance) · Payables (finance) · Leases expiring (re, warning) · Open work orders (re, error when critical > 0) · Pending approvals (core) · Contracted annual rent (re) |
| 4 | `finance.billed-vs-collected` | chart grouped-bar / lg | 12 months, viz-1 billed / viz-2 collected |
| 5 | `core.alerts` | list / md | Alerts & to-dos (alert types from re + finance + core) |
| 6 | `finance.receivables-ageing` | chart horizontal-bar / md | Current, 1–30, 31–60, 61–90, 90+ |
| 7 | `realestate.occupancybyproperty` | chart stacked-bar / lg | by unit status |
| 8 | `finance.opex-ytd` | chart bar / md | |
| 9 | `realestate.contractexpiries` | chart bar / lg | next 12 months |

Status panels (optional, reference style): `realestate.occupancystatus` ("10/20 let", state degraded < 70 %), `finance.chequesstatus` (core Finance widget — cheques are core, D1; "16 in custody · 1 due"), `facilities.workordersstatus`.

## 8. Worked example B — Retail (EXAMPLE ONLY)

| Order | Widget | Kind / size | Content |
|---|---|---|---|
| 1 | KPI strip | kpi / xl | Net sales today (retail, primary, delta vs yesterday) · Transactions (retail) · Avg. basket value (retail) · Gross margin % (finance) · Stock value (inventory, info) · Receivables (finance) |
| 2 | `retail.storesstatus` ×3 | status / sm | Stores online "42/45 Online" · Tills "38/45 Optimal" · Sync failures "23" — exactly the reference's status row |
| 3 | `core.alerts` | list / md | low stock, till offline, price-change approvals |
| 4 | `retail.salesbyday` | chart bar / lg | views Sales / Transactions / Refunds; ranges 7d / 30d / 90d (reference chart) |
| 5 | `inventory.low-stock` | list (metric) / md | top 8 items below reorder point |
| 6 | `retail.topcategories` | chart donut / md | |

The two examples share **every** component; only registrations and the layout preset differ.
