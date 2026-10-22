# Micro-interactions

Owner: Advanced UI/UX · Every value below is a token from `theme/tokens.scss` (§ Motion). Under `prefers-reduced-motion: reduce` or `[data-motion=reduced]` all `--erp-duration-*` become **0 ms**; the "Reduced" column says what remains.

| Token | ms | | Easing token | curve |
|---|---|---|---|---|
| `fast` | 100 | | `standard` | cubic-bezier(0.2, 0, 0, 1) |
| `quick` | 150 | | `decelerate` (enter) | cubic-bezier(0, 0, 0, 1) |
| `base` | 200 | | `accelerate` (exit) | cubic-bezier(0.3, 0, 1, 1) |
| `moderate` | 250 | | `emphasized` | cubic-bezier(0.05, 0.7, 0.1, 1) |
| `slow` | 300 | | `linear` | linear |
| `slower` | 400 | | | |
| `skeleton` | 1400 | | | |

Rules of thumb: only animate `opacity`, `transform`, `background-color`, `border-color`, `box-shadow`, `outline-color`, `grid-template-rows` (cheap, no layout thrash). Exit = ~70 % of enter. Never delay user input; never animate more than one large region at a time. Animate with CSS transitions or Angular's native `animate.enter`/`animate.leave` (v20+) / `@angular/animations` (v17–19) reading the same CSS vars.

---

## 1. Hover

| Element | Change | Duration / easing | Reduced |
|---|---|---|---|
| Buttons (primary) | bg `primary` → `primary-hover` | `fast` / `standard` | instant colour change |
| Buttons (default/text) | bg → `bg-hover`, border → `border-strong` | `fast` / `standard` | instant |
| Table row | bg → `bg-hover`; row action icons fade from 0.6 → 1 opacity | `fast` / `standard` | instant |
| Nav item | bg → `nav-item-hover-bg`, icon → `nav-text` | `fast` / `standard` | instant |
| Clickable card (KPI, widget) | `shadow-1 → shadow-2`, border → `border-default`, translateY(−1px) | `quick` / `standard` | shadow only, no translate |
| Link | underline appears (text-decoration-color from transparent) | `fast` | instant |
| Tooltip | appears after 300 ms hover-intent (500 ms on dense tables), fade+scale 0.96→1 | `quick` / `decelerate`; exit `fast` / `accelerate` | fade only |
| Chart bar | others dim to 0.45 opacity, tooltip follows | `fast` | instant |

Hover is never the only way to reveal something: anything shown on hover is also shown on focus (`:focus-within`) and is available on touch (tap) — WCAG 1.4.13 (dismissible with Esc, hoverable, persistent).

## 2. Focus

| | |
|---|---|
| Ring | `outline: 2px solid var(--erp-color-focus-ring); outline-offset: 2px` (inputs use the box-shadow ring `--erp-focus-ring`: 2 px surface gap + 2 px ring) |
| Timing | ring appears instantly (no transition on outline width — users must see it immediately); colour transitions `quick` only when moving between states (error → valid) |
| `:focus-visible` only | mouse clicks don't draw rings on buttons; inputs always show focus |
| Error state | ring + border `error-solid`; error message enters with opacity 0→1 + translateY(−2px→0) `quick` / `decelerate` |
| Programmatic focus (after route change, dialog open) | no ring flash on headings (`tabindex=-1` + `:focus:not(:focus-visible)` outline none) |

## 3. Press / active

| Element | Change | Timing |
|---|---|---|
| Buttons | bg → `primary-active` / `bg-pressed`, `transform: scale(0.98)` | press `fast` / `standard`, release `quick` / `decelerate` |
| Icon buttons | bg `bg-pressed` circle | `fast` |
| Switch | thumb slides; track colour change | `base` / `standard` |
| Checkbox | check mark draws (stroke-dashoffset) | `quick` / `decelerate` |
| Toggle button / segmented | indicator slides to new option | `base` / `standard`; reduced: jump |
| Reduced | no scale; colour only | |

Buttons that trigger async work switch to `nzLoading` (spinner replaces icon, width locked to avoid jump) after 150 ms; disabled while pending (prevents double submit, uiux/00 §2).

## 4. Skeletons & loading

* **Show threshold**: skeleton/spinner only if work exceeds **150 ms**; once shown, keep for **≥ 300 ms** to avoid flicker.
* **Shimmer**: linear-gradient `skeleton-base → skeleton-shine → skeleton-base`, background-position animation `duration-skeleton` (1400 ms) `linear` infinite, moving in the reading direction (flipped in RTL via `--erp-direction-sign`). Reduced: static `skeleton-base` blocks.
* **Shape fidelity**: skeletons match final layout dimensions (KPI, list rows, table rows with column widths, chart bars) so content swaps in with **no layout shift (CLS < 0.05)**.
* **Content reveal**: skeleton → content cross-fade `quick` / `standard`; lists stagger first 6 items by 20 ms (max 120 ms total); reduced: instant, no stagger.
* **Route progress**: 2 px bar at top of content, `primary`, appears after 120 ms, indeterminate `linear`, completes with width→100 % `base` then fades `quick`.
* **Tables**: first load = skeleton rows (25); paging/sorting/filtering = keep rows + `nzLoading` overlay at 0.6 opacity (no skeleton).
* **KPI count-up**: on first data and on real-time change, values tween from previous → new over `slower` (400 ms) `emphasized`, using tabular numerals so width doesn't jitter; changed value gets a 1-cycle background flash `primary-subtle` fading over 1200 ms. Reduced: no tween, no flash; screen-reader announcement only for KPIs the user is focused on.

## 5. Toasts (`NzMessageService` / `NzNotificationService`)

