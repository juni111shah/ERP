# ERP Design System — foundations & tokens

Owner: Advanced UI/UX · Status: Sprint 0 · Stack: Angular 17+ (standalone, signals) + ng-zorro-antd 17–19 (LESS engine, CSS-variable mode)
Sources: spec §3 (design system), §4 (screens), §9 (Arabic RTL, white-labelling), §10.2 (Angular), dashboard reference image; teammate files `uiux/00-shared-conventions.md`, `uiux/07-app-shell.md`, `uiux/custom-fields.md`.

> **Hard constraint (project owner):** nothing in the design system is tied to a business type. Tokens, shell and dashboard are industry-neutral; menus, KPI cards, widgets, alerts and labels come from whichever modules are enabled (see `specs/app-shell.md` §2, module manifest). Real estate is only a worked example.

Files: `theme/tokens.scss` (source of truth), `theme/ng-zorro-overrides.less` (+ `theme/var-safe-color-functions.js`), `theme/tenant-theme.service.ts`, `theme/_dynamic-fields.scss`, `theme/styles.scss` (entry).

---

## 1. Principles

1. **Data first, chrome second.** ERP users scan numbers all day: tabular numerals, right-aligned money, quiet surfaces, colour reserved for meaning (status, trend, brand action).
2. **Meaning is never colour-only.** Every status has text; every chart has a table alternative; every custom-field marker has an icon + tooltip.
3. **One stylesheet, every context.** Light/dark, comfortable/compact, LTR/RTL, any workspace brand are runtime switches of CSS custom properties — no per-tenant builds.
4. **Neutral core, expressive modules.** Core uses neutral greys + the workspace brand. A module may add one *accent* (decorative), never its own palette.
5. **Accessible by construction.** Every semantic text token passes WCAG 2.2 AA on the surfaces it is allowed on; the brand derivation enforces it for any tenant colour.
6. **Predictable motion.** Short, functional, token-timed; zero motion under `prefers-reduced-motion` or the user's "Reduce motion" setting.
7. **Spec-faithful where it observed, opinionated where it was silent.** Every deviation is listed in §12.

---

## 2. Token architecture

```
Primitive  ($erp-primitives in SCSS; not emitted)      green-600, grey-50, night-100, error-solid, viz-1 …
   │
Brand      --erp-brand-50 … 950 + resolved roles         ← TenantThemeService overwrites at runtime
   │        (--erp-brand-solid-light, -on-solid-light, -text-light, -subtle-light, -ink, …-dark)
   │
Semantic   --erp-color-*  (light map / dark map)        what components use
   │
Component  --erp-nav-width, --erp-kpi-accent-width, density vars, …
   │
Bridge     --ant-primary-color … (ng-zorro variable theme) + LESS vars pointing at --erp-*
```

Naming: `--erp-{category}-{role}[-{variant}]`, kebab-case, **no industry words** (SQA denylist `sqa/core-vocabulary-denylist.txt` passes on all theme files). SCSS helpers: `erp.color('text-muted')`, `erp.space(4)`, `erp.radius('card')`, `erp.duration('base')`, `erp.ease('standard')`, mixins `erp.up(md)`, `erp.down(lg)`, `erp.focus-ring`, `erp.type('kpi')`, `erp.tabular-numbers`, `erp.visually-hidden`.

Theme switches (all on `<html>`, set by `TenantThemeService`):

| Attribute | Values | Effect |
|---|---|---|
| `data-theme` | `light` · `dark` (`system` resolved by the service; a pure-CSS `[data-theme=system]` fallback also exists) | swaps the semantic colour map + shadows |
| `data-density` | `comfortable` · `compact` | control/table/card sizes, body size |
| `data-motion` | absent · `reduced` | zeroes durations |
| `dir` / `lang` | `ltr`/`rtl`, `en`/`ar`/`ur`… | logical layout, Arabic-script type |
| inline `style` | `--erp-brand-*`, `--erp-radius-base`, `--erp-brand-font`, `--erp-brand-logo` | workspace branding |

---

## 3. Colour

### 3.1 Brand decision

The spec's demo uses teal-blue `#1F5F7A` on a navy sidebar (§3.1). The project owner's dashboard reference uses a **white sidebar with green accents** and deep-green headings. The product is a generic, white-label ERP, so:

* **Default product brand = green `#0B8457`** (`green-600`), with deep-green ink `#0B3B2E` for headings, matching the reference.
* **White sidebar** (`--erp-color-nav-bg`) with a brand-tinted active pill, per the reference.
* The spec's teal/navy look is preserved as a **"Classic navy" branding preset** (primary `#1F5F7A`; derivation output below), so a workspace can choose it with one setting. Sidebar-dark is a planned preset (§12, G-3).

