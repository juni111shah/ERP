# Automated work queue (Claude analyses and reviews, Antigravity codes)

Everything happens on **one branch**: `antigravity/dashboard-and-modules`. Nothing is merged to `main` and nothing is pushed by this flow. Both agents work in the same checkout, so the rules below keep them from colliding.

## Roles

| Agent | Allowed to edit | Never edits |
|---|---|---|
| **Antigravity** (implementer) | application code, tests, and the `status` line of the card it is working on | other agents' review notes, `docs/AUDIT-*.md` |
| **Claude** (analyst, reviewer) | `docs/backlog/**` and `docs/AUDIT-*.md` only | application code, migrations, config |

## Card life cycle (the `status:` line in each card's front matter)

```
open -> in-progress -> ready-for-review -> done
                           |
                           v
                    changes-requested -> in-progress (loop)
blocked  = Antigravity could not continue; it writes the reason in the card
```

1. **Antigravity** takes the open card with the lowest priority number (P0 before P1 before P2), skipping cards whose `depends_on` are not `done`. It sets `status: in-progress`.
2. It implements only that card, adds the tests the card requires, runs `dotnet build backend/ERP.sln`, `dotnet test backend/ERP.sln` and (if the frontend changed) `cd frontend && npx ng build`.
3. It makes **one commit** whose message starts `TASK-NNN:` (for example `TASK-004: guard work-order completion`) and sets `status: ready-for-review`, with a short "What I did" note and the commit hash in the card.
4. **Claude** (scheduled run) finds every `ready-for-review` card, reviews `git show` of that commit against the acceptance criteria, rebuilds and retests in a clean copy, then either:
   - sets `status: done` and writes a "Review" note, or
   - sets `status: changes-requested` and lists the exact problems.
5. Antigravity picks up `changes-requested` cards before any new `open` card.
6. When fewer than 5 cards are `open`, Claude analyses the next area in the rotation below and writes new cards (bugs, missing features, improvements), numbered after the highest existing id.

## Rules for Antigravity

- One card, one commit, one concern. No bundled "add everything" commits.
- Follow `AGENTS.md` (clean architecture, permissions naming, standard response envelope).
- Every card ships with tests (integration tests for backend behaviour, spec for shared frontend pieces).
- A card marked `schema: yes` may add an EF migration **file** and its SQL script under `database/scripts` (also update the model snapshot). Never apply it to any database yourself, and never delete or edit an existing migration.
- Do not change accounting rules beyond what the card states. If the card is unclear or wrong, set `status: blocked` and explain; do not guess.
- Never commit secrets. Never touch `frontend/angular.json` or `package-lock.json` unless the card says so.
- Do not push. Do not switch branch. Do not merge.

## Rules for Claude

- Evidence for every card: file and line, impact, exact change wanted, acceptance criteria.
- A card must be small enough for one commit. Split large findings.
- Review only against the card's acceptance criteria plus build and tests. Report anything else as a new card.
- Never mark a card `done` without a clean build and passing tests for the touched projects.

## Area rotation for new analysis (Claude)

Order to cash (Sales, invoices, receipts) -> Procure to pay (Purchasing, bills, payments) -> POS and Retail -> Inventory and Manufacturing -> Projects and Services -> CRM and Distribution -> Finance reports and period close -> HR and payroll -> Real estate -> Security and tenancy -> UI/UX and accessibility -> Performance. Keep the current position in `docs/backlog/STATE.md`.

## Files

- `STATE.md` – board, last run times, rotation position (Claude keeps it current).
- `_TEMPLATE.md` – card template.
- `TASK-NNN-*.md` – one file per card.
- `ANTIGRAVITY-PROMPT.md` – paste into Antigravity once; it keeps looping on this queue.
- `CLAUDE-ROUTINE.md` – the routine Claude's scheduled task runs.
