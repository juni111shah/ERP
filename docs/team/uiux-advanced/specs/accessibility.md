# Accessibility — WCAG 2.2 AA applied to the ERP

Owner: Advanced UI/UX · Target: **WCAG 2.2 Level AA** for every core and module screen, in light/dark, comfortable/compact, LTR/RTL. Module manifests cannot opt out. SQA hooks: axe-core in the Playwright suite (`sqa/playwright`) + the manual matrix in §9.

---

## 1. Checklist (criterion → how this app meets it)

### Perceivable
| SC | Requirement here | Owner/where |
|---|---|---|
| 1.1.1 Non-text content | Icon-only buttons have `aria-label` (nav rail, row actions ✎ ⋮, bell, theme toggle); decorative icons `aria-hidden="true"`; workspace logo `alt="<Workspace> home"`; charts have a text summary + table (§6) | all components |
| 1.3.1 Info & relationships | Real `<table>` (nz-table) with `<th scope>`; forms: `<label for>` / `nzFor`; sections are `<fieldset><legend>` or `role="group" aria-labelledby`; nav = `<nav>` + lists; KPI = `<article aria-labelledby>`; headings hierarchy h1 page title → h2 card titles → h3 | |
| 1.3.2 Meaningful sequence | DOM order = visual order in both directions (grid `order` only for widgets in personalised layouts, and the DOM is re-ordered to match) | WidgetGrid |
| 1.3.4 Orientation | no orientation lock; builder shows a notice on phones but still works | |
| 1.3.5 Input purpose | `autocomplete` on login/profile (email, current-password, new-password, name, tel); custom Email/Phone fields set `autocomplete="email"/"tel"` only on the user's *own* profile forms | |
| 1.4.1 Use of colour | Status = tag text; deltas = arrow + sign; required = "(optional)" text convention (uiux/00) + `aria-required`; links in body text underlined; custom-field marker = icon + tooltip; chart series also differ by pattern/decal on request and direct labels | |
| 1.4.3 Contrast (min) | All text tokens ≥ 4.5:1 (table in `design-system.md` §3.4); tenant brand auto-corrected (`deriveBrand`) | tokens |
| 1.4.4 Resize text | 200 % zoom without loss; type uses px tokens on root, but layouts use rem-free fluid containers — verified at 200 % (spec §3.6 tests 110–250 %) | |
| 1.4.10 Reflow | 320 CSS px width: single column, sidebar → drawer, tables scroll horizontally **inside their card** with sticky first column and pinned action column (spec §3.6) — tables are an allowed 2-D exception | |
| 1.4.11 Non-text contrast | Input borders `border-control` 3.18:1 / 4.78:1; focus ring brand ≥ 3:1 against surface; status icons use `-solid` (≥ 3:1); chart bars ≥ 3:1 vs surface (viz-2 orange 3.20 ✓) | |
| 1.4.12 Text spacing | no fixed-height text containers; KPI/labels allow wrap; tested with the text-spacing bookmarklet | |
| 1.4.13 Content on hover/focus | tooltips/popovers: dismiss with Esc, hoverable, persist until moved away; never contain the only copy of essential info | |

