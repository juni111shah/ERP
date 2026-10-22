# 10 · Responsive behaviour and accessibility

Spec: §3.6 and §4.32 (observed narrow layout: hamburger, search hidden, hero stacks, KPI 4→2, tables scroll horizontally with the action column pinned), §9 (Arabic RTL), §13 sprint 14 (accessibility pass). Target: **WCAG 2.2 AA**.

## 1. Responsive behaviour
Breakpoints are the ng-zorro defaults (xs < 576, sm ≥ 576, md ≥ 768, lg ≥ 992, xl ≥ 1200, xxl ≥ 1600). The theme owner may alias them as tokens.

| Element | ≥ xl | lg | md | < md (sm/xs) |
|---|---|---|---|---|
| Header | `--erp-header-height`: 56 px comfortable / 48 px compact, at every breakpoint | same | same | same |
| Workspace switcher (header, v1) | Name + ⇅▾ | Name (truncated) + ⇅▾ | Icon ⇅ only (tooltip shows name) | Moves into the user menu as "Switch workspace" |
| Sider | Expanded, group labels shown | Expanded (user can collapse) | Collapsed icons + tooltips | Hidden. ☰ opens `nz-drawer` left (full menu). |
| Global search | Inline input | Inline (narrower) | Icon → overlay | Icon → full-screen overlay (spec hides it; we keep it reachable) |
| Entity switcher | Select | Select | Select (compact) | Moves into the user menu as "Legal entity: …" (sub-menu) |
| Business-unit selector | Tree select | Tree select (compact) | Moves into a header "Context" popover together with the legal entity | Moves into the user menu as "Business unit: …" (sub-menu) |
| Hero banner | Text left, action right | same | Stacked | Stacked, action full width (spec) |
| KPI cards | 4–6 per row | 4 | 2 | 2 (xs: 1) (spec 4 → 2) |
| Filter bar | All quick filters inline | ≤ 3 inline + "+ Filter" | Search + **Filters (n)** button → right drawer | Same, search full width |
| Table | Full | Horizontal scroll | Scroll, code pinned left, ⋮ pinned right | Same. **Assumption:** no card-list transformation in v1, to keep one code path. Revisit for the mobile app. |
| Bulk bar | Inline above table | same | same | Fixed bottom sheet |
| Drawer form | `--erp-drawer-width-lg` / `-xl` | lg | 100 % | 100 %, sticky footer |
| Form columns | 2 | 2 | 2 | 1 |
| Wizard steps | Horizontal `nz-steps` | Horizontal | "Step 2 of 5" + `nz-progress` | same |
| Permission matrix | Full | Horizontal scroll, first column pinned | same | Per-module accordion: module → resource → action checkboxes listed vertically |
| Form layout designer | Palette + canvas + field drawer | Palette collapses to a button | Designer is read-only, with the notice "Use a larger screen to edit layout" | same |

Zoom and reflow: the content must reflow at 320 CSS px / 400 % zoom without loss (WCAG 1.4.10). Two-dimensional tables are exempt.

## 2. Keyboard and focus
* **Skip link** "Skip to main content" is the first focusable element, and it targets `main#content` (`tabindex="-1"`).
* **Route change:** focus moves to the page `h1` (`tabindex="-1"`), and the new `document.title` is announced via a polite live region ("Journals, page loaded").
* **Shortcuts:** `Ctrl/⌘ K` or `/` focuses search, `g` then `o` goes to Overview (**optional**), `?` opens the shortcuts list, and `Esc` closes overlays. Single-key shortcuts can be turned off in Preferences (WCAG 2.1.4).
* **Sider:** `nz-menu` keyboard (↑/↓, → / ← open or close submenus, Enter activates). The active item has `aria-current="page"`.
* **Tables:** sortable headers are buttons with `aria-sort`. Row click is also reachable by Enter on the focused row link (the code cell is a real `<a>`). The ⋮ actions are a `button` with `aria-haspopup="menu"` and a descriptive label.
* **Drawers and modals:** focus moves inside on open and is trapped (ng-zorro CDK focus trap). `Esc` closes, after the dirty guard. Focus returns to the trigger on close. Each has `aria-labelledby` pointing to its title.
* **Drag and drop** (column settings, options editor, layout designer, field list): every drag has a keyboard or button alternative (WCAG 2.5.7), and the result of each move is announced.
* **Permission matrix:** grid navigation with arrow keys and Space (04 §D).
* **Focus indicator:** always visible, using the ring `box-shadow: var(--erp-focus-ring)` (global `:focus-visible` rule); custom outlines use `--erp-focus-ring-color`. It must meet a 3:1 contrast against adjacent colours and must not be removed by component overrides.