| Type | Use | Placement | Duration | Motion |
|---|---|---|---|---|
| Message (success/info) | "Saved", "Showing Acme LLC" | top-centre | 3 s (success), 4 s (info); hover/focus pauses | enter translateY(−8px)+fade `base` / `emphasized`; exit fade `quick` / `accelerate` |
| Message (error) | non-field mutation errors | top-centre | 6 s, or sticky when it has an action | same |
| Notification | real-time events, approvals decided, background job done, critical alerts | top-end (top-left in RTL) | 6 s; critical = sticky until dismissed | slide from inline-end 16 px + fade `slow` / `emphasized`; exit `base` / `accelerate` |
| Undo toast | after destructive-but-reversible actions (archive, remove widget, delete draft line) | bottom-centre | 8 s with "Undo" button + countdown bar (`linear`) | slide up 8 px `base` / `emphasized` |

Max 3 visible; extras queue. Toasts never steal focus; they are announced via `aria-live` (`polite`; error/critical `assertive`). Actionable toasts are reachable via `F6` landmark cycle and their action is also available elsewhere (notification centre). Reduced: fade only, 0 ms durations → appear/disappear instantly.

## 6. Optimistic updates

Use for low-risk, reversible, single-record changes: toggles (active/inactive), favourites, mark notification read, reorder (drag), inline edit of non-financial fields, dashboard layout changes, column chooser.

1. Apply the change to the signal store immediately; the row/control shows a subtle **pending** affordance (inline 12 px spinner or 60 % opacity on the changed cell) only if the request exceeds 300 ms.
2. On success: commit; no toast for trivial actions (state is visible) — toast only when the effect is off-screen.
3. On failure: **roll back** with a `base` / `standard` transition back to the old value, field flashes `error-bg` (1 cycle, 600 ms), toast error "Couldn't update <field>. Changes were undone." with **Retry**.
4. Conflicts (412): roll back + concurrency alert (uiux/00 §2).

**Never optimistic**: postings, payments, approvals, anything that creates ledger entries, emails customers, or changes permissions — those use pessimistic flow (button loading → result), as the API is the source of truth (spec §10.5).

## 7. Page & panel transitions

| Transition | Motion | Timing |
|---|---|---|
| Route change (list ↔ detail, module ↔ module) | outgoing fades out; incoming fades in + translateY(4px→0) | out `fast` / `accelerate`, in `base` / `decelerate`; use the View Transitions API (`withViewTransitions()` in `provideRouter`) with these values; skip when navigating within the same page (tabs, query params) |
| Sidebar collapse/expand | width 248 ↔ 64; labels fade out in first `fast` | `moderate` / `standard` |
| Mobile nav drawer | slide from inline-start + mask fade | in `moderate` / `decelerate`; out `base` / `accelerate` |
| Detail drawer (right) | slide from inline-end 24 px + fade | in `slow` / `decelerate`; out `base` / `accelerate` |
| Modal | fade + scale 0.96 → 1; mask fade | in `slow` / `emphasized`; out `base` / `accelerate` |
| Command palette | fade + translateY(−8px) + scale 0.98 → 1 | in `base` / `emphasized`; out `fast` |
| Dropdown / select / popover | fade + scaleY 0.96 from origin | in `base` / `decelerate`; out `fast` / `accelerate` |
| Tabs | ink bar slides; content cross-fades | `base` / `standard`; content `quick` |
| Accordion / form section | `grid-template-rows: 0fr ↔ 1fr` + chevron rotate | `base` / `standard` |
| Theme switch | **no transition** (`.erp-theme-switching` disables transitions for one frame) — avoids a slow colour wash | 0 |
| Entity switch | content skeletons; toast | per §4/§5 |

Reduced motion: every transition becomes an instant swap (View Transitions skipped via `if (matchMedia('(prefers-reduced-motion: reduce)').matches) skip`).

## 8. Drag & drop (dashboard edit, field builder, option lists)

Pick-up: item lifts (`shadow-3`, scale 1.02) `quick` / `standard`; placeholder shows `primary-subtle` with dashed `primary-border`; siblings shift `base` / `standard`; drop: preview animates into place `base` / `standard` (CDK `.cdk-drag-animating`); invalid drop zone: cursor `not-allowed` + zone outline `error-solid`. Keyboard alternative is mandatory (see `dynamic-fields.md` §5.4). Reduced: no scale/shift animation, positions update instantly.

## 9. Real-time cues

* Bell ring: rotate ±12° ×2 over `slow` (300 ms) `standard`, once per batch (not per message). Reduced: badge count change only.
* New list item (alerts/notifications): slides in at top 8 px + fade `base` / `decelerate`, background `primary-subtle` fading out over 2 s.
* Presence/lock ("Being edited by Maya"): avatar chip fades in `quick`.

## 10. Implementation notes

```scss
@use 'tokens' as erp;
.erp-card--clickable {
  transition: box-shadow erp.duration('quick') erp.ease('standard'),
              transform  erp.duration('quick') erp.ease('standard'),
              border-color erp.duration('quick') erp.ease('standard');
  &:hover { box-shadow: erp.shadow(2); transform: translateY(-1px); }
  @media (prefers-reduced-motion: reduce) { &:hover { transform: none; } }
}
```

* JS-driven motion (count-up, ECharts) must read `matchMedia('(prefers-reduced-motion: reduce)')` **and** `document.documentElement.dataset.motion === 'reduced'` — expose as `MotionService.reduced` signal.
* Durations are never hard-coded in components; lint rule (stylelint `declaration-property-value-disallowed-list` for `transition`/`animation` with raw `ms`/`s` values) enforces tokens.