### Operable
| SC | Requirement here |
|---|---|
| 2.1.1 Keyboard | Everything operable by keyboard: nav, palette, tables (row open = Enter), drag & drop has keyboard alternatives (dashboard edit, field builder, option reorder), charts (focusable data via "View as table") |
| 2.1.2 No keyboard trap | Overlays trap focus intentionally but always close with Esc and return focus; code/rich-text editors: `Esc` then `Tab` leaves the editor (documented in shortcut help) |
| 2.1.4 Character key shortcuts | single-key shortcuts (`/`, `g` sequences) only when focus is not in an editable field; Preferences → "Keyboard shortcuts: on/off/remap" |
| 2.2.1 Timing adjustable | session expiry warning 2 min before with "Stay signed in"; toasts with actions are sticky or their action is repeated in the notification centre; undo toasts 8 s, extendable by hover/focus |
| 2.2.2 Pause, stop, hide | skeleton shimmer stops at 0 ms durations under reduced motion; carousels not used; real-time KPI flashes ≤ 1 cycle |
| 2.3.1 Three flashes | no flashing > 3/s anywhere |
| 2.4.1 Bypass blocks | "Skip to main content" link (first tab stop, visible on focus); landmarks `banner`, `navigation` (Main), `main`, `complementary` (notification drawer), `search`; F6 landmark cycling |
| 2.4.2 Page titled | `document.title` = "<Page or record> · <Workspace> · <Product>" (uiux/07 §5) |
| 2.4.3 Focus order | logical; after route change focus moves to the page `<h1>` (tabindex −1); after closing a drawer/modal focus returns to the trigger; after deleting a row focus moves to the next row (or the table caption) |
| 2.4.4 Link purpose | "View all" links have `aria-label="View all alerts"`; KPI links read "Receivables, AED 155,363, view details" |
| 2.4.6 Headings & labels | every card has a visible heading; forms labels describe purpose |
| 2.4.7 Focus visible | 2 px ring (`--erp-color-focus-ring`), never removed |
| **2.4.11 Focus not obscured (min)** *(2.2)* | sticky header (56 px, 48 px compact) and sticky table headers/footers: `scroll-padding-block-start: calc(var(--erp-header-height) + 8px)` on the scroll container; toasts placed so they never cover the focused element (top-centre/end, max-width 420) ; command palette and drawers are modal so nothing behind receives focus |
| 2.5.1 Pointer gestures | no path/multi-point gestures; chart zoom has buttons |
| 2.5.2 Pointer cancellation | actions fire on `click` (up-event); drag can be cancelled with Esc |
| 2.5.3 Label in name | icon buttons' accessible names start with any visible text; "Save" button named "Save" |
| **2.5.7 Dragging movements** *(2.2)* | every drag has a single-pointer alternative: ⋮ "Move up / Move down / Move to…" for fields, sections, options, widgets; resize via menu |
| **2.5.8 Target size (min)** *(2.2)* | ≥ 24×24 px for all targets, including compact density (row icons 28×28 hit area, pagination 32), tag close × 24×24 hit area via padding; nav items 40/34 px tall |

### Understandable
| SC | Requirement here |
|---|---|
| 3.1.1 / 3.1.2 Language | `<html lang>` follows UI language; Arabic/Urdu data in an English UI wrapped with `lang="ar"`/`dir="auto"` (party names, addresses) |
| 3.2.1 / 3.2.2 On focus / input | changing the entity switcher **does** rescope the page — this is the expected, labelled behaviour and is announced ("Showing Acme LLC"); no other control navigates on change; filters apply on explicit action or debounced with an announced result count |
| 3.2.3 / 3.2.4 Consistent navigation & identification | shell is identical for all modules; same icons/labels for the same actions (manifest review + lint of icon names) |
| **3.2.6 Consistent help** *(2.2)* | Help (?) menu in the same header position on every page; contextual help links in page headers keep the same relative order |
| 3.3.1 Error identification | inline field errors (text + icon), error summary at top on submit with links (uiux/00 §2) |
| 3.3.2 Labels or instructions | labels always visible (no placeholder-only); help text under fields; format hints (e.g. date format, IBAN length) |
| 3.3.3 Error suggestion | messages say how to fix ("Enter 15 digits", "Choose a date after 01 Oct 2026") |
| 3.3.4 Error prevention (legal, financial, data) | posting/payments/approvals: review step or confirm modal summarising amounts; reversals instead of deletes (spec §10.5); typed confirmation for permanent deletes |
| **3.3.7 Redundant entry** *(2.2)* | multi-step flows (onboarding, import wizard, document lines) remember previous answers; "same as billing address" options; the field builder pre-fills from the previous field type |
| **3.3.8 Accessible authentication (min)** *(2.2)* | login allows paste + password managers (`autocomplete`), no cognitive puzzles; OTP fields accept paste and `autocomplete="one-time-code"`; magic link / passkeys on roadmap |