## 3. ARIA and semantics
* Landmarks: `header` (banner), `nav aria-label="Main"` (sider), `nav aria-label="Breadcrumb"`, `main`, and `aside` for the notifications drawer.
* Headings: one `h1` per page, with sections and cards as `h2`/`h3`.
* Forms: each `label for` is linked to its control. Help and error text are linked by `aria-describedby`. Required fields have `aria-required`. Invalid fields have `aria-invalid`. The error summary has `role="alert"`.
* Status messages (toasts, record counts, "Saved", bulk results, wizard autosave) use `aria-live="polite"`. Errors that block the user use `role="alert"`.
* Status tags: the text is always present, so colour is never the only signal (WCAG 1.4.1). Tags with icons hide the icons from assistive tech (`aria-hidden`).
* Charts (dashboard): each chart needs a text summary and a "View as table" toggle.
* Icons-only buttons: `aria-label` plus `nz-tooltip` (the tooltip is not a substitute for the label).
* Loading: skeleton containers have `aria-busy="true"` and a visually hidden "Loading…" message.

## 4. Contrast and reliance on tokens
* No raw colours appear in these specs. All colour comes from `--erp-*` semantic tokens owned by the theme owner (`../uiux-advanced/theme/tokens.scss`); every name used here is mapped in [`../uiux-advanced/token-mapping.md`](../uiux-advanced/token-mapping.md) (§2 lists the measured contrast of each pair below).
* The theme must guarantee these **AA pairs** in light, dark, the navy nav variant (`<html data-nav-theme="dark">`) and compact density: `--erp-color-text-primary` / `--erp-color-bg-surface` ≥ 4.5:1; `--erp-color-text-muted` / surface ≥ 4.5:1 (spec muted `#8C8C8C` on white is ~3.4:1 and **fails**); `--erp-sider-text` and `--erp-sider-text-muted` / `--erp-sider-bg` ≥ 4.5:1; `--erp-sider-active-text` / `--erp-sider-active-bg` ≥ 4.5:1; `--erp-tone-<tone>-fg` / `--erp-tone-<tone>-bg` for every tone (status tags **and** custom-field option colours) ≥ 4.5:1, and `--erp-tone-<tone>-dot` / surface ≥ 3:1; `--erp-text-on-primary` / `--erp-primary` ≥ 4.5:1; `--erp-focus-ring-color` / adjacent colour ≥ 3:1; input borders / surface ≥ 3:1 (WCAG 1.4.11); `--erp-money-negative` / surface ≥ 4.5:1; and gold, when used as text, only via `--erp-color-accent-gold-text` (≥ 4.5:1; otherwise gold is decorative).
* Respect `prefers-reduced-motion` (no row highlight animation, no animated progress stripes) and `prefers-color-scheme` (initial theme).

## 5. RTL readiness (spec §9 Arabic)
Use logical CSS properties (`margin-inline-start`, `inset-inline-end`), `nzDirection` from `NzConfigService`, and mirrored icons where they show direction (chevrons, back arrows). Pinning uses "start/end" semantics. Numbers and codes stay LTR via `<bdi>`.
