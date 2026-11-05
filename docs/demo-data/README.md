# Demo data that needs the running API

`ProgramDemoTopUp.cs` (backend/src/ERP.Api) fills most tables when the API starts. These scripts fill the rest by calling the
real workflows, so the data is genuine (journals balance, balances move). Run them once, in order, against a dev API
(`http://localhost:5080`, seeded admin `admin@demo.local`) with Node 18 or later:

    node docs/demo-data/1-payroll-leave-approvals-settings.mjs
    node docs/demo-data/2-credit-notes-attachments-idempotency.mjs
    node docs/demo-data/3-users-2fa-reset-portal.mjs demo      # wait a minute between workspaces (login rate limit)
    node docs/demo-data/3-users-2fa-reset-portal.mjs demo-re

What they add: a posted November payroll (statutory and loan lines), approved leave, approval requests, settings, a
credit note applied to an invoice, an attachment, two demo users with legal-entity access and two-factor, a password
reset request and a portal sign-in code. Script 1 expects a draft November payroll run; if posting reports an unbalanced
journal, recalculate the draft first (`POST /m/hr/payroll/{id}/recalculate`).

`UserClaims` and `UserLogins` are ASP.NET Identity tables the app does not use; the demo rows were inserted with SQL.
