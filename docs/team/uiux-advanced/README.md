# uiux-advanced — design system & advanced UX (Sprint 0)

Owner: Advanced UI/UX expert · For: Muhammad's general-purpose ERP (shared core and pluggable industry modules) · Angular 17+ (standalone, signals) + ng-zorro-antd 17–19.
Screen layouts are owned by the UI/UX expert in `../uiux/`. This folder owns the **theme, tokens and the advanced experience specs**. Names are aligned with `uiux/00-shared-conventions.md`, `uiux/07-app-shell.md` and `uiux/custom-fields.md`.

**Industry-neutral by rule.** Tokens, shell, dashboard and field system contain no business-type naming. Real estate appears only as a clearly labelled **worked example**. Retail is the second example and shows that the same contracts generalise.

## Files

| File | What it is |
|---|---|
| [`design-system.md`](design-system.md) | Principles; token architecture; colour (primitive and semantic layers, light and dark, a computed WCAG contrast table); typography with an Arabic/Urdu stack; spacing, radius, elevation and motion; multi-tenant branding with contrast-safe derivation; RTL rules; data density; the token → ng-zorro mapping; decisions, gaps and conflicts |
| [`theme/tokens.scss`](theme/tokens.scss) | Source of truth: SCSS maps, helper functions and mixins, and CSS custom properties for `:root`, `[data-theme=dark]`, `[data-theme=system]`, `[data-density=compact]`, `[dir=rtl]`, `:lang(ar/ur/fa)` and reduced motion; the `--ant-*` bridge; ng-zorro runtime patches |
| [`theme/ng-zorro-overrides.less`](theme/ng-zorro-overrides.less) | ng-zorro LESS overrides on top of `ng-zorro-antd.variable.less`. Surfaces, text, borders, radius, font, shadows and motion all point at `var(--erp-*)` |
| [`theme/var-safe-color-functions.js`](theme/var-safe-color-functions.js) | A small Less plugin. It lets ng-zorro's `fade()/tint()/darken()` accept CSS vars by emitting `color-mix()`. Because of it, one stylesheet serves every theme and brand |
| [`theme/tenant-theme.service.ts`](theme/tenant-theme.service.ts) | `TenantThemeService`: applies workspace branding (colour, logo, radius, font), theme, density, motion, language and direction at runtime, plus `deriveBrand()`, the OKLCH-based, AA-enforcing brand scale generator |
| [`theme/_dynamic-fields.scss`](theme/_dynamic-fields.scss) | Styles for custom fields: sections, the 1/2/3-column responsive field grid, read/edit values, the admin-only custom-field marker, and the field builder (palette, canvas, properties, CDK drag states) |
| [`theme/styles.scss`](theme/styles.scss) | Global SCSS entry (`@use 'tokens'; @use 'dynamic-fields';`) |
| [`token-mapping.md`](token-mapping.md) | Alignment with the uiux screen specs: T1–T15 token → `--erp-*` alias table, results for every contrast pair in uiux/10 §4 (light, dark, navy nav; all AA), tone/option palette values, suggested renames, settled choices |
| [`specs/app-shell.md`](specs/app-shell.md) | **Module manifest contract (accepted standard)** (nav, permissions, dashboard, alerts, terminology, status tones, search, commands, settings, customizable resources, accent); collapsible module-driven sidebar; header (entity switcher, Ctrl+K command palette, notifications, dark mode); breadcrumbs; breakpoints; Angular component tree; two example manifests |
| [`specs/dashboard.md`](specs/dashboard.md) | Widget-grid dashboard: KPI/widget registration interfaces, data flow, a 12-column grid per breakpoint, anatomy (KPI card, status panel, alerts list, chart card via ngx-echarts, hero), loading/empty/error/stale states, personalisation, real-estate and retail example layouts |
| [`specs/micro-interactions.md`](specs/micro-interactions.md) | Hover, focus, press, skeletons, toasts, optimistic updates, page/panel transitions, drag and drop, real-time cues, all with exact token durations and easings plus reduced-motion fallbacks |
| [`specs/accessibility.md`](specs/accessibility.md) | WCAG 2.2 AA checklist applied to this app; keyboard maps (shell, sidebar, tables, palette, dashboard); focus management; ARIA for sidebar, tables and charts; contrast; RTL; screen-reader test plan |
| [`specs/dynamic-fields.md`](specs/dynamic-fields.md) | User-defined fields (Odoo Studio pattern): TypeScript metadata contract, 22 types → ng-zorro edit/read/list rendering (v1 = 15 BA types, 7 in sprint 3, hidden through the capabilities API), the tone-based option colour palette, form sections/tabs/grid, the custom vs built-in marker, the field builder (palette, live canvas, properties panel, keyboard-accessible drag, undo, draft/publish, name validation, states), micro-interactions and a11y, and a backend gap list |

## Integration (for the repo owner)

```jsonc
// angular.json → projects.app.architect.build.options
"styles": ["src/theme/ng-zorro-overrides.less", "src/theme/styles.scss"],
"stylePreprocessorOptions": { "includePaths": ["src/theme"] }
```
* Copy `theme/` to `src/theme/`. The LESS file imports `../../node_modules/ng-zorro-antd/ng-zorro-antd.variable.less`, so adjust the relative path if `src/theme` sits at a different depth.
* Root template: `<div [dir]="theme.dir()"> … </div>` (CDK `BidiModule`). After the workspace settings load, call `theme.applyBranding(settings.branding)`.
* Don't add `ng-zorro-antd.dark.css`. Dark mode is `data-theme="dark"` on `<html>`.

## Verification done (22 Oct 2026)

* `sass 1.106.1`: `theme/styles.scss` and `theme/tokens.scss` compile with **no errors or warnings**. This was re-run after the T1–T15 alias, tone, navy-nav and density-header changes.
* `lessc 4.x --js` against **ng-zorro-antd 19.3.1**: `theme/ng-zorro-overrides.less` compiles (~758 KB CSS, ~880 `var(--erp-*)` references, no invalid arithmetic on vars).
* `tsc 5.5 --strict` with Angular 19 typings: `tenant-theme.service.ts` type-checks. The TS contracts in `specs/app-shell.md`, `specs/dashboard.md` and `specs/dynamic-fields.md`, including both example manifests, type-check together.
* `deriveBrand()` was run on 7 sample brand colours. Every output meets 4.5:1 (table in design-system.md §7).
* Theme files scanned with `sqa/core-vocabulary-denylist.txt`. The only hits are CSS/technical words ("custom properties", "unit()"), with no industry terms.
