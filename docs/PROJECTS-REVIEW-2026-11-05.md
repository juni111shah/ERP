# Projects module review, 5 November 2026

Scope: the **Projects** module only (Projects, Tasks, Timesheets, Milestones, Project billing), on backend, web and mobile.
**Verified** = read in the code or counted in the repository. **Judgement** = what a project-based business would expect.
Not exercised in a browser; the findings come from the code and its tests.

## 1. What exists today (Verified)

| Part | What is there |
|---|---|
| Project | number, name, description, customer, manager (text), status (Planning, Active, On hold, Completed, Cancelled), billing type (Time and materials, Fixed fee, Non-billable), budget, start and end dates, custom fields |
| Task | number, title, description, status (To do, In progress, Review, Done), priority, assigned to (text), estimated and actual hours, due date, optional milestone |
| Milestone | title, due date, amount, status (Pending, Achieved, Invoiced), achieved date |
| Timesheet | date, worker (text), hours (max 24), hourly rate (typed per entry), billable flag, billed flag, invoice link |
| Billing | overview of unbilled work per project; "bill project" creates one customer invoice from chosen timesheets and achieved milestones |
| Rules | status transitions; completed or cancelled projects take no new work; billed timesheets and invoiced milestones are locked; a project with invoiced work cannot be deleted |
| Screens | web: list, detail (Overview, Tasks, Milestones, Time, Billing), tasks, timesheets, billing; mobile: 705 lines for projects, tasks, milestones, time |
| Tests | about 31 backend integration tests, 5 unit tests, 1 mobile test file, **0 web specs** |
| Dashboard | cards: active projects, open tasks, unbilled hours; chart: tasks by status |

## 2. Things that are wrong or risky

1. **Voiding a project invoice does not release the work.** Billing marks timesheets as billed and milestones as invoiced. Nothing
   listens for the invoice being voided (Sales has such a handler, Projects has none). After a void the hours and milestones stay
   "billed" forever, cannot be billed again, and the revenue is stranded. *Fix:* a handler that clears `IsBilled`/`InvoiceId` and
   moves milestones back to Achieved, plus a test.
2. **"Total timesheet cost" is really billable value.** It sums hours × the typed hourly (billing) rate. There is no cost rate, so the
   module cannot show cost, margin or profit, and comparing it with the budget is misleading. The label and the number disagree.
3. **People are free text.** Project manager, task "assigned to" and timesheet "worker" are typed names. They are not linked to users or
   HR employees, so there is no "my tasks", no notifications to the assignee, no permission by assignee, no cost rate from the
   employee, no leave check, and the same person can be spelled three ways.
4. **Customer name is taken from the client, not from the customer record.** The server stores the typed `customerName` next to the
   `customerId`; the two can disagree, and a renamed customer is not reflected.
5. **Edits on closed projects.** Creating a task or timesheet checks that the project is open, but *updating* a task, changing its
   status, deleting it, and updating or deleting an unbilled timesheet do not. Work can change on a completed project.
6. **Deleting a task that has time on it** is allowed; the timesheets keep a dangling task id.
7. **Task "actual hours" is typed by hand** and is not the sum of the timesheets logged against the task, so the two numbers drift.
8. **Completing a project checks nothing.** It does not warn about open tasks, pending milestones or unbilled work.
9. **Hours are limited to 24 per entry, not per person per day.** One person can log 24 hours twice on the same date. No check seen
   on future dates either.
10. **The web timesheet form hard-codes "Hourly Rate (QAR)"** although the workspace currency can differ (the earlier cleanup card
    TASK-012 aimed to remove these).
11. **The dashboard summary loads all projects, timesheets and milestones into memory** to add them up. Fine for a demo, slow with a few
    years of data. It should aggregate in the database.
12. **One default tax code is applied to every invoice line**, with no per-project tax and no tax-exempt option. The invoice due date is
    passed empty; confirm the customer's payment terms fill it in.
13. **Status model is thin.** Tasks have no Blocked or Cancelled state and Done can only go back to In progress; milestones have no
    Cancelled or Rejected; projects have no Closed/Archived after Completed.
14. **Web has no tests** for any Projects screen; mobile has one file.

## 3. Incomplete (exists but only partly)

- **Billing:** one invoice per call, one line per timesheet entry (a month of time makes a very long invoice). No grouping by person or
  task, no rate override, no discount, no retention, no billing a percentage of a fixed fee, no preview before creating, no re-bill after a
  void (see 2.1).
- **Budget:** a single amount. It is not compared with cost or billing anywhere (no budget vs actual, no overrun alert, no budget by
  phase or cost category).
- **Tasks:** list with a status flow, but the web has no board (Kanban) or calendar view; no subtasks, no dependencies, no comments or
  attachments (the generic attachment feature is not wired to tasks or projects in the screens).
