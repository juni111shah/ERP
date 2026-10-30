---
id: TASK-111
title: Clickable cards, chips and rows must work from the keyboard
priority: P1
status: done
schema: no
depends_on: [TASK-110]
area: frontend-a11y
audit_ref: Claude analysis, UI/UX and accessibility
commit: PENDING
---

## Problem
Ten `<div>`, `<span>` and `<tr>` elements act as buttons through `(click)` only: notification items (`layout/notification-drawer.component.ts:297`), pipeline stage chips (`modules/realestate/leads.page.ts:81`), bank account cards (`pages/admin/bank.page.ts:107`), custom-field rows, party-detail rows, POS tiles, and every clickable row of the shared table (`shared/paged-table.component.ts:200`). None has `tabindex`, a role or key handling.

## Impact
Keyboard and switch users cannot open a notification, pick a bank account, filter by stage, or open a row in any list page.

## Change wanted
- New `shared/keyboard-click.directive.ts` (`erpKeyboardClick`): focusable, `role="button"` (clearable for table rows), Enter and Space trigger the click, keys typed in nested controls are ignored, and it can be bound to a condition (table rows only when `clickable()`).
- Apply it to the ten elements. Extend `scripts/a11y-lint.mjs` to fail on a `(click)` on a div, span, tr, li or td without it (menu items and `stopPropagation` wrappers excepted).

## Acceptance criteria
- [x] The ten elements are focusable and activate with Enter and Space.
- [x] `npm run lint:a11y` passes; `ng build` and `ng test` pass.

## Tests required
`shared/keyboard-click.directive.spec.ts`: focusable and role, Enter and Space click, nested input ignored, disabled state, role cleared for rows.

## Do not touch
Click behaviour and styling.

## What I did (Claude, on user request)
Directive, spec, applied to ten elements, lint rule.

## Review (Claude)