### 3.2 Primitive palette

| Family | 50 | 100 | 200 | 300 | 400 | 500 | **600** | 700 | 800 | 900 | 950 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| green (default brand) | `#E8F7F0` | `#CBEEDD` | `#9DDFC2` | `#66CBA2` | `#34BC86` | `#12A06C` | **`#0B8457`** | `#08704B` | `#075E40` | `#0B3B2E` | `#062419` |

| Light neutrals | 0 | 25 | 50 | 100 | 200 | 300 | 400 | 500 | 600 | 700 | 800 | 900 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| grey | `#FFFFFF` | `#FAFBFC` | `#F4F6F8` | `#EEF0F3` | `#E2E5EA` | `#CDD2D9` | `#A3AAB5` | `#8A919C` | `#626977` | `#434A55` | `#2B313A` | `#1F2329` |

| Dark neutrals | 0 | 50 | 100 | 200 | 300 | 400 | 500 | 600 | 700 | 800 | 900 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| night | `#0D0F12` | `#16181C` | `#1E2126` | `#2A2E35` | `#3A3F47` | `#5C636E` | `#7D8592` | `#8B929D` | `#A6ADB8` | `#C9CED6` | `#E6E6E6` |

Status families keep Ant Design hues (so ng-zorro tags/alerts stay coherent and match spec §3.1 tag backgrounds): success `#F6FFED / #B7EB8F / #389E0D / #237804`, warning `#FFFBE6 / #FFE58F / fill #FAAD14 / solid #B87503 / text #874D00`, error `#FFF2F0 / #FFCCC7 / #CF1322`, info `#E6F4FF / #91CAFF / #1677FF / text #0958D9`, attention (orange) `#FFF7E6 / #FFD591 / #D46B08 / text #AD4E00`. Dark variants: success `#73D13D`, warning `#FFC53D`, error `#FF7875`, info `#69B1FF`, attention `#FFA940` on tinted dark backgrounds.

Data-viz (categorical, industry-neutral): `viz-1 #2A78D6` (spec chart blue), `viz-2 #EB6834` (spec chart orange), `viz-3 #13A8A8`, `viz-4 #8E5BD9`, `viz-5 #E84A86`, `viz-6 #BFA66A` (spec gold), `viz-7 #7CB305`, `viz-8 #5B6B7F`; dark versions are lighter (`#5DA9F0`, `#F0855A`, …). Order 1→8 alternates hue and lightness, which keeps it distinguishable for common colour-vision deficiencies when ≤ 4 series are shown; > 4 series must also use patterns/labels (accessibility spec §6).

### 3.3 Semantic tokens (light → dark)