- **Timesheets:** entry one row at a time; no weekly grid, no timer, no copy of last week, no submission or manager approval, no
  lock-after-approval.
- **Milestones:** amounts are not checked against the fixed fee or budget (the sum can exceed it); not linked to deliverables; no
  percentage of completion.
- **Reports:** only the dashboard cards and the billing overview. No project profitability, utilisation, budget vs actual, ageing of
  unbilled work, or timesheet reports.
- **Mobile:** can create and view, but no timer, no offline entry, no photo attachment, no push when assigned.

## 4. Missing features (not in the code)

**Planning**
- Gantt chart, dependencies, critical path, baselines; project and task templates; recurring tasks; phases or work breakdown structure;
  calendar of due dates; project health indicator (on track, at risk).

**People and resources**
- Link manager, assignees and workers to users or employees; resource allocation and capacity; utilisation report; skills; rate cards by
  role, person or customer; cost rate vs bill rate; holiday and leave awareness (HR has it).

**Time and expenses**
- Weekly timesheet submission and approval; timer; overtime and non-working-day rules; expense claims billed to the client with
  receipts; mileage; per-diem; supplier costs and purchase orders charged to a project.

**Cost, budget and profit**
- Actual cost posting to the ledger (labour cost, WIP), budget by category and phase, forecast to complete, earned value, margin per
  project, over-budget approval, work in progress and revenue recognition for fixed fee (percent complete).

**Billing**
- Progress and milestone-percentage invoices, retention, advance and deposit invoices, recurring invoices for retainers, invoice
  schedules, multi-currency billing, per-line tax, credit notes against project invoices, write-off of unbilled work, invoice
  preview and a client-facing statement.

**Collaboration and customers**
- Comments and mentions, activity feed, files per project and task, client portal for progress and approvals, change orders and
  approvals, risks and issues log, meeting notes, e-signature on sign-off.

**Administration**
- Project permission by manager or team, project categories and tags, archive, clone a project, import tasks from CSV, export, audit
  history on tasks, notifications and reminders for due and overdue work (the reminder system exists but has no Projects source).

**Integration**
- Link a project to a CRM opportunity or a sales order, to a real-estate property or work order, to HR leave and payroll costs, and to
  Services engagements (today the modules are separate silos).

## 5. Suggested order

**First (correctness, small)**
1. Release billed work when the invoice is voided, with tests (2.1).
2. Block edits, deletes and status changes on completed projects; block deleting tasks that have time (2.5, 2.6).
3. Take the customer name from the customer record; hard-coded currency label; per-day hour total (2.4, 2.10, 2.9).
4. Aggregate the dashboard in SQL (2.11).

**Second (the features people ask for first)**
5. Link people to users and employees; add cost rate; compute actual hours from timesheets; show budget vs actual and margin.
6. Timesheet weekly grid, submission and approval.
7. Board view for tasks; subtasks, comments and attachments; reminders for due tasks.
8. Billing: preview, grouping, rate override, percent of fixed fee, re-bill after void, per-line tax.

**Later**
9. Gantt and dependencies, resource planning and utilisation, expenses billed to clients, revenue recognition and WIP, client portal,
   templates, integration with CRM, Sales, Real Estate and HR.
10. Web specs and a Playwright smoke run for every Projects screen; mobile timer and offline entry.

---

## 6. What was built after this review (same day)

**Fixed:** voiding a project invoice releases its time, expenses, milestones and fixed-fee percentage; closed projects refuse edits; a task with time cannot be deleted;
customer name comes from the customer record; daily hour limit per person and no future dates; "cost" is now labour cost plus expenses, with `billableValue` separate;
actual hours follow the time logged; completing a project needs every task finished and nothing unbilled; milestones cannot pass the fixed fee; dashboard totals added up
in the database; web timesheet and task forms saved the previous day for users east of UTC and the timesheet form had a duplicated field and a fixed QAR label.

**Added:** people linked to accounts (assignee, worker, manager, owner); rate cards with bill and cost rates; timesheet approval and expense approval (settings); weekly grid and
timer (web and mobile); expenses with mark-up; write-off; invoice preview, grouping, discount and fixed-fee percentage billing; subtasks, dependencies, critical-path schedule and
Gantt; task board; blocked and cancelled statuses; comments; change orders; risks and issues; project team and allocations; templates, clone and archive; reports (profitability,
utilisation, budget and work in progress, unbilled ageing, hours); task import; reminders for due tasks and risks; health indicator.

**Also added later:** posting project cost to work in progress (`POST /billing/post-costs`, migration `ProjectsLedger`, script `0074`, new account "Project cost recovery"), receipts on expenses and a Files tab on the project plan (generic attachments).

**Not built:** releasing work in progress to cost of sales when the project is billed, client portal for projects, e-signature, (the generic attachment
store accepts them), links that create a project from a sales order or opportunity (only the source fields exist), per-project member visibility rules.

