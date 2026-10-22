# Token mapping: uiux screen specs (T1–T15) → `theme/tokens.scss`

Status: **done** (22 Oct 2026). Every token name the screen specs request (`uiux/coordination-notes.md` §A, T1–T15) exists as a CSS custom property `--erp-<their-name>`. It is defined in **light, dark, system-dark, the navy nav variant and compact density** (aliases are re-emitted in each block, so they follow theme, density and brand on any subtree). Aliases are `var()` pointers to our canonical tokens, so a white-label brand change flows through them too. Nothing was renamed on our side.

Files: `theme/tokens.scss` §4b (tones), §4c (aliases, `$erp-aliases`), the `:root` T11/T13 block and the "T15 ng-zorro patches" block. Compiled: `sass` OK, `lessc` OK (≈758 KB), and the `tsc` strict check of the spec contracts is OK.

## 1. T1–T15 mapping

| T | Their token (`--erp-…`) | Resolves to | Light | Dark | Notes |
|---|---|---|---|---|---|
| T1 | `sider-bg` | `color-nav-bg` | #FFFFFF | #16181C | `data-nav-theme="dark"` → navy #10253A in both themes |
| T1 | `sider-text` | `color-nav-text` | #434A55 | #C9CED6 | navy: #C9D3DD |
| T1 | `sider-text-muted` | `color-nav-section-label` | #626977 | #8B929D | navy: #8FB3C4 |
| T1 | `sider-active-bg` | `color-nav-item-active-bg` | #E8F7F0 (brand-subtle) | #0D2E23 | navy: #294251 |
| T1 | `sider-active-text` | `color-nav-text-active` | #08704B (brand text) | #34BC86 | navy: #FFFFFF |
| T1 | `header-bg` | `color-header-bg` (new) | #FFFFFF | #16181C | |
| T1 | `canvas-bg` / `surface-bg` / `border-subtle` | `color-bg-canvas` / `color-bg-surface` / `color-border-subtle` | #F4F6F8 / #FFF / #EEF0F3 | #0D0F12 / #16181C / #2A2E35 | |
| T2 | `primary`, `primary-hover`, `primary-active` | `color-primary*` | brand solid (#0B8457 default) | brand dark solid (#55BA89) | derived per workspace brand |
| T2 | `primary-disabled` | `color-primary-disabled-bg` (new) | #EEF0F3 | #2A2E35 | text: `color-primary-disabled-text` |
| T2 | `accent` | `color-accent-gold` | #BFA66A | #D4BC82 | **decorative only**; for text use `color-accent-gold-text` (#7A6530 / #D4BC82) |
| T2 | `hero-gradient-from` / `-to` | `color-hero-from` / `-to` (new) | brand-900 → brand-700 | brand-950 → brand-800 | |
| T2 | `hero-text` / `hero-eyebrow` | `color-hero-text` / `-eyebrow` (new) | #FFF (6.12 worst stop) / brand-100 (4.90) | #FFF (7.82) / brand-200 (5.13) | |
| T3 | `text-primary` | `color-text-primary` | #1F2329 | #E6E6E6 | |
| T3 | `text-secondary` | **`color-text-muted`** | #626977 | #8B929D | ⚠ their "secondary" = our **muted** (see §3) |
| T3 | `text-disabled` | `color-text-disabled` | | | |
| T3 | `text-on-primary` | `color-on-primary` | white or #111418 (auto ≥ 4.5) | | |
| T3 | `link` | `color-primary-text` | ≥ 4.5 on canvas | | |
| T4 | `tone-{success,neutral,warning,error,info,orange,plain}-{bg,fg,border}` | first-class (`$erp-tones-light/dark`) | see §2.2 | see §2.2 | + `-dot` (≥ 3:1 solid). Also the custom-field option palette |
| T5 | `kpi-accent-gold/primary/info/warning/error` | `color-accent-gold`, `color-primary`, `color-{info,warning,error}-solid` | | | |
| T6 | `money-negative`, `money-zero` | `color-money-*` | #CF1322 (5.57) / muted | #FF7875 (6.94) | |
| T7 | `focus-ring-color` | `color-focus-ring` | primary | | ⚠ `--erp-focus-ring` is already our **box-shadow** ring (use `box-shadow: var(--erp-focus-ring)`), so the colour alias is `focus-ring-color` (§3) |
| T7 | `highlight-bg` | `color-highlight-bg` (new) | brand-subtle | brand-subtle-dark | |
| T7 | `overlay-backdrop` | `color-bg-overlay` | | | |
| T7 | `skeleton-base` / `skeleton-shimmer` | `color-skeleton-base` / `color-skeleton-shine` | | | |
| T8 | `chart-1` … `chart-8` | `color-viz-1…8` | #2A78D6 blue, #EB6834 orange, … | dark set | 1/2 = spec blue/orange |
| T9 | `spacing-xs/sm/md/lg/xl/xxl` | `space-1/2/3/4/6/8` | 4 / 8 / 12 / 16 / 24 / 32 | | |
| T9 | `radius-sm/md/lg` | `radius-sm` (same name) / `radius-control` / `radius-card` | 4 / 6 / 8 | | tracks `--erp-radius-base` (brandable 0–16) |
| T9 | `elevation-1/2/3` | `shadow-1/2/3` | | dark shadows | |
| T9 | `page-padding` | same name | 24 | compact 20 | |
| T9 | `card-gap` | `grid-gap` | 16 | compact 12 | |
| T10 | `font-family` | `font-family-sans` | Inter (brandable) | | Arabic/Urdu stack under `:lang(ar/ur/fa)` |
| T10 | `type-page-title`, `-page-subtitle`, `-section-title`, `-body`, `-table`, `-label`, `-caption`, `-kpi-value`, `-kpi-label`, `-eyebrow`, `-code` | **CSS `font` shorthand**: `font: var(--erp-type-kpi-value)`; parts `--erp-type-<role>-size/-line-height/-weight`; SCSS `@include erp.type-role('kpi-value')` | page-title 20/28 700 · page-subtitle 14/22 · section-title 16/24 600 · body/table 14/22 · label 12/16 500 · caption 12/16 · kpi-value 24/32 700 · kpi-label 12 500 · eyebrow 11/16 600 uppercase · code 13 mono | | compact & Arabic re-emit |
| T11 | `header-height` | same name, **density-driven** | **56** comfortable / **48** compact | | ≥ 48 confirmed (Q11) |
| T11 | `sider-width` / `sider-width-collapsed` | `nav-width` / `nav-width-collapsed` | 248 / 64 | | |
| T11 | `drawer-width-md/lg/xl` | new | 640 / 880 / 1120 | | clamp to `100vw` < md (full-screen) |
| T11 | `content-max-width` (forms/wizard) | **`form-max-width`** = 960 | | | ⚠ our `content-max-width` = 1600 (page cap). See §3 |
| T11 | `table-row-height-comfortable/compact` | new | 44 / 32 | | `table-row-height` follows the active density |
| T12 | `motion-duration-fast/base`, `motion-easing` | `duration-fast/base`, `ease-standard` | 100 ms / 200 ms / (0.2,0,0,1) | | **0 ms** under `prefers-reduced-motion` and `data-motion="reduced"` |
| T13 | breakpoints | ng-zorro defaults, unchanged | `--erp-bp-xs/sm/md/lg/xl/xxl` = 0/576/768/992/1200/1600 | | informational vars (CSS can't use vars in `@media`); SCSS mixins `erp.up(md)` / `erp.down(lg)` |
| T14 | modes | `data-theme` light/dark/system, `data-density`, `dir`, `data-nav-theme`, white-label via `TenantThemeService.applyBranding()` | | | logical properties throughout; brand colour, font, radius and logo per workspace |
| T15 | ng-zorro patches | tokens.scss "T15 ng-zorro patches" | | | `nz-menu` pill (white & navy), `nz-table` header/density, `nz-tag` tones (`.erp-tone[data-tone]` + presets success/processing/error/warning/default), `nz-steps`, sticky `nz-drawer` footer, `nz-segmented`, `nz-badge`/danger buttons (dark-safe), `nz-header` height |

## 2. Contrast pairs required by `uiux/10-responsive-accessibility.md` §4

Computed with WCAG 2.x relative luminance from the compiled token values. **All pass. Nothing is unsatisfied.**

### 2.1 Core pairs

| Pair (requirement) | Light | Dark | Navy nav variant |
|---|---|---|---|
| Body text / surface (≥ 4.5) | #1F2329 / #FFF **15.78** | #E6E6E6 / #16181C **14.24** (raised 12.94) | — |
| Muted text / surface (≥ 4.5) | #626977 / #FFF **5.52** (canvas 5.02, subtle 5.33) | #8B929D / #16181C **5.67** (raised 5.15, subtle 5.39) | — |
| Sider text / sider bg (≥ 4.5) | #434A55 / #FFF **8.94** | **7.86** | #C9D3DD / #10253A **10.27** |
| Section label / sider bg (≥ 4.5) | **5.52** | **5.67** | #8FB3C4 **6.98** |
| Active-pill text / pill (≥ 4.5) | #08704B / #E8F7F0 **5.54** | #34BC86 / #0D2E23 **6.06** | #FFF / #294251 **10.54** |
| Primary button text / primary (≥ 4.5) | #FFF / #0B8457 **4.72** | #001C0F / #55BA89 **6.82** | — |
| Focus ring / adjacent (≥ 3) | 4.72 white · 4.35 canvas · 4.27 pill | 7.35 surface · 7.94 canvas | — |
| Input border / surface (≥ 3) | #8A919C **3.18** | **4.78** (raised 4.34) | — |
| `money-negative` / surface (≥ 4.5) | #CF1322 **5.57** | #FF7875 **6.94** | — |
| Gold accent **as text** (≥ 4.5) | `accent-gold-text` #7A6530 **5.63** (canvas 5.19) | #D4BC82 **9.58** | — |
| Count badge / danger button (extra) | #FFF / #CF1322 **5.57** | #FFF / #D32029 **5.24** | — |
| Hero text & eyebrow (extra) | #FFF 6.12 · brand-100 4.90 | #FFF 7.82 · brand-200 5.13 | — |

Brand-derived values (primary, link, pill, focus) are guaranteed by `deriveBrand()` for any workspace colour: the solid is adjusted until on-primary is ≥ 4.5, and brand text until it is ≥ 4.5 on canvas. `contrastReport` exposes the results to the branding settings screen.

### 2.2 Tone pairs: status tags **and** custom-field option colours (fg / bg ≥ 4.5)

| Tone | Light bg · fg · border | fg:bg | Dark bg · fg · border | fg:bg |
|---|---|---|---|---|
| success | #F6FFED · #237804 · #B7EB8F | **5.44** | #162312 · #73D13D · #274916 | **8.51** |
| neutral | #EEF2F6 · #3D4D5C · #CBD5E0 | **7.73** | #1F2A36 · #A9B8C7 · #33445A | **7.19** |
| warning | #FFFBE6 · #874D00 · #FFE58F | **6.53** | #2B2111 · #FFC53D · #594214 | **10.02** |
| error | #FFF2F0 · #CF1322 · #FFCCC7 | **5.10** | #2A1215 · #FF7875 · #58181C | **6.85** |
| info | #E6F4FF · #0958D9 · #91CAFF | **5.50** | #111A2C · #69B1FF · #15325B | **7.73** |
| orange | #FFF7E6 · #AD4E00 · #FFD591 | **5.09** | #2B1D11 · #FFA940 · #593815 | **8.55** |
| plain | #F4F6F8 · #434A55 · #E2E5EA | **8.25** | #2A2E35 · #C9CED6 · #3A3F47 | **8.62** |

The tone borders are decorative, because the label text carries the meaning. The `-dot` solids are ≥ 3:1 on the surface (light 3.18–5.45, dark 4.78–9.24).

## 3. Suggested renames for the uiux specs (optional, since aliases already work)

| Their name | Suggest | Why |
|---|---|---|
| `text-secondary` | `text-muted` | Our `--erp-color-text-secondary` is a darker grey (#434A55, labels and table headers). The alias maps theirs to *muted*, so if they later read our docs, the name clash will confuse. |
| `content-max-width` (forms/wizards) | `form-max-width` | Our `--erp-content-max-width` = 1600 px page cap. **Their alias was NOT created under this name** to avoid overriding ours. Use `--erp-form-max-width` (960 px). |
| `focus-ring` (as a colour) | `focus-ring-color`, or use the ring itself | `--erp-focus-ring` is the full box-shadow ring (2 px gap + 2 px primary). Prefer `box-shadow: var(--erp-focus-ring)` or the global `:focus-visible` rule, and use `--erp-focus-ring-color` only for custom outlines. |
| `type-*` | keep, but apply as `font: var(--erp-type-…)` | the tokens are font shorthands; SCSS users can `@include erp.type-role(...)` |
| Tone `attention` (if it appears anywhere) | `orange` | The public `Tone` type uses `orange` (matches custom-fields.md); `attention-*` is now only our internal semantic name |
| Manifest `keyCamel` (earlier draft) | removed; `key` is lower case (`realestate`) plus optional `routePrefix` (`real-estate`) | as proposed in 08 §5 |
| Manifest property names from 08 §5 | `menu`→`nav`, `alertDefinitions`→`alerts`, `dashboardWidgets`→`dashboard.widgets`, `PermissionResourceDef`→`PermissionArea {module, resource, actions}` | Our `ModuleManifest` (specs/app-shell.md §2) is the accepted standard; it already absorbed `apiBase`, `plans`, `countryPacks`, `roleTemplates`, `approvalDocumentTypes`, `reports`, `extensions`, `onboarding` |

Everything else matches 1:1 or via the aliases above. The specs need no other changes.

## 4. Settled choices (no open questions)

* **Sider:** both looks ship. The default is the white sider from the reference; `<html data-nav-theme="dark">` gives the spec's navy, and it is a per-workspace setting. Both are AA.
* **Header:** 56 px comfortable, 48 px compact.
* **Drawer widths:** md 640 / lg 880 / xl 1120. Below md, drawers go full screen.
* **Form max width:** 960 px.
* **Dark danger fill:** #D32029, not the text red #FF7875 (white text would only reach 2.56), with a 3.39 non-text contrast against the surface.
* **Gold:** stays decorative. `accent-gold-text` is the only gold for text.
* **Tone palette:** fixed and not brandable. User-facing names are Green, Slate, Amber, Red, Blue, Orange, Grey.
* **Coordinator decisions applied across this folder:**
  * The permission is `core.customfields.manage`. Keys follow `<module>.<resource>.<action>` in all lower case, with `.read` for viewing (e.g. `realestate.leases.read`, `retail.tillsessions.create`).
  * Module keys are `realestate` and `retail`. Routes are `/real-estate/` and `/retail/`, and API bases are `/api/v1/m/realestate/` and `/api/v1/m/retail/`.
  * Invoices, receipts, bills and payments sit in the core Finance group (`/finance/...`). There are no Sales or Purchases groups.
  * Field keys match `^cf_[a-z0-9_]{1,60}$`.
  * Draft → publish applies to both the field builder and the layout designer.
  * v1 ships 15 types; 7 types come in sprint 3, hidden through `GET /api/v1/metadata/capabilities`.
  * The module manifest is the accepted standard.