| Token `--erp-color-…` | Light | Dark | Use |
|---|---|---|---|
| `bg-canvas` | grey-50 `#F4F6F8` | night-0 `#0D0F12` | page background (spec `#F3F6FA` ≈) |
| `bg-surface` | `#FFFFFF` | night-50 `#16181C` | cards, header, sidebar, tables |
| `bg-surface-raised` | `#FFFFFF` | night-100 `#1E2126` | dropdowns, modals, drawers, popovers |
| `bg-subtle` | grey-25 | `#1A1D21` | table header, read-only blocks |
| `bg-sunken` | grey-100 | night-0 | wells, disabled inputs |
| `bg-hover` / `bg-pressed` | grey-50 / grey-100 | night-100 / night-200 | row & item hover / press |
| `bg-selected` | brand-subtle | brand-subtle-dark | selected rows, active menu item |
| `bg-inverse` | grey-900 | night-900 | tooltips |
| `bg-overlay` | rgba(16,24,40,.45) | rgba(0,0,0,.65) | modal mask |
| `border-subtle` | grey-100 `#EEF0F3` (spec) | night-200 | card borders, dividers |
| `border-default` | grey-200 | night-300 | separators that must be seen |
| `border-control` | grey-500 `#8A919C` | night-500 `#7D8592` | input/select/checkbox borders (≥3:1) |
| `border-strong` | grey-400 | night-400 | drag outlines, scrollbars |
| `text-primary` | grey-900 `#1F2329` | night-900 `#E6E6E6` | body |
| `text-heading` | brand-ink `#0B3B2E` | night-900 | page/card titles (reference's deep green) |
| `text-secondary` | grey-700 | night-700 | labels, table headers |
| `text-muted` | grey-600 `#626977` | night-600 `#8B929D` | hints, subtitles, captions |
| `text-disabled` | grey-400 | night-400 | disabled only (exempt from contrast) |
| `text-inverse` | `#FFFFFF` | night-0 | on inverse/tooltip |
| `primary` / `-hover` / `-active` | brand-600 / 700 / 800 | brand-400 / 300 / 500 | primary buttons, switches, ink bars |
| `on-primary` | `#FFFFFF` | brand-950 | text/icons on primary |
| `primary-text` | brand-700 `#08704B` | brand-400 | links, active nav label, brand text |
| `primary-subtle` / `-subtle-hover` / `-border` | brand-50 / 100 / 300 | tinted dark | active pill, selected, focus halo |
| `focus-ring` | brand-600 | brand-400 | 2 px focus outline |
| `{success,warning,error,info,attention,neutral}-{text,solid,bg,border}` | see §3.2 | dark variants | tags, alerts, icons, dots |
| `warning-fill` | `#FAAD14` | `#FFC53D` | large warning fills with dark text |
| `money-negative` / `money-zero` | error-text / grey-600 | error dark / night-600 | MoneyPipe (spec §3.2: negatives red with minus, zero grey) |
| `trend-up` / `trend-down` | success-text / error-text | dark variants | KPI deltas (always with ↑/↓ + text) |
| `nav-bg`, `nav-border`, `nav-text`, `nav-text-active`, `nav-icon`, `nav-item-hover-bg`, `nav-item-active-bg`, `nav-section-label` | white sidebar set | dark sidebar set | sidebar (07-app-shell "active-pill token" = `nav-item-active-bg`) |
| `accent-gold` | `#BFA66A` | `#D4BC82` | decorative KPI accent (spec gold; uiux/00 `accent-gold`) |
| `module-accent` | `var(--erp-module-accent, brand)` | lightened | decorative module identity |
| `viz-1…8`, `viz-grid`, `viz-axis` | §3.2 | dark set | charts |
| `skeleton-base` / `skeleton-shine` | grey-100 / grey-50 | night-100 / night-200 | loaders |

Status-tag map (spec §3.4, generic names): **success** = Active/Posted/Paid/Completed/Approved; **neutral** = Issued/Pending/Open/In progress/Scheduled; **warning** = Draft/Partly paid; **error** = Overdue/Lost/Critical/Void; **info** = Normal priority/informational; **orange** (internally `attention`) = High priority/System; **plain** (neutral-bg + text-muted) = Low/Inactive. Modules map their own statuses onto these **tones** in the manifest (`statusTones`), never onto hex values.

### 3.4 Contrast ratios (WCAG 2.x, computed)

| Foreground on background | Ratio | AA verdict |
|---|---|---|
| white on `primary` #0B8457 | **4.72** | text ✓ |
| `primary-text` #08704B on white / canvas | **6.12 / 5.65** | text ✓ |
| `primary` #0B8457 on canvas #F4F6F8 | 4.35 | ✗ for text → why links use `primary-text` |
| `text-heading` #0B3B2E on white / canvas | 12.51 / 11.54 | ✓ |
| `text-primary` #1F2329 on white / canvas | 15.78 / 14.57 | ✓ |
| `text-muted` #626977 on white / canvas | **5.52 / 5.02** | ✓ |
| spec muted #8C8C8C on white | 3.36 | ✗ — replaced (gap G-5) |
| `border-control` #8A919C on white | 3.18 | non-text ✓ (1.4.11) |
| `success-text` #237804 on white / on success-bg | 5.59 / 5.44 | ✓ |
| `success-solid` #389E0D on white | 3.46 | non-text ✓ (icons/dots) |
| spec success #52C41A on white | 2.27 | ✗ even for icons → only as decorative fill |
| `warning-text` #874D00 on warning-bg | 6.53 | ✓ |
| `warning-solid` #B87503 on white | 3.76 | non-text ✓ |
| `error-text` #CF1322 on white / error-bg | 5.57 / 5.10 | ✓ |
| `info-text` #0958D9 on white / info-bg | 6.16 / 5.50 | ✓ |
| `info-solid` #1677FF on white | 4.10 | non-text ✓, not for small text |
| viz-1 #2A78D6 / viz-2 #EB6834 on white | 4.42 / 3.20 | graphical ✓ |
| **Dark:** `text-primary` #E6E6E6 on surface #16181C | 14.24 | ✓ |
| `text-muted` #8B929D on surface / raised | 5.67 / 5.15 | ✓ |
| `primary` #34BC86 on surface | 7.35 | ✓ (text & non-text) |
| `on-primary` #062419 on #34BC86 | 6.82 | ✓ |
| `border-control` #7D8592 on surface | 4.78 | ✓ |
| success/warning/error/info dark solids on surface | 9.24 / 11.26 / 6.94 / 7.90 | ✓ |
| `accent-gold` #BFA66A on white | 2.37 | decorative only (never sole carrier of meaning) |

---

## 4. Typography

| Stack | Value |
|---|---|
| `--erp-font-family-sans` | `var(--erp-brand-font,'Inter'), 'Noto Sans Arabic', system-ui, -apple-system, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif` |
| `--erp-font-family-arabic` (auto for `:lang(ar|ur|fa)`) | `'Noto Sans Arabic', 'IBM Plex Sans Arabic', 'Segoe UI', Tahoma, <brand font>, sans-serif` |
| `--erp-font-family-urdu-display` | `'Noto Nastaliq Urdu', 'Noto Sans Arabic', serif` — **opt-in for long-form reading only** (letters, print). UI chrome stays Naskh-style: Nastaliq needs ~2× line-height and breaks dense tables. |
| `--erp-font-family-mono` | `'JetBrains Mono', 'Cascadia Mono', ui-monospace, …` (API keys, record ids) |

Inter (spec §3.2) is self-hosted (woff2, `font-display: swap`, subset Latin + Latin-ext). Noto Sans Arabic covers Arabic, Urdu (ٹ ڈ ڑ ں ے ۓ), Persian, so one family serves `ar` and `ur`. Load it only when an Arabic-script language is active or Arabic data is shown (unicode-range subset).

Type scale (comfortable; px / line-height / weight):

| Token | Size | LH | Wt | Use |
|---|---|---|---|---|
| `overline` | 11 | 16 | 600, +0.06em, uppercase (Latin only) | sidebar section labels, hero eyebrow (spec 10–11 px) |
| `caption` | 12 | 16 | 400 | hints, KPI hint line, timestamps |
| `label` | 12 | 16 | 500 | form labels, KPI labels |
| `body-sm` | 13 | 20 | 400 | compact tables (spec 12–13 px) |
| `body` | 14 | 22 | 400 | default body/tables (comfortable) |
| `body-lg` | 16 | 24 | 400 | reading text, empty-state copy |
| `title-sm` | 16 | 24 | 600 | card / section titles |
| `title` | 18 | 26 | 600 | drawer/modal titles |
| `page-title` | 20 | 28 | 700 | page titles (spec ~20 px bold) |
| `kpi` | 24 | 32 | 700, tabular | KPI values (spec 18–20, reference larger → 24 comfortable / 20 compact) |
| `display` | 30 | 38 | 700 | hero / onboarding only |

Rules: numbers use `font-variant-numeric: tabular-nums` everywhere in tables, KPIs and forms (`erp.tabular-numbers`). **Arabic script**: +1 px size, taller line-height (body 15/26), `letter-spacing: 0` (tracking breaks joining), no `text-transform: uppercase`, no italics (use weight). Min body size 13 px; never below 11 px.

---

## 5. Spacing, layout, radius, elevation

**Spacing** (4 px base): `--erp-space-0` 0 · `0-5` 2 · `1` 4 · `2` 8 · `3` 12 · `4` 16 · `5` 20 · `6` 24 · `8` 32 · `10` 40 · `12` 48 · `16` 64.
Page padding 24 (compact 20) — spec 20–24; card gap 16 (compact 12 — spec 12); card padding 20 (compact 16).

**Breakpoints** (= ng-zorro): xs 0 · sm 576 · md 768 · lg 992 · xl 1200 · xxl 1600. Content max-width 1600 (centred on ultra-wide).

**Radius** (tenant-controlled `--erp-radius-base`, default 6 px, clamp 0–16):
`none` 0 · `xs` 2 · `sm` = base−2 · `control` = base (buttons, inputs, tags) · `card` = base+2 (spec ~8 px) · `overlay` = base+4 (modals, dropdowns, drawers) · `pill` 999 · `round` 50%.

**Elevation**: `--erp-shadow-0…4`. 1 = resting card (barely there; spec uses 1 px border #EEF0F3 + soft shadow), 2 = dropdown/popover, 3 = drawer, sticky header on scroll, dragged item, 4 = modal, toast, command palette, drag preview. Dark mode uses a 1 px inner white hairline + deeper black shadow, because shadows alone vanish on dark surfaces; raised surfaces are also lighter (night-100).

**z-index**: sticky 100 · nav 200 · header 300 · overlay (modal/drawer) 1000 · dropdown 1050 · command palette 1060 · toast 1070.

---

## 6. Motion

| Duration token | ms | Typical use |
|---|---|---|
| `instant` | 0 | state changes that must not animate (sorting result) |
| `fast` | 100 | hover/press colour, icon swap |
| `quick` | 150 | focus ring, tooltip, small fade, error message |
| `base` | 200 | dropdown, toggle, accordion, menu |
| `moderate` | 250 | sidebar collapse, mobile drawer |
| `slow` | 300 | modal, page enter |
| `slower` | 400 | chart entrance, count-up |
| `skeleton` | 1400 | shimmer loop |

| Easing | Curve | Use |
|---|---|---|
| `standard` | `cubic-bezier(0.2, 0, 0, 1)` | most transitions (on-screen → on-screen) |
| `decelerate` | `cubic-bezier(0, 0, 0, 1)` | entering elements |
| `accelerate` | `cubic-bezier(0.3, 0, 1, 1)` | exiting elements (use ~70 % of enter duration) |
| `emphasized` | `cubic-bezier(0.05, 0.7, 0.1, 1)` | toasts, command palette, celebratory moments |
| `linear` | `linear` | progress bars, shimmer |

**Reduced motion**: `@media (prefers-reduced-motion: reduce)` and `[data-motion=reduced]` set every duration token to 0 ms; components additionally drop transforms (no slide/scale) and keep only instant opacity changes; skeletons become static; charts render without entrance animation (`animation: false` in ECharts options when `matchMedia` says reduce). Details: `specs/micro-interactions.md`.

---

## 7. Multi-tenant (workspace) branding

Workspace admins set in **Settings → Branding** (permission `admin.settings`):

| Setting | CSS var / API | Constraints |
|---|---|---|
| Primary colour | `--erp-brand-*` (derived) | any `#RRGGBB`; auto-adjusted for AA (admin sees the report) |
| Optional dark-mode primary | `primaryColorDark` | else derived |
| Logo (light / dark) + square mark | `--erp-brand-logo`, `--erp-brand-logo-dark`; `logoUrl()` signal | https, same-origin path or `data:image/*`; SVG/PNG ≤ 200 KB; displayed max 160×40 (mark 32×32) |
| Corner radius | `--erp-radius-base` | 0–16 px, everything else derives |
| Font | `--erp-brand-font` (+ optional woff2 URL loaded via `FontFace`) | Latin only; Arabic script always uses the Arabic stack |

**Runtime flow**: after sign-in the session store loads workspace settings → `TenantThemeService.applyBranding()` writes CSS vars inline on `<html>` (one style recalc, no flash: the boot spinner covers it; for zero-flash, the server also inlines the last-known brand vars into `index.html`'s `<style>` via the login/workspace resolution endpoint). Sign-out → `resetBranding()`.

**Contrast-safe derivation** (implemented in `deriveBrand()`; pure, unit-testable):
1. Convert the brand colour to **OKLCH**; build the 50–950 scale at fixed lightness stops (0.97 → 0.225) keeping hue, scaling chroma per step (lighter steps less saturated) and gamut-mapping by chroma reduction.
2. **Solid (button) light**: keep the exact brand colour if white *or* near-black text reaches 4.5:1 (so a yellow brand keeps dark text); else darken in OKLCH until white passes, and report the adjustment.
3. **Brand text light** (links/active nav): darken until ≥ 4.5:1 on the canvas (the darker light surface).
4. **Dark theme**: use the vivid 400 step (or the input) if it reaches 4.5:1 on the raised dark surface, else lighten until it does; on-colour = white or a hue-tinted near-black, whichever contrasts more.
5. Subtle tints/borders: scale steps (light) or mixes into the dark surface (dark).
6. Hover = ±0.06 L, active = ±0.12 L (darker on white-text brands, lighter on dark-text brands).

Verified outputs (node run of the shipped function):

| Input | Solid light / on / ratio | Brand text light | Solid dark / on / ratio |
|---|---|---|---|
| `#0B8457` (default green) | `#0B8457` / white / 4.72 | `#028154` (4.54 on canvas) | `#55BA89` / `#001C0F` / 7.48 |
| `#1F5F7A` (spec teal, "Classic navy") | `#1F5F7A` / white / 7.06 | `#1F5F7A` | `#6FACCA` / `#001925` / 7.23 |
| `#FFD400` (yellow) | `#FFD400` / `#111418` / 12.9 | `#846D05` | `#C0A01B` / `#1C1500` / 7.16 |
| `#38BDF8` (sky) | `#38BDF8` / `#111418` / 8.62 | `#0E79A4` | `#24B1EB` / `#001926` / 7.34 |
| `#E11D48` (rose) | `#E11D48` / white / 4.70 | `#DD1645` | `#FC6F7A` / `#290B0D` / 6.70 |
| `#111111` (black) | `#111111` / white / 18.9 | `#111111` | `#A3A3A3` / `#161616` / 7.17 |

Guard-rails: brand colour is never used for status meaning (a red brand doesn't make buttons look like errors because destructive actions also use icon + "danger" outline + confirm text); status tones are **not** tenant-overridable (consistency + accessibility); module accents are passed through `applyModuleAccent()` which enforces 3:1 non-text contrast.

---

## 8. RTL (Arabic, Urdu, Persian)

| Rule | Implementation |
|---|---|
| Direction follows language | `TenantThemeService.setLanguage('ar')` → `dir="rtl"`, `lang="ar"` on `<html>`; root template `<div [dir]="theme.dir()">` (CDK `Dir`) so ng-zorro components (which read CDK `Directionality`, surfaced as `nzDirection`) and their overlays flip. The i18n layer also calls `NzI18nService.setLocale(ar_EG | ur_PK | en_US)` and switches the Angular locale data used by pipes. |
| Logical properties only | `margin-inline-start`, `padding-inline`, `inset-inline-end`, `border-inline-start`, `text-align: start`. Stylelint rule `liberty/use-logical-spec` (or `plugin/use-logical`) fails the build on `left/right` in component SCSS. |
| Transforms | multiply X translations by `var(--erp-direction-sign)` (1 / −1). |
| Icons | **Mirror** directional icons (chevrons, arrows, back/forward, indent, list-bullets, send, undo/redo, sidebar collapse «/», breadcrumb separator, progress/steps): class `.erp-icon-directional { transform: scaleX(var(--erp-direction-sign)) }` or `rotate(var(--erp-chevron-rotate))`. **Do not mirror**: checkmarks, clocks, media play, search magnifier, brand logos, charts axes direction of time *in tables* (see below), slashes in codes, phone/keyboard icons. |
| Numerals | Default **Western Arabic digits (0–9)** in every locale for financial data (common practice in Gulf/Pakistan business software, matches bank statements, avoids mixed-digit errors) via `Intl.NumberFormat(locale, { numberingSystem: 'latn' })`. User preference "Use Arabic-Indic digits (٠١٢)" switches `numberingSystem: 'arab'` (ar) / `'arabext'` (ur, ۰۱۲) for display only; inputs always accept both and normalise. |
| Numbers inside RTL text | wrap amounts, codes, IBANs, phone numbers, emails and dates in `<bdi>` / `dir="ltr"` spans (`unicode-bidi: isolate`) so "INV-2026-000028" and "-1,250.00" don't reorder. Money stays currency-code + amount in reading order of the locale (`Intl` handles it). Minus sign: use `Intl` output (U+2212 / LRM handled), never string concat. |
| Tables | columns order flips (first column on the right); numeric columns remain *end*-aligned (= left in RTL); pinned action column pins to `inline-end`. |
| Charts | ECharts in RTL: mirror the category/time x-axis (`xAxis.inverse: true`) so time flows right→left, put the y-axis on the right (`yAxis.position: 'right'`), align legend and tooltip to the end. Numbers on axes still use the chosen digit system. |
| Forms | labels above fields (no left/right label alignment problems); input text for codes/emails/URLs/IBAN `dir="ltr"` with `text-align: end`. |
| Type | Arabic stack + metrics (§4). |
| Testing | Pseudo-RTL build (`?dir=rtl` on any language) for every PR screenshot test; Playwright visual tests in `ar` + `ur`. |

---

## 9. Data density (ERP tables & forms)

| Var | Comfortable (default) | Compact |
|---|---|---|
| `--erp-control-height` | 32 px (ng-zorro default) | 28 px (`nzSize="small"`) |
| `--erp-table-row-height` | ~44 px | ~32 px (spec rows 25–35 px) |
| `--erp-table-cell-py / px` | 10 / 12 | 5 / 8 |
| `--erp-font-size-body` | 14 px | 13 px (spec `--re-fs:13px`) |
| `--erp-card-padding` / `--erp-grid-gap` / `--erp-page-padding` | 20 / 16 / 24 | 16 / 12 / 20 |
| `--erp-nav-item-height` | 40 | 34 |
| `--erp-form-row-gap` | 16 | 12 |

* User preference (avatar menu → Preferences → Density), persisted per user; defaults to comfortable (also on touch devices: `pointer: coarse` forces comfortable regardless).
* Tables additionally expose a per-table density toggle in the toolbar (`nz-segmented` icons) for power users; the shared `DataTableComponent` maps it to `[nzSize]` (`default` / `small`) and a `data-density` attribute on the table card.
* Minimum target size stays ≥ 24×24 px in compact (WCAG 2.5.8); row action icons get 28×28 hit areas.

---

## 10. Tokens → ng-zorro mapping

| Component | Tokens / config | Notes |
|---|---|---|
| Global | `@primary-color` = `var(--ant-primary-color)` = `var(--erp-color-primary)`; `@font-family`, `@border-radius-base`, `@body-background`, `@component-background`, `@text-color`, `@border-color-base`, shadows, easings, durations → `var(--erp-*)` | spec §3.7 asked for exactly these three LESS vars to be fed from CSS vars; we went further |
| `nz-button` primary | `primary`, `primary-hover`, `primary-active`, `on-primary`; danger = `error-solid` | hover goes **darker** (white text stays ≥4.5:1) |
| `nz-input`, `nz-select`, `nz-date-picker`, `nz-input-number` | `border-control`, `bg-surface`, focus `--erp-focus-ring` (2 px), radius `control`, height via `nzSize` | |
| `nz-table` | header `bg-subtle` + `text-secondary`; row hover `bg-hover`; selected `bg-selected`; borders `border-subtle`; padding density vars; tabular nums | 25 rows/page (spec) |
| `nz-tag` | tones → `{tone}-bg / -border / -text`; plain → `neutral-*` | `StatusTagComponent` maps status→tone |
| `nz-card` | `bg-surface`, `border-subtle`, radius `card`, `shadow-1`, padding density | KPI/Status/Chart cards wrap it |
| `nz-menu` (sidebar) | `nav-*`; active pill `nav-item-active-bg` + `nav-text-active`; no edge bar (`@menu-item-active-border-width: 0`) | white sidebar (`nzTheme="light"`) |
| `nz-layout` / `nz-header` / `nz-sider` | `bg-canvas`, `bg-surface`, `nav-bg`, `--erp-header-height` 56 / 48 compact, `--erp-nav-width` 248 / 64 | |
| `nz-modal`, `nz-drawer`, `nz-dropdown`, `nz-popover` | `bg-surface-raised`, `shadow-3/4`, radius `overlay`, mask `bg-overlay` | |
| `nz-tooltip` | `bg-inverse` + `text-inverse` | readable in both themes |
| `nz-alert`, `nz-message`, `nz-notification` | status `-bg/-border/-text/-solid` | toasts: §micro-interactions |
| `nz-badge` | count = `error-solid` + white (5.57:1); dot = status solids | |
| `nz-skeleton` | `skeleton-base/shine`, `duration-skeleton` | static under reduced motion |
| `nz-tabs` | ink bar `primary`; active `primary-text` | |
| `nz-switch`, `nz-checkbox`, `nz-radio` | `primary`; unchecked border `border-control` | |
| `nz-steps`, `nz-progress` | `primary`, `success-solid`, `error-solid` | |
| Charts (ngx-echarts) | `viz-1…8`, `viz-grid`, `viz-axis`, `text-muted` read from CSS vars at runtime (theme-change → `setOption`) | `specs/dashboard.md` §6 |

### 10.1 Integration (angular.json)

```jsonc
"styles": [
  "src/theme/ng-zorro-overrides.less",  // imports ng-zorro-antd.variable.less + our overrides
  "src/theme/styles.scss"               // tokens + --ant-* bridge + patches + dynamic fields
],
"stylePreprocessorOptions": { "includePaths": ["src/theme"] }
// LESS needs javascriptEnabled: true (ng-zorro's colorPalette); Angular CLI enables it for .less by default.
```

`ng-zorro-overrides.less` registers `var-safe-color-functions.js` (a 40-line Less plugin) so ng-zorro's internal `fade()/tint()/darken()` calls on our `var(--erp-…)` values compile to native `color-mix()` (Chrome 111+, Safari 16.2+, Firefox 113+ — fine for a 2026 back-office app). **No `ng-zorro-antd.dark.css`** is shipped; dark mode is the same CSS with different vars.

---

## 11. Component inventory (shared, industry-neutral)

`AppShellComponent`, `SidebarNavComponent`, `HeaderComponent`, `EntitySwitcherComponent`, `CommandPaletteComponent`, `NotificationCenterComponent`, `PageHeaderComponent`, `HeroBannerComponent`, `KpiCardComponent`, `StatusPanelComponent`, `AlertListComponent`, `ChartCardComponent`, `WidgetGridComponent`, `DataTableComponent`, `StatusTagComponent`, `MoneyPipe`, `InfoGridComponent`, `DynamicFormComponent` / `DynamicFieldComponent` (custom fields), `EmptyStateComponent`, `ErrorStateComponent`. Names match `uiux/00-shared-conventions.md` and spec §10.2.

---

## 12. Decisions, spec gaps and conflicts

| # | Topic | Spec says | Decision |
|---|---|---|---|
| C-1 | Brand colour | teal-blue `#1F5F7A`, gold accent, navy sidebar (§3.1) | Green `#0B8457` default + white sidebar per owner's reference; spec look = "Classic navy" preset |
| C-2 | Token prefix | `--re-*` (§3.7) | `--erp-*`; `re` is product/industry-specific. Mapping: `--re-primary→--erp-color-primary`, `--re-bg→bg-canvas`, `--re-card→bg-surface`, `--re-border→border-subtle`, `--re-text→text-primary`, `--re-muted→text-muted`, `--re-chart-1/2→viz-1/2`, `--re-radius→radius-card`, `--re-gap→grid-gap`, `--re-pad→page-padding`, `--re-fs→font-size-body` |
| C-3 | Dark cards | cards = canvas `#141414` (§3.5) | cards one step lighter (`#16181C` on `#0D0F12`) so elevation reads; spec's "thin gold top accent" kept as optional `accent-gold` KPI accent |
| G-4 | Sidebar in dark | "sidebar stays navy" | Default nav follows the theme (white / near-black). **Shipped:** `<html data-nav-theme="dark">` gives the spec's navy sider (#10253A) in both themes, all pairs AA (text 10.27, label 6.98, active 10.54). |
| G-5 | Muted text | `#8C8C8C` (3.36:1) | `#626977` (5.52:1) — spec value fails AA |
| G-6 | Success colour | `#52C41A` | text `#237804`, icons `#389E0D`; `#52C41A` fails even 3:1 |
| G-7 | Base font size | 12–13 px | 14 px comfortable / 13 px compact (readability + AA reflow); compact = spec fidelity |
| G-8 | Primary hover | AntD lightens on hover | darken on hover (lighter green fails white text) |
| G-9 | Input borders | AntD `#D9D9D9` (1.4:1) | `#8A919C` (3.18:1) for WCAG 1.4.11 |
| G-10 | Arabic/Urdu | "Arabic RTL" Should (§9); no font/digits guidance | Noto Sans Arabic for both; Nastaliq opt-in for long-form; Western digits default |
| G-11 | Charts lib | ngx-echarts (§4.2, §10.2) | kept |
| G-12 | Custom-field permission key | BA `customization.fields.manage` vs uiux `admin.customFields` | **Resolved (coordinator):** `core.customfields.manage`. All permission keys use `<module>.<resource>.<action>`. |
| G-13 | Custom-field key format | BA `cf_` snake prefix; backend `^[a-z][a-zA-Z0-9_]{0,63}$` | **Resolved (coordinator):** `^cf_[a-z0-9_]{1,60}$`; the backend regex should match it |
| G-14 | Field types | backend enum has 9 types | **Resolved (coordinator):** v1 = the 15 BA §8 types; the other 7 come in sprint 3. The UI hides types missing from `GET /api/v1/metadata/capabilities` (dynamic-fields §1.2). |
| G-15 | Header height | spec ~32 px; uiux Q11 ≥ 48 | **56 px comfortable / 48 px compact** (`--erp-header-height`, density-driven) |
| G-16 | Tone names | we had `attention`; uiux uses `orange` | Public `Tone` type and `--erp-tone-*` use **`orange`**. `attention-*` stays an internal semantic name for the same hue (alert severity "high"). |
| G-17 | Option colours | uiux A.3.1 | The 7 status tones are the only option palette (`--erp-tone-<t>-bg/fg/border/dot`); fixed, not brandable, all AA in both themes |
| G-18 | Draft/publish | uiux A.5 saved layouts directly | **Resolved (coordinator):** draft → publish for both the field builder and the layout designer |
| G-20 | Module and permission naming | earlier drafts: `real-estate` key + `realEstate` camel twin; `.view` | Module key `realestate` (lower case, no separators) + `routePrefix: 'real-estate'`. API base `/api/v1/m/realestate/`. Permissions are all lower case `<module>.<resource>.<action>` with `.read` (uiux 08 §2/§5). NB: BA core-domain BR-SEC-01a still says `view` |
| G-21 | Sales / Purchases nav groups | — | None. Invoices, receipts, bills and payments sit under core **Finance** (`/finance/...`, QA-7) |
| G-19 | Token names used by screen specs | uiux T1–T15 | All provided as `--erp-*` aliases in tokens.scss; see `token-mapping.md` |