**Added after that:** work in progress is released to cost of sales when the work is billed (same transaction as the invoice); the setting `projects.restrictToMembers`
limits people without manage rights to projects they manage or belong to (opening a project, adding tasks, time and expenses); customer sign-off: staff ask from the plan page,
the customer signs with a typed name (or declines with a reason) in the portal's Projects tab, and signing a milestone's sign-off marks it achieved. Migration `ProjectsPortal`, script `0075`.

**Still not built:** a drawn or certified e-signature (the sign-off is a typed name with time and user, not a legally certified signature), a button that creates a project from a sales order
or opportunity (the source fields are accepted when creating a project), hiding other people's projects from the project list when members-only is on (the list itself is not filtered).

**Completed last:** the members-only setting now filters the project, task, time and expense lists too (not only opening a project); "Create project" on a sales order and on an opportunity (web and mobile) fills in the customer, budget and
source link; the customer's sign-off can be drawn on a pad as well as typed, and every answer keeps the time, the IP address and a SHA-256 evidence hash of what was signed (migration `ProjectsSignature`, script `0076`).
This is an audit trail, not a legally certified signature from a trust provider; for contracts that need one, connect a provider.

---

## 7. Checked in the running app, then rebuilt (same day)

Sections 1 to 6 came from reading the code. Opening the web screens showed that much of it did not work:

- **The screens compared statuses in the wrong letter case.** The API sends `active`, `todo`, `achieved`; the screens checked `Active`, `Todo`, `Achieved`. The board was empty, every milestone said "Billed", change orders had no buttons, Submit never appeared, and fixed-fee options were hidden. The specs used the same wrong values, so they passed.
- **The Risks page crashed** on load (`NzModalModule` was not imported).
- **Billing from the project page** created an invoice with no confirmation, for a different amount than the button showed.
- **Saving a project from the list or mobile wiped** the description, manager account, budget hours, tax code, source link and template flag.
- **Auto-numbering produced the text `{PREFIX}-{YYYY}-{SEQ:4}`** where the seeded pattern is in capitals, because the formatter only knew lowercase tokens.
- **A drawn signature could not be saved on SQL Server:** the column held 400 characters.

**Fixed and added:**

| Area | What changed |
|---|---|
| Web | One project page with tabs in the address, replacing Detail and Plan. Portfolio list with health, progress, budget used, views, cards and starting from a template. Task panel (edit, delete, subtasks, start date, labels, milestone, dependencies, discussion, files). Milestone actions and customer sign-off status. Invoice builder that picks lines and previews. Real timeline: date axis, today line, milestones, dependency arrows, drag to reschedule, baseline. Board with rows, search, quick add and only the moves the server allows. My Week that keeps entries intact, copies last week and sends the week for approval. Approvals inbox by person. Capacity heatmap. Reports with charts, totals, links and CSV. Filters that search the server, theme colours, Arabic text. |
| Rules | One calculation for cost and unbilled (`ProjectFigures`). Nobody approves their own time or expenses unless `projects.allowSelfApproval` is on. People change only their own time and expenses unless they can approve or manage. Approved entries cannot be deleted while approval is on. The members-only setting now covers milestones, risks, change orders, schedule, comments, allocations, billing, reports and direct links. Cost rates are hidden from people without rates, reports or billing rights. |
| Accounting | Voiding a project invoice reverses the release to cost of sales. Writing off work moves its posted cost out of work in progress. Entries already posted to the ledger cannot be edited or deleted, and a project with posted cost cannot be deleted. |
| New API | `GET /config`, `GET /projects/{id}/activity`, `POST /projects/{id}/baseline`, budget lines, `GET /billing/{id}/billable`, `GET /billing/{id}/invoices`, `POST /billing/write-off`, and `?live=true` on the work lists. A list of ids on billing now means exactly those lines; an empty list means none of that kind. |
| Settings | `projects.allowSelfApproval`, `projects.workWeekDays` (capacity and the My Week start day; no longer fixed to Monday to Friday). |
| Database | Migration `ProjectsWorkspace`, script `0077`: task baseline dates, table `prj_BudgetLines`, and `prj_Acceptances.SignatureImage` widened to `nvarchar(max)`. |

**Not done:**

- **Mobile:** no new screens. Flutter is not installed where this was built, so Dart could not be compiled or tested. Mobile gets the backend fixes only, and still lacks comments, files, schedule, team and sign-off status.
- **Billing that needs an accounting decision:** retention, advance and recurring invoices, multi-currency, per-line tax.
- **Links to other modules:** purchase orders and supplier bills charged to a project, payroll cost, HR leave and public holidays in capacity.
- **Also still missing:** skills, dated rate history, recurring tasks, a calendar view, mentions and alerts when a task is assigned, overtime rules, mileage, and comments or files for customers in the portal.
- **Scheduling** still counts calendar days. The work-week setting affects capacity, shading on the timeline and My Week, not task durations.