### Robust
| SC | Requirement here |
|---|---|
| 4.1.2 Name, role, value | custom widgets (KPI card, status panel, command palette, field builder canvas, toggles) expose correct roles/states (`aria-pressed`, `aria-expanded`, `aria-selected`, `aria-current`, `aria-busy`) |
| 4.1.3 Status messages | `aria-live="polite"` region in the shell (`RouteAnnouncer`), toasts, search result counts, save states ("Draft saved"), drag moves; `assertive` only for critical alerts and blocking errors |

---

## 2. Keyboard maps

### 2.1 Shell — see `app-shell.md` §5.2 (Ctrl/⌘K, Ctrl/⌘B, G-sequences, Shift+?, Esc, F6).

### 2.2 Sidebar (`nz-menu` inline)
| Key | Action |
|---|---|
| Tab | enters the menu at the active item (roving tabindex — one tab stop for the whole nav) |
| ↑ / ↓ | previous / next visible item (wraps) |
| → (← in RTL) | expand group / enter submenu |
| ← (→ in RTL) | collapse group / go to parent |
| Home / End | first / last item |
| Enter / Space | open link / toggle group |
| A–Z | type-ahead to item starting with letter |
| Collapsed rail | ↑/↓ moves between icons; → opens the flyout submenu, Esc closes it and returns focus |

### 2.3 Data tables (`DataTableComponent`)
| Key | Action |
|---|---|
| Tab | toolbar → table (one tab stop on the grid) → pagination |
| ↑ / ↓ | move row focus (row is `tabindex=-1` roving; `aria-rowindex`) |
| Enter | open the record (row click equivalent, spec §4) |
| Space | toggle row selection (when selectable) |
| Shift+↑/↓ | extend selection |
| → / ← | move between interactive cells (links, row actions) inside the row |
| Ctrl/⌘ A | select all on page (with announcement "25 rows selected") |
| Context menu key / Shift+F10 | open row ⋮ actions |
| Sort headers | buttons inside `<th>`; `aria-sort="ascending|descending|none"` |

We use the **"row as a link + inner tab stops"** pattern rather than a full `role="grid"` for read-only lists (simpler, better SR support); editable line grids (invoice lines) use `role="grid"` with cell navigation and `F2`/Enter to edit, Esc to cancel.

### 2.4 Command palette, dialogs, drawers
Palette: ↑/↓ options, Enter open, Ctrl/⌘+Enter new tab, Tab cycles filter chips, Esc close. Modal/drawer: focus first field (or the primary content heading), Tab trapped, Esc closes (with dirty guard), focus returns to trigger.

### 2.5 Dashboard & charts
Widgets in reading order; each widget is a region with a heading; chart: Tab to chart container (`role="img"` with `aria-label` summary) → "View as table" button → series legend buttons (`aria-pressed`). Edit mode: Space picks up a widget, arrows move, Ctrl+arrows resize, Space drops, Esc cancels (announced).

---

## 3. Focus management rules

1. **Route change**: move focus to `<h1>` of the new page; `RouteAnnouncer` announces the title politely. Exception: query-param-only navigations (filters, tabs) keep focus.
2. **Overlay open**: focus the first meaningful element (search input in palette, first field in forms, heading in read-only drawers). **Close**: return to the invoking element; if it no longer exists (deleted row), to the nearest logical successor.
3. **Async content**: never steal focus when data arrives; announce counts instead.
4. **Errors on submit**: focus the error summary (`role="alert"` not used — summary is `tabindex=-1` + `aria-labelledby`) whose links move focus to fields.
5. **Entity switch**: focus stays on the switcher; the page content updates under it with an announcement.
6. **Sidebar collapse**: focus stays on the toggle; `aria-expanded` updates.
7. **Focus not obscured** (2.4.11): `scroll-padding` for sticky header; tables with sticky header set `scroll-padding-top` = header row height.

