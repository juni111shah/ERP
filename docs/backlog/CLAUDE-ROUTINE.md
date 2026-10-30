# Claude routine (run by the scheduled task `erp-backlog-review`)

Working directory: `D:\ERP\ERP`, branch `antigravity/dashboard-and-modules`. Claude edits only `docs/backlog/**` and `docs/AUDIT-*.md`. Claude never edits application code, never pushes, never switches branch, never merges.

## 1. Safety check
- `git status -sb`. If there are uncommitted changes outside `docs/backlog`, leave them alone (Antigravity may be mid-task). Never stash, reset, checkout or discard.
- `git log --oneline -15` to see recent commits.

## 2. Review every card with `status: ready-for-review`
For each card:
1. Read the card and its `commit:` hash (if missing, find it with `git log --grep "TASK-NNN" --oneline`).
2. `git show <hash> --stat` and read the full diff. Confirm the commit contains only this card's changes.
3. Check every acceptance criterion against the code and the tests added. Check "Do not touch".
4. Verify in a clean copy (do not build in the working tree, a running API may lock DLLs): create a fresh directory under the scratchpad, `git archive <hash> backend frontend/src frontend/package.json | tar -x -C <dir>` (or `HEAD` if later commits exist), then run `dotnet build backend/ERP.sln` and `dotnet test backend/ERP.sln --no-build`. If frontend files changed, also run the frontend build (`npm ci` may be needed; reuse `D:\ERP\ERP\frontend\node_modules` by running `npx ng build` in a copy that links it, or run `npx ng build` in the working tree only if no one is editing it).
5. Decide:
   - Everything met, build 0 errors, tests pass -> set `status: done` and write a short "Review (Claude)": what was verified, command results.
   - Otherwise -> set `status: changes-requested` and list each problem as a checkbox with file/line evidence and the exact fix wanted.
6. Commit only the card file(s): `git add docs/backlog && git commit -m "backlog: review TASK-NNN <done|changes-requested>" -- docs/backlog`.

## 3. Detect stuck work
- A card `in-progress` for more than 6 hours (see file modification time and git log) -> append a note "possible stall" to its Review section. `blocked` cards: read the reason; if the card was unclear, rewrite it and set it `open`.

## 4. Refill the queue
- Count cards with `status: open`. If fewer than 5, analyse the next area from the rotation in `README.md` (position in `STATE.md`) at HEAD: read the code, run safe checks, find defects, missing features and improvements with file and line evidence, and write new cards using `_TEMPLATE.md` numbered after the highest id (small, one concern each, with acceptance criteria and required tests). Do not duplicate existing cards. Mark `schema: yes` if a migration is needed. Add at most 8 cards per run.

## 5. Update the board
- Rewrite `docs/backlog/STATE.md` (counts per status, list of cards with status, last run time, rotation position, any failures) and commit with `git commit -m "backlog: update board" -- docs/backlog`.

If something unexpected happens (merge in progress, rebase, conflicting file), do nothing destructive: record it in `STATE.md` under "Needs human" and stop.
