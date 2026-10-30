---
id: TASK-112
title: Inputs labelled only by a placeholder need an accessible name
priority: P2
status: done
schema: no
depends_on: [TASK-110]
area: frontend-a11y
audit_ref: Claude analysis, UI/UX and accessibility
commit: 51b7197
---

## Problem
42 `<input>` and `<textarea>` elements outside a labelled form item have only a `placeholder`: line-item description/qty fields in bills, invoices, journals, purchase and sales documents, the POS barcode/search and customer fields, the ledger-report source filter, bank memo, cheque reasons, account security code/password, attachments category, and the shared table's column filter (`shared/paged-table.component.ts:174`). A placeholder disappears when typing and is not a reliable name.

## Impact
Screen-reader users hear an unnamed edit field in the most-used forms; voice-control users cannot say its name.

## Change wanted
Add `aria-label` (or `[attr.aria-label]` when the placeholder is bound) with the placeholder text to each; extend `scripts/a11y-lint.mjs` to fail on an `input`/`textarea` with only a placeholder outside an `nz-form-item`. Selects, date pickers and number inputs (about 56) are not covered here and need a follow-up because their accessible name has to be set on the inner control.

## Acceptance criteria
- [x] `npm run lint:a11y` passes with the new rule (verified to fail when a label is removed).
- [x] `ng build` and `ng test` pass.

## Tests required
The lint script (CI step).

## Do not touch
Placeholders and layout.

## What I did (Claude, on user request)
Script-applied labels to 42 inputs and textareas plus the lint rule.

## Review (Claude)
Follow-up needed: nz-select, nz-date-picker, nz-input-number controls with only a placeholder.