---

## 4. ARIA patterns

### 4.1 Sidebar
```html
<nav aria-label="Main" id="app-sider">
  <h2 id="g-finance" class="erp-nav-group-label">Finance</h2>
  <ul aria-labelledby="g-finance">
    <li><a routerLink="/finance/journals" aria-current="page"><span nz-icon nzType="book" aria-hidden="true"></span>
        <span class="erp-nav-label">Journals</span></a></li>
    <li><a routerLink="/approvals"><span class="erp-nav-label">Approvals</span>
        <span class="erp-badge" aria-hidden="true">3</span><span class="erp-visually-hidden">, 3 pending</span></a></li>
  </ul>
</nav>
```
Decision: a plain list of links (site navigation is not an application menu); render `nz-menu` but override roles to the **disclosure navigation** pattern: `<nav aria-label="Main"><ul>` → group heading `<h2 class="visually-hidden-when-collapsed" id="g-finance">Finance</h2>` + `<ul aria-labelledby="g-finance">`; collapsible groups use a `<button aria-expanded aria-controls>`; links carry `aria-current="page"`; badges are inside the link text: `Approvals <span class="erp-visually-hidden">, 3 pending</span>`. Collapsed rail: links keep their accessible name (visible label hidden with `visually-hidden`, not `display:none`), tooltip is supplementary. Mobile drawer: `role="dialog" aria-modal="true" aria-label="Main navigation"`. Toggle: `aria-controls="app-sider" aria-expanded`.

(ng-zorro's `nz-menu` emits `role="menu"`/`menuitem`; we strip those via a small directive `erpNavA11y` because the ARIA menu pattern forces application-mode keyboard handling that SR users don't expect for site navigation — keyboard map §2.2 is still provided by the directive.)

### 4.2 Tables
`<table>` with `<caption class="visually-hidden">Invoices, 128 records, sorted by date descending</caption>`; `<th scope="col">`, sortable headers contain `<button>` + `aria-sort` on `<th>`; selection checkbox column labelled per row ("Select INV-2026-000028"); row actions `aria-label="Actions for INV-2026-000028"`; numeric cells end-aligned and with full value in text (no abbreviations without `<abbr title>`); empty state in a `<td colspan>` with `role="status"`; loading: `aria-busy="true"` on the table; pagination `nav aria-label="Pagination"`, current page `aria-current="page"`; horizontal scroll container `tabindex="0" role="region" aria-label="Invoices table, scrolls horizontally"` (so keyboard users can scroll).

### 4.3 Charts
* Container `role="img"` + `aria-label` with a generated summary: "Billed vs collected, last 12 months. Billed highest in March, AED 240,100; collected lowest in July, AED 61,000." (ECharts `aria.label.description` or our own summariser from the data).
* "View as table" toggle renders the same data as an accessible table (headers = categories × series).
* Series distinguishable without colour: direct labels on ≤ 3 series, legend + optional decal patterns (`aria.decal.show`), dashed/solid lines for line charts.
* Tooltips also open on keyboard focus of the data table cell (not required inside canvas).
* Reduced motion: `animation: false`.

### 4.4 KPI cards & status panels
`<article aria-labelledby="kpi-receivables-label">`; value has the full number in `aria-label` when abbreviated ("AED 155,363" vs "AED 155K"); delta read as "up 4.2 percent versus last month, unfavourable" (direction meaning spelled out). Status panel: state word is text, the dot is `aria-hidden`.

### 4.5 Live regions
One polite (`#erp-live-polite`) and one assertive (`#erp-live-assertive`) region in the shell, driven by `LiveAnnouncer` (CDK). Throttle: max one polite message per 1.5 s, merging ("3 new notifications").

