---
id: TASK-110
title: Icon-only buttons need an accessible name
priority: P2
status: done
schema: no
depends_on: []
area: frontend-a11y
audit_ref: Claude analysis, UI/UX and accessibility
commit: PENDING
---

## Problem
20 icon-only `<button>` elements have no text and no `aria-label`: delete/remove-row buttons (bills, invoices, journals, budgets, reviews, inspections, acquisitions, van trips, BOM components, attachments), the row actions in projects, engagements, milestone billing and retainers (these only have a tooltip or `title`, which is not reliably announced), and the collapsed sidebar search button (`layout/shell.component.ts` ~line 1032).

## Impact
Screen-reader and voice-control users hear "button" with no purpose, so they cannot remove a line, edit or view a row, or open the menu search.

## Change wanted
- Add `aria-label` to every icon-only button found (text taken from its tooltip or action). The sidebar button uses the translated "Search menu...".
- Add `frontend/scripts/a11y-lint.mjs` (`npm run lint:a11y`) that fails on an icon-only button with no `aria-label`/`title`, and run it in CI.

## Acceptance criteria
- [x] `npm run lint:a11y` reports no icon-only button without an accessible name.
- [x] `ng build` and `ng test` pass.

## Tests required
The lint script is the regression test (CI step).

## Do not touch
Button styling and behaviour.

## What I did (Claude, on user request)
Added aria-labels to all 20 buttons, the lint script and a CI step.

## Review (Claude)