---

## 5. Contrast & theme rules

* Text tokens verified in `design-system.md` §3.4 for light and dark; any new token must add a row to that table (PR checklist).
* Tenant brand: `deriveBrand()` guarantees ≥ 4.5:1 for button text and brand text; admin preview shows the report and warnings.
* Module accents: decorative only, enforced ≥ 3:1 vs surface.
* Disabled controls are exempt but must still be perceivable (`text-disabled` + not relying on opacity < 0.4).
* Focus ring ≥ 3:1 against both the control and the surface in both themes (brand-600 on white 4.72; brand-400 on dark 7.35).
* **Forced colors** (Windows High Contrast): `@media (forced-colors: active)` in tokens.scss keeps borders on cards/KPIs, uses `CanvasText` focus outlines, and status dots use `forced-color-adjust: none` + shapes.

---

## 6. RTL accessibility

* `dir` and `lang` on `<html>` change together; screen readers switch voice by `lang`.
* Arrow-key semantics follow visual direction (→ in RTL = "back"/collapse), implemented by reading `Directionality.value`.
* Mixed-direction strings (codes, amounts, emails, IBANs) isolated with `<bdi>` so SRs read them in order.
* Mirrored icons keep the same accessible names ("Next" stays "Next").
* Digits: if a user chooses Arabic-Indic digits, `aria-label`s still contain the same number (SRs handle both); inputs accept both digit sets.
* Test with NVDA + Arabic voice and VoiceOver (macOS) Arabic/Urdu.

---

## 7. Forms & dynamic (custom) fields

All rules in `uiux/00` §2 plus `dynamic-fields.md` §6: labels always visible, `aria-describedby` → help + error ids, `aria-invalid` on error, required via `aria-required` and the "(optional)" convention, groups (`fieldset/legend`) for radio/checkbox/multi-select, rich-text editor with labelled toolbar (`role="toolbar"`, roving tabindex), file upload with keyboard-operable button + drop zone (drop zone is enhancement only).

---

## 8. Motion & cognitive load

Reduced motion honoured (OS + in-app). No auto-advancing content. Plain-language microcopy (uiux owns copy). Session-time warnings. Destructive actions confirm with the object name. Undo where possible.

---

## 9. Testing plan

| Layer | Tooling | Gate |
|---|---|---|
| Unit | `deriveBrand` contrast tests (all tokens & sample brands ≥ thresholds); token table generated from SCSS and diffed | CI |
| Component | Storybook + `@storybook/addon-a11y` (axe) for every shared component in light/dark/compact/RTL | CI, zero violations of serious/critical |
| E2E | Playwright + `@axe-core/playwright` on every route (SQA suite), run for `en` LTR and `ar` RTL, light and dark | CI blocks on serious/critical |
| Keyboard | scripted Playwright journeys using only keyboard: login → switch entity → open record → edit → save; palette; drag alternatives | per release |
| Zoom/reflow | 320 px viewport + 200 %/400 % zoom screenshots (spec §3.6 states) | per release |
| Screen readers (manual) | **NVDA 2025+ / Firefox & Chrome (Windows)**, **JAWS 2025 / Chrome**, **VoiceOver / Safari (macOS)**, **VoiceOver / Safari (iOS)**, **TalkBack / Chrome (Android)** — scripts: sidebar navigation, data table reading & sorting, KPI and chart summaries, form errors, toasts, command palette, field builder keyboard reorder, RTL Arabic page | per release, findings logged with the SQA bug template |
| Contrast | automated (axe) + manual spot check of brand presets and charts in both themes | per release |
| Forced colours | Windows High Contrast (Aquatic, Desert) smoke test | per release |
| Users | 1 usability session per quarter with a screen-reader user and a keyboard-only user (finance persona) | roadmap |
