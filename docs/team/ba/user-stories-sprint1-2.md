# User Stories: Core foundations (sprints 1–9)

*The file name is kept for continuity. The plan now covers sprints 1–9 at a velocity of 40 points.*

| | |
|---|---|
| Product | The ERP (core, industry-agnostic) |
| Author | Business Analyst, sprint 0 |
| Date | 22 Oct 2026 |
| Grounding | Build spec §4.1, §4.4, §4.10, §4.17, §4.25, §4.26, §4.27, §4.30, §5.2, §7, §10.3–10.4, §11.4, §13; `core-domain.md` (rule IDs `BR-*`, `FIN-CHQ-*`, `CF-*`) |

## Conventions

- **ID**: `<EPIC>-nn`. **Priority**: MoSCoW (M = Must, S = Should, C = Could, W = Won't this release).
- **Points**: Fibonacci (1, 2, 3, 5, 8, 13) for relative effort, covering API, Angular UI (ng-zorro), tests and docs.
- **Velocity**: one full-time developer, 2-week sprints, **40 points per sprint** (Decision, 22 Oct 2026). No sprint is planned above 40. Re-calibrate after sprint 1.
- **Permission keys** use the format `<module>.<resource>.<action>` (`core-domain.md` Section 3.3).
- **Decision (accepted)** marks BA proposals Muhammad confirmed on 22 Oct 2026.
- Acceptance criteria are **Given / When / Then**. Each story also inherits the **Definition of Done** at the end.
- Rule references (e.g. `BR-SEC-03`) point to `core-domain.md`. `§x.y` points to the build spec.
- **[Assumption]** marks behaviour the spec doesn't describe.

## Personas

| Persona | Description |
|---|---|
| Platform admin | Operator of the ERP SaaS (Muhammad's team). Provisions tenants. |
| Org admin | Tenant's system owner (e.g. group CFO). Holds every permission (§2.3). |
| Finance manager / Accountant | Maintain finance master data. The Finance manager can approve. |
| Staff user | Any signed-in employee of the tenant. |
| Prospect | Person signing up for a trial. |

## Epic overview

| Epic | Stories | Points |
|---|---|---|
| AUTH: Authentication | 12 | 34 |
| TEN: Tenancy (workspaces) | 9 | 39 |
| USR: Users and roles | 8 | 30 |
| MD: Master data | 13 | 45 |
| LED: Ledger core | 10 | 40 |
| DOC: Finance documents (FIN-DOC: invoices, credit notes, bills, payments) | 12 | 48 |
| AR: Receipts and receivables | 8 | 29 |
| CUS: Customization (custom fields) | 13 | 54 |
| CHQ: Cheques (core finance) | 8 | 32 |
| **Total** | **93** | **351** |

## Sprint plan (velocity 40)

| Sprint | Goal | Stories (points) | Total |
|---|---|---|---|
| **1** | Sign in safely to an isolated workspace | AUTH-01 (5), AUTH-12 (3), AUTH-02 (5), AUTH-10 (2), TEN-01 (5), TEN-07 (5), USR-01 (5), USR-02 (2), USR-04 (3), USR-06 (5) | **40** |
| **2** | Master data essentials: legal entities, tax, chart of accounts, parties, numbering, fiscal years | TEN-05 (3), TEN-09 (5), AUTH-04 (3), AUTH-08 (1), USR-03 (3), MD-01 (2), MD-04 (5), MD-05 (5), MD-07 (5), MD-10 (5), MD-11 (3) | **40** |
| **3** | Remaining Musts + Should stretch: account self-service, scoping, period lock, business units, custom roles | AUTH-03 (1), AUTH-05 (3), AUTH-06 (2), AUTH-07 (2), AUTH-11 (2), TEN-04 (2), TEN-08 (3), USR-07 (5), USR-08 (2), MD-02 (3), MD-12 (3), MD-13 (3), TEN-06 (3), USR-05 (5) | **39** |
| **4** | Onboarding and master-data polish; custom fields start | MD-03 (2), MD-06 (3), MD-08 (3), MD-09 (3), AUTH-09 (5), TEN-03 (8), TEN-02 (5), CUS-01 (2), CUS-02 (8) | **39** |
| **5** | **Ledger core**: posting service, bank accounts, manual journals, trial balance, GL | LED-01 (8), LED-02 (3), LED-03 (5), LED-04 (3), LED-05 (5), LED-06 (3), LED-07 (2), LED-08 (3), LED-09 (3), LED-10 (5) | **40** |
| **6** | **Receipts & AR (part 1)** with sales invoices: issue, credit, void, receive and allocate | DOC-01 (5), DOC-02 (5), DOC-03 (5), DOC-04 (2), DOC-05 (5), DOC-06 (2), AR-01 (5), AR-02 (5), AR-03 (3), AR-04 (2) | **39** |
| **7** | **Receipts & AR (part 2)** and payables: statements, ageing, bills, payments | AR-05 (3), AR-06 (3), AR-07 (5), AR-08 (3), DOC-07 (5), DOC-08 (5), DOC-09 (5), DOC-10 (3), DOC-11 (3), DOC-12 (3) | **38** |
| **8** | Custom fields core + incoming cheques (unchanged from the previous sprint 5) | CUS-03 (3), CUS-04 (5), CUS-05 (5), CUS-07 (5), CUS-09 (3), CHQ-01 (5), CHQ-02 (3), CHQ-03 (5), CHQ-04 (5) | **39** |
| **9** | Custom fields complete + cheque replacement, reminders, outgoing cheques (unchanged from the previous sprint 6) | CUS-06 (5), CUS-08 (3), CUS-10 (5), CUS-11 (2), CUS-12 (5), CUS-13 (3), CHQ-05 (3), CHQ-06 (3), CHQ-07 (5), CHQ-08 (3) | **37** |
| | | **Grand total** | **351** |

Notes on the plan:

- **Sign-in** is email + password only, with a workspace picker after a correct password (AUTH-01, AUTH-12; Decision, 22 Oct 2026). AUTH-12 was added to sprint 1 (+3 points), so AUTH-08 (password policy) moved to sprint 2 and AUTH-03 (server-side sign-out) to sprint 3. Until then, sign-out clears the client session only.
- Sprints 1 and 2 contain only **Must** stories. Every Should/Could story is in sprint 3 or later.
- **Sprint 1 has only the legal entity created at signup/provisioning** (TEN-01). There's no legal-entity screen until TEN-05 in sprint 2, and USR-01 invitations default to "All legal entities" until then. Decision (accepted, 22 Oct 2026).
- **Dependency order** (Decision, 22 Oct 2026): Ledger core (sprint 5) → sales invoices + receipts (sprint 6) → statements, ageing and payables (sprint 7) → cheques (sprints 8–9). Receipts can't be allocated without issued invoices, so the invoice half of FIN-DOC sits in sprint 6 with AR-01…04. The Receipts & AR work therefore spans sprints 6–7, and the CUS/CHQ stories move to **sprints 8–9** (not 7–8) with their content unchanged. CHQ-03 needs AR-01/02, and CHQ-07 needs DOC-09.
- Only one Should story (LED-10, year-end close) is in sprint 5, as a stretch. If sprint 5 runs over, it moves to a later sprint without affecting the dependencies.
- Sprint numbers are for this backlog. The spec's other roadmap sprints (Leases, billing run, approvals dashboard, etc.) are interleaved by the Real Estate module plan.

## Epic AUTH: Authentication

### AUTH-01 Sign in with email and password
**As a** staff user **I want** to sign in with only my email and password **so that** I can reach my workspace's data without remembering an organisation code.
Priority **M** · Points **5** · Sprint **1** · Spec §4.1, §10.3, BR-TEN-05 (Decision, 22 Oct 2026)

- **Given** the login page, **then** it has only Email and Password fields (plus "Forgot password?"). There's **no organisation, tenant or workspace code** field, and the URL carries no org identifier.
- **Given** an active login account that's a member of exactly **one** active workspace, **when** they submit valid credentials, **then** that workspace is selected automatically and they're redirected to the Overview (or the return URL), and their last sign-in time is updated.
- **Given** a member of **two or more** active workspaces, **when** the password is correct (and MFA, if enabled, is passed), **then** the workspace picker opens (AUTH-12) instead of the Overview.
- **Given** a wrong password or unknown email, **when** they submit, **then** a red inline alert shows "Email or password is incorrect", the message doesn't reveal which one was wrong, and the response time is the same for both.
- **Given** any request before the password has been verified (wrong password, unknown email, locked account, rate-limited attempt), **then** the response contains **no workspace names, ids, count or logos**, and no endpoint lists workspaces for an email without a successful password check.
- **Given** a correct password but no active membership (all workspaces Disabled or Suspended), **then** access is refused with "Your account is not active. Contact your administrator."
- **Given** the password field, **when** the user clicks the eye icon, **then** the password is shown or hidden.

### AUTH-02 Stay signed in with short-lived access and a rotating refresh token
**As a** staff user **I want** my session to survive a page reload **so that** I don't lose my place (fixes spec gap §8 #1).
Priority **M** · Points **5** · Sprint **1** · Spec §7.3, §10.4

- **Given** a successful sign-in, **when** the API responds, **then** it returns a JWT access token (15 min lifetime, held in memory) with claims `sub` (login account), `org` (the selected workspace), `uid` (membership), `perms`, `ents`, `ver`, and sets a refresh token (14 days) in an HttpOnly, Secure, SameSite=Strict cookie.
- **Given** I'm on `/journals` and press F5, **when** the app starts, **then** it calls `/auth/refresh` silently before routing and I stay on `/journals` without a blank page.
- **Given** a refresh token is used, **when** a new pair is issued, **then** the old refresh token is revoked (rotation). **And** re-using a revoked token revokes the whole token family and forces a new sign-in.
- **Given** my roles changed (permission version bumped), **when** my next refresh happens, **then** the new token carries the new permissions (BR-SEC-06).
- **Given** an expired access token, **when** an API call returns 401, **then** the HTTP interceptor refreshes once and retries. If the refresh fails, I'm sent to login with a return URL.

### AUTH-03 Sign out
**As a** staff user **I want** to sign out **so that** nobody else can use my session.
Priority **M** · Points **1** · Sprint **3** · Spec §4.31

- **Given** I'm signed in, **when** I choose Avatar → Sign out, **then** the refresh token is revoked on the server, the cookie is cleared and I land on the login page.
- **Given** I signed out, **when** I press Back, **then** protected pages aren't shown.

### AUTH-04 Rate limiting and account lockout
**As an** org admin **I want** repeated failed sign-ins to be throttled and accounts to lock **so that** passwords can't be brute-forced.
Priority **M** · Points **3** · Sprint **2** · Spec §4.1, §7.3, §8 #14

- **Given** 5 failed attempts within 1 minute for the same IP + email, **when** a 6th attempt arrives, **then** the API returns 429 and the UI shows "Too many attempts. Try again in N seconds" with a live countdown.
- **Given** 10 consecutive failed attempts for a login account (configurable), **when** the limit is hit, **then** the login account is locked for 15 minutes across all its workspaces *[Assumption]*, an audit event "Account locked" is written, and the user gets an email.
- **Given** a locked login account, **when** an org admin of any workspace it belongs to chooses "Unlock" on Users, **then** the user can sign in again immediately, and that's audited in that workspace.
- **Given** the 429 or lockout responses, **then** they never reveal whether the email exists or which workspaces it belongs to.

### AUTH-05 Self-service password reset by email
**As a** staff user **I want** to reset a forgotten password by email **so that** I don't need to wait for an admin (fixes spec gap §8 #3).
Priority **M** · Points **3** · Sprint **3** · Spec §8 #3, §9, §10.4

- **Given** the login page, **when** I click "Forgot password?" and enter my email, **then** I always see "If an account exists, we've sent a link" (no account enumeration).
- **Given** a valid account, **when** the request is processed, **then** an email with a single-use link valid for 60 minutes *[Assumption]* is sent.
- **Given** a valid link, **when** I set a new password that meets the policy (AUTH-08), **then** the password changes for my login account (all my workspaces), all my refresh tokens in every workspace are revoked, I'm asked to sign in, and the event is audited.
- **Given** an expired or used link, **when** I open it, **then** I see "This link has expired" with an option to request a new one.

### AUTH-06 Admin issues a temporary password
**As an** org admin **I want** to issue a temporary password **so that** I can help a user who has no email access (keeps the behaviour from §4.1/§4.26).
Priority **M** · Points **2** · Sprint **3** · Spec §4.26

- **Given** `core.users.manage`, **when** I choose Reset password on a user, **then** a temporary password is generated and shown once (copy button), and the user is flagged "must change password".
- **Given** a user with a temporary password, **when** they sign in, **then** they must set a new password before they can open any other page.
- **Given** the reset, **then** the user's existing sessions are revoked and an audit event is written.
- **Given** the user's login account is also a member of another workspace, **then** Reset password isn't offered (an admin of one workspace can't change a password used elsewhere). The admin can send a reset-password email instead (AUTH-05).

### AUTH-07 Change my password
**As a** staff user **I want** to change my password **so that** I can keep my account secure.
Priority **M** · Points **2** · Sprint **3** · Spec §4.31

- **Given** Avatar → Change password, **when** I enter my current password, a new password and its confirmation that meet the policy, **then** the password changes, my other sessions are revoked, and I stay signed in on this device.
- **Given** a wrong current password, **then** the error "Current password is incorrect" appears on that field.

### AUTH-08 Password policy
**As an** org admin **I want** a password policy **so that** weak passwords are rejected.
Priority **M** · Points **1** · Sprint **2** · Spec §7.3

- **Given** any password set or change, **when** the password is shorter than 10 characters, lacks 3 of 4 character classes, or appears in a common-password list, **then** it's rejected with a clear message. *[Assumption: these are the policy values]*
- **Given** a new password, **then** it can't equal any of the last 5 passwords. *[Assumption]*
- Passwords are stored with ASP.NET Core Identity hashing (PBKDF2 or Argon2) and are never logged.

### AUTH-09 Optional multi-factor authentication (TOTP)
**As a** staff user **I want** to protect my account with an authenticator app **so that** a stolen password isn't enough.
Priority **S** · Points **5** · Sprint **4** · Spec §7.3, §9

- **Given** Profile → Security, **when** I enrol by scanning the QR code and entering a valid 6-digit code, **then** MFA is enabled and 10 one-time recovery codes are shown once.
- **Given** MFA is enabled, **when** I sign in with a correct password, **then** I'm asked for the code before any workspace is selected or listed. **And** 5 wrong codes lock the MFA step for 15 minutes.
- **Given** the org setting "Require MFA for admins and approvers" is on, **when** a user holding `core.users.manage`, `core.roles.manage`, `core.settings.manage` or `core.approvals.decide` in that workspace selects it without MFA, **then** they must enrol before continuing. MFA belongs to the login account, so enrolling once covers every workspace.
- **Given** "Remember this device for 30 days", **when** I sign in again from that browser, **then** the code isn't asked.
- **Given** an org admin, **when** they reset MFA for a user, **then** the user must enrol again, and that's audited.

### AUTH-10 Session profile endpoint (`/me`)
**As a** staff user **I want** the app to know my permissions, entities and settings when it loads **so that** menus and pages match what I'm allowed to do.
Priority **M** · Points **2** · Sprint **1** · Spec §12

- **Given** I'm signed in, **when** the app calls `GET /api/v1/me`, **then** it gets my name, email, roles, permission keys, allowed legal entities and business units, last selected entity, the current workspace and the list of my other active workspaces (for Switch workspace), org settings needed by the UI (currency, time zone, theme), and the custom-field schema version.
- **Given** I don't have `finance.accounts.read`, **then** the Finance menu items are hidden and a direct URL shows "You don't have access to this page" (403 page).

### AUTH-11 Authentication audit
**As an** org admin **I want** sign-in events in the audit log **so that** I can investigate suspicious access.
Priority **M** · Points **2** · Sprint **3** · Spec §7.3, BR-AUD-03

- **Given** any sign-in success, workspace selection or switch, failure, lockout, password reset, password change, MFA change or sign-out, **then** an audit event is written with user (or attempted email), time, IP and user agent. Events before a workspace is selected go to the platform security log and are copied to the audit log of every workspace the login account belongs to.
- **Given** Audit log, **when** I filter Record type = "Authentication", **then** I see these events.

### AUTH-12 Workspace picker and switching
**As a** user who belongs to several workspaces **I want** to choose a workspace after signing in and switch later **so that** I can work for each organisation with one login.
Priority **M** · Points **3** · Sprint **1** · BR-TEN-02, BR-TEN-05 (Decision, 22 Oct 2026)

- **Given** a correct password for a login account with 2+ active memberships, **then** the API returns only a short-lived **workspace-selection token** (5 minutes, single use, no access to any business endpoint) and the list of the user's active workspaces (display name, logo, last used). Workspaces where the user is Disabled or the org is Closed aren't listed.
- **Given** the picker, **when** I choose a workspace, **then** the access token and refresh cookie are issued for that workspace only (`org` claim), its "Require MFA" rule is applied (AUTH-09), and I land on its Overview. The last-used workspace is pre-selected next time.
- **Given** the selection token, **when** it's used to call any business endpoint or reused after selection, **then** the API returns 401.
- **Given** I'm signed in, **when** I choose Avatar → Switch workspace, **then** the picker opens without asking for the password again, the current workspace's refresh token is revoked, and new tokens are issued for the chosen workspace.
- **Given** a request that names a workspace I'm not a member of, **then** it's rejected with 403, and the attempt is audited.

---

## Epic TEN: Tenancy

### TEN-01 Platform admin provisions a tenant
**As a** platform admin **I want** to create a new organisation with its first org admin **so that** a customer can start using the ERP.
Priority **M** · Points **5** · Sprint **1** · Spec §10.3, `core-domain` Section 4.1 *[Assumption: provisioning flow]*

- **Given** the platform console, **when** I enter org display name, country, base currency, time zone, plan, primary industry and the admin's name and email, **then** the org is created with status Active, the industry template (Section 4.2) is applied, and an invitation is emailed to the admin.
- **Given** the admin's email already has a login account (member of another workspace), **then** no new password is created. The invitation adds a membership, and the admin picks the new workspace after signing in (AUTH-12).
- **Given** the org, **then** no sign-in key or org code is created or shown. Users reach it only through email + password and the workspace picker (BR-TEN-05).
- **Given** provisioning fails midway, **then** nothing is half-created (single transaction or compensating cleanup).

### TEN-02 Public self-service signup with email verification
**As a** prospect **I want** to sign up for a trial myself **so that** I can try the ERP without talking to sales.
Priority **C** · Points **5** · Sprint **4** · *[Assumption]*

- **Given** the public signup page, **when** I enter name, work email, company, country, phone, password and accept the terms, **then** a pending org is created and a verification email (24 h link) is sent. If the email already has a login account, signup asks for that account's password instead of creating a new one.
- **Given** I click the link, **then** my user is activated, the org becomes Trial (default 14 days, configurable), and the onboarding wizard opens.
- **Given** an unverified signup older than 7 days, **then** it's purged automatically.
- **Given** bot protection, **then** signup requires a captcha and is rate-limited per IP.

### TEN-03 Onboarding wizard
**As an** org admin **I want** a guided setup wizard **so that** my company is configured correctly before the first transaction.
Priority **S** · Points **8** · Sprint **4** · `core-domain` Section 4.1

- **Given** a new org, **when** the admin signs in the first time, **then** the wizard opens with the steps from `core-domain` Section 4.1 and a progress indicator.
- **Given** I complete a step and leave, **when** I come back, **then** I resume at the next incomplete step with my answers kept.
- **Given** the mandatory steps (profile, industry, localisation, first legal entity, chart of accounts) aren't complete, **when** any user tries to post a journal or issue a document, **then** it's blocked with "Finish company setup first".
- **Given** I finish, **then** the org is marked set up and a "Getting started" checklist appears on the dashboard.

### TEN-04 Maintain organisation profile and settings
**As an** org admin **I want** to edit the organisation profile and global rules **so that** every module follows them.
Priority **M** · Points **2** · Sprint **3** · Spec §4.25

- **Given** `core.settings.manage`, **when** I open Settings, **then** I can edit display name, group legal name, contact email and invoice footer (max 1000 chars, with a counter), and see plan, country/currency and time zone read-only.
- **Given** I change an approval threshold, **then** values ≥ 0 or exactly −1 are accepted, the hints "0 = always require approval, −1 = never" appear under the field, and the change is audited with before/after values.
- **Given** a user with only `core.settings.read`, **then** the page is read-only, and without it hidden.

### TEN-05 Create and maintain legal entities
**As an** org admin **I want** to register each legal entity with its tax and licence details **so that** each one keeps its own books and issues its own tax invoices.
Priority **M** · Points **3** · Sprint **2** · Spec §4.4, BR-ORG-*

- **Given** Legal entities → Add, **when** I enter a legal name and region (emirate: Abu Dhabi, Dubai, Sharjah, Ajman, Umm Al Quwain, Ras Al Khaimah, Fujairah for the UAE pack) and leave the code blank, **then** the entity is created with the next code `ENT-0001…`.
- **Given** I enter a TRN, **then** it must be exactly 15 digits. **And** if "Tax registered" is ticked, a TRN is required.
- **Given** a licence expiry date, **then** a licence-expiry alert is raised from 60 days before it.
- **Given** "Create an operating bank account" is ticked (default) with a bank name, **then** a bank account and its GL account are created. *(The bank account screen itself is delivered with the Ledger sprint. Only the hook is needed here.)*
- **Given** an entity has open documents, **when** I set it Inactive, **then** it's blocked with the list of blockers (BR-ORG-05).
- **Given** the entity list, **then** I see Code, Legal entity, Region, Trade licence + expiry, TRN, Books locked to and Status, with search, filter, CSV and 25 rows per page.

### TEN-06 Maintain business units
**As an** org admin **I want** to define business units under each legal entity **so that** I can scope operations, access and reporting by location.
Priority **S** · Points **3** · Sprint **3** · Decision (accepted): business unit is a reporting dimension, BR-ORG-01/03

- **Given** a legal entity, **when** I add a business unit with name, address and manager, **then** it gets the code `BU-0001` and is listed under that entity.
- **Given** an entity's first business unit, **then** it's automatically marked Head office. **And** exactly one head office per entity is enforced.
- **Given** a business unit used on documents or user scopes, **when** I try to delete it, **then** only deactivation is offered.
- **Given** a business unit is inactive, **then** it can't be chosen on new documents or user scopes.

### TEN-07 Tenant data isolation
**As an** org admin **I want** a guarantee that no other organisation can see my data **so that** I can trust the ERP with confidential finances.
Priority **M** · Points **5** · Sprint **1** · Spec §7.3, §10.3, BR-TEN-01…04

- **Given** two orgs A and B with data, **when** a user of A calls any list, detail, report, search or export endpoint, **then** no row of B is ever returned (automated integration test per endpoint group).
- **Given** a request body or query string containing an `orgId`, **then** it's ignored. The org always comes from the token.
- **Given** a user of A requests B's record by id, **then** the API returns 404 (not 403, so the record's existence isn't revealed).
- **Given** a unique code such as `ENT-0001`, **then** both orgs can use it (unique indexes include OrgId).

### TEN-08 Legal-entity switcher
**As a** staff user with access to several legal entities **I want** to switch the active entity in the header **so that** every list, KPI and report shows only that entity, or all of them.
Priority **M** · Points **3** · Sprint **3** · Spec §4.30, BR-ORG-08

- **Given** I have access to entities A and B, **when** I open the switcher, **then** I see "All legal entities", A and B, and nothing else.
- **Given** I select A, **then** every API call carries `X-Entity-Id: A`, lists show only A's records, and my choice persists for my next sign-in.
- **Given** I tamper with `X-Entity-Id` to an entity I can't access, **then** the API returns 403.
- **Given** I have access to only one entity, **then** the switcher shows that entity without the "All" option.

### TEN-09 Audit trail capture and audit log
**As an** org admin **I want** every create, update and delete recorded with before/after values **so that** I can see who changed what.
Priority **M** · Points **5** · Sprint **2** · Spec §4.27, §10.7, BR-AUD-*

- **Given** any insert, update or delete through the application, **then** an audit row is written in the same transaction with time (UTC), user (or "System"), IP, action, record type, record id, changed fields and before/after JSON.
- **Given** Audit log (`core.audit.read`), **when** I search by user or record and filter by record type and date range, **then** results show 50 per page with an expander for before/after values.
- **Given** any detail page, **then** its History tab shows that record's audit timeline.
- **Given** any user, including Org admin, **then** audit rows can't be edited or deleted through the UI or API.
- **Given** sensitive fields (Emirates ID, IBAN, salary), **then** their values are masked in the diff for users without the sensitive permission.

---

## Epic USR: Users and roles

### USR-01 Invite a user
**As an** org admin **I want** to invite a colleague by email with roles and entity access **so that** they can start working with the right rights.
Priority **M** · Points **5** · Sprint **1** · Spec §4.26

- **Given** `core.users.manage`, **when** I enter full name, email, at least one role (default Viewer) and legal entity access (All / Only selected), **then** the membership is created with status Invited and an email with a 7-day invitation link is sent. If the email already has a login account, the email says "You've been added to <workspace>" and no new password is asked for.
- **Given** the email already exists in my org, **then** I get "A user with this email already exists".
- **Given** the selected role, **then** its description is shown under the field (as in §4.26).
- **Given** an invitation that has expired or was lost, **when** I choose "Resend invitation", **then** a new link is sent and the old one stops working.
- **Given** the plan's user limit is reached, **then** inviting is blocked with an upgrade message. *[Assumption]*

### USR-02 Accept an invitation
**As an** invited user **I want** to accept the invitation and set my password **so that** I can sign in.
Priority **M** · Points **2** · Sprint **1**

- **Given** a valid invitation link and no existing login account, **when** I set a password that meets the policy, **then** my login account is created, my membership becomes Active, and I'm signed in to that workspace.
- **Given** a valid link and an existing login account, **when** I sign in with my existing password, **then** the membership becomes Active and the workspace appears in my picker.
- **Given** an expired link, **then** I see "Ask your administrator to resend the invitation".

### USR-03 Edit, deactivate and unlock users
**As an** org admin **I want** to change a user's name, roles, entity/business unit access and active flag **so that** access follows people's jobs.
Priority **M** · Points **3** · Sprint **2** · Spec §4.26, BR-SEC-05

- **Given** Edit user, **then** the email is read-only and the name, roles, access and Active flag can be changed.
- **Given** I deactivate a user, **then** their sessions are revoked immediately and they can't sign in. Their history and authored records stay intact.
- **Given** I try to remove the Org admin role from, or deactivate, the last active Org admin, **then** it's blocked with "At least one active Org admin is required".
- **Given** any change, **then** the user's permission version is bumped (forcing token refresh) and an audit event is written.

### USR-04 Built-in roles seeded from the permission matrix
**As an** org admin **I want** ready-made roles that match common jobs **so that** I don't have to build permissions from scratch.
Priority **M** · Points **3** · Sprint **1** · Spec §2.3, §7.2

- **Given** a new org, **then** the core roles (Org admin, Finance manager, Accountant, Approver, Viewer) and the industry template's roles (e.g. for real estate: Leasing manager, Facilities manager and Brokerage manager from the Real Estate module, plus HR manager from HR) are seeded with the permission keys of spec §7.2.
- **Given** the Roles tab, **then** each role shows its name, description and permission keys.
- **Given** a built-in role, **when** I try to edit or delete it, **then** only "Clone as custom role" is offered (BR-SEC-04).

### USR-05 Custom roles
**As an** org admin **I want** to build my own roles from permission keys **so that** access fits my organisation chart (spec gap §8 #7).
Priority **S** · Points **5** · Sprint **3** · Spec §9 "Custom roles"

- **Given** Roles → New role (or Clone), **when** I enter a name and description and tick permissions in a grid of module → resource rows × action columns (`<module>.<resource>.<action>`, e.g. `finance.invoices.issue`), **then** the role is saved and can be assigned.
- **Given** I tick any action on a resource (e.g. `finance.invoices.create`), **then** its `.read` key is ticked automatically and locked.
- **Given** a role is assigned to users, **when** I change its permissions, **then** all its users' permission versions are bumped and the change is audited with before/after keys.
- **Given** a role is assigned to users, **when** I try to delete it, **then** it's blocked until it's removed from all users.
- **Given** a role name that already exists, **then** saving is blocked.

### USR-06 Permission enforcement in the API and UI
**As an** org admin **I want** permissions enforced everywhere **so that** users can't do what their role doesn't allow.
Priority **M** · Points **5** · Sprint **1** · Spec §7.3, BR-SEC-01/07

- **Given** an endpoint marked `[HasPermission("finance.invoices.issue")]`, **when** a user without it calls it, **then** the API returns 403 ProblemDetails and nothing changes.
- **Given** the same user in the UI, **then** the related buttons, menu items and routes are hidden or disabled (`*hasPermission`, route guards).
- **Given** a command reaches the application layer by another path (e.g. a background job on a user's behalf), **then** the MediatR authorisation behaviour checks the permission again (defence in depth).
- **Given** a test suite, **then** every endpoint has a declared permission, and an automated test fails if an endpoint has none (except the public auth endpoints).

### USR-07 Entity- and business-unit-scoped access
**As an** org admin **I want** to limit a user to selected legal entities and business units **so that** staff only see their own company's or business unit's data.
Priority **M** (entity) / **S** (business unit) · Points **5** (3 entity + 2 business unit) · Sprint **3** (business unit part after TEN-06, same sprint) · Spec §2.2, §4.26, BR-SEC-03

- **Given** a user with access "Only selected entities: A", **when** they list any entity-scoped record, **then** only A's records appear, and A's alone in the switcher.
- **Given** that user requests a record of entity B by id, **then** the API returns 404.
- **Given** business unit access "Only selected business units: Marina office", **when** they list business-unit-tagged documents, **then** only that business unit's documents (and documents without a business unit, if "Include unassigned" is on) appear. *[Assumption]*
- **Given** shared masters (parties, accounts), **then** they stay visible to all users with the read permission. Only balances are scoped.

### USR-08 Users list
**As an** org admin **I want** a list of users with roles, access, status and last sign-in **so that** I can review access regularly.
Priority **M** · Points **2** · Sprint **3** · Spec §4.26

- **Given** Users & roles, **then** the table shows Name (+ "You" tag, email), Roles (tags), Legal entities, Last sign-in (org time zone), Status, and actions Edit, Reset password, Unlock.
- **Given** the search box and filters (role, status, entity), **then** the list narrows accordingly, and CSV export lists the same columns.

---

## Epic MD: Master data

### MD-01 Enable currencies
**As a** finance manager **I want** to enable the currencies we trade in **so that** they can be used on documents, bank accounts and rates.
Priority **M** · Points **2** · Sprint **2** · BR-FX-01, Decision (accepted): multi-currency in core

- **Given** a new org, **then** the base currency (e.g. AED) is enabled and can't be disabled.
- **Given** Currencies, **when** I enable USD from the ISO 4217 list, **then** it shows with symbol and decimal places (editable display symbol only).
- **Given** a currency used on any document or bank account, **when** I try to disable it, **then** it's blocked.

### MD-02 Maintain exchange rates
**As a** finance manager **I want** to record exchange rates by date **so that** foreign-currency amounts convert consistently.
Priority **M** · Points **3** · Sprint **3** · BR-FX-02/03

- **Given** Exchange rates, **when** I add a rate (from currency, to base currency, date, rate up to 8 decimals, type Spot/Closing/Average), **then** it's saved. A duplicate (same pair, date and type) is rejected.
- **Given** a CSV with columns date, from, to, rate, type, **when** I import it, **then** valid rows are added, invalid rows are listed with reasons, and nothing is half-imported (dry-run preview first).
- **Given** a rate is used by a posted document, **when** I edit or delete it, **then** it's blocked. Only a new rate for another date can be added.

### MD-03 Rate lookup and missing-rate handling
**As an** accountant **I want** the system to pick the correct rate automatically **so that** I don't enter it by hand.
Priority **S** · Points **2** · Sprint **4** · BR-FX-03

- **Given** a document in USD dated 15 Mar, **then** the rate defaults to the latest Spot rate on or before 15 Mar.
- **Given** no rate exists on or before that date, **when** I try to post, **then** it's blocked with "No USD→AED rate on or before 15 Mar".
- **Given** I override the rate (permission `finance.exchangerates.override`), **then** the override is stored on the document and audited.

### MD-04 Tax codes from the localisation pack
**As a** finance manager **I want** the tax codes of my country to be ready and adjustable **so that** invoices and bills calculate tax correctly.
Priority **M** · Points **5** · Sprint **2** · Spec §4.11, §6.5, §11.5, BR-TAX-*

- **Given** the UAE pack, **then** these codes are seeded: STD5 (Standard 5%), ZERO (Zero-rated), EXEMPT, OOS (Out of scope), RC5 (Reverse charge 5%), each with output/input GL accounts (2100/1200) and a VAT201 box mapping.
- **Given** a tax code, **when** I change its rate, **then** I must enter an effective-from date. Documents dated before it keep the old rate (BR-TAX-05).
- **Given** a tax code used on documents, **then** its treatment and box mapping can't change. It can only be deactivated.
- **Given** the input-tax default, **then** each code has a "recoverable by default" flag that bill lines inherit (BR-TAX-04).
- **Given** a legal entity that isn't tax-registered, **then** STD5 and ZERO can't be selected on its sales documents (BR-TAX-03).

### MD-05 Chart of accounts
**As an** accountant **I want** to maintain the chart of accounts, starting from my industry's template **so that** postings land in the right accounts.
Priority **M** · Points **5** · Sprint **2** · Spec §4.17, BR-GL-05/06

- **Given** a new org, **then** the industry template's accounts are seeded, with system accounts (AR 1100, input VAT 1200, AP 2000, output VAT 2100, deposits 2200, opening equity 3900, retained earnings 3100) tagged "System".
- **Given** New account, **when** I enter code (4 digits, unique), name, type, reporting group, description and Active, **then** it's saved and appears in its type's table with the group total.
- **Given** a System account, **then** it can't be deleted or retyped. Only its name and description can change.
- **Given** an account with postings, **then** its type can't change and it can't be deleted. It can only be deactivated, and inactive accounts aren't selectable on new lines.
- **Given** "Show inactive" and "Balances as of <date>", **then** the list shows balances per account following the header entity scope.
- **Given** a control account, **then** "Allow manual posting" is off by default.

### MD-06 Import a chart of accounts
**As an** accountant migrating from another system **I want** to import my chart from CSV **so that** I keep my account codes.
Priority **S** · Points **3** · Sprint **4**

- **Given** a CSV (code, name, type, group, description), **when** I run the import, **then** a preview shows new, updated and invalid rows before anything is saved.
- **Given** a CSV row whose code matches a System account, **then** only name and description are updated.
- **Given** the import completes, **then** an audit event lists the counts.

### MD-07 Create and maintain parties with roles
**As a** staff user **I want** one record per customer, supplier or tenant, with roles **so that** I don't keep duplicates when someone is both customer and supplier.
Priority **M** · Points **5** · Sprint **2** · Spec §4.8, §4.13, BR-PTY-*, Decision (accepted): one party record with roles

- **Given** New party, **when** I enter type (Individual/Company), name, optional email/phone/ID/nationality/TRN and choose at least one role (Customer, Supplier; modules add Tenant, Owner …), **then** the party is saved and each role gets its code (`CUS-0001`, `VEN-0001`).
- **Given** a TRN is entered, **then** it must be 15 digits (UAE pack). **And** if "Tax registered" is ticked, a TRN is required.
- **Given** the Supplier role, **then** I can set payment terms (default 30 days), bank and IBAN (AE + 21 characters validated).
- **Given** a party used on documents, **when** I try to delete it, **then** only Inactive is offered. Inactive parties can't be picked on new documents.
- **Given** the party list, **then** I can filter by role, type and status, and search by name, code, phone, email or TRN.

### MD-08 Duplicate party warning
**As a** staff user **I want** to be warned about possible duplicates **so that** the party list stays clean.
Priority **S** · Points **3** · Sprint **4** · BR-PTY-04

- **Given** I create a party whose TRN, email or phone matches an existing party, or whose name is very similar, **then** a warning lists the matches with "Open existing" and "Create anyway".
- **Given** I choose "Create anyway", **then** the decision is audited.

### MD-09 Party addresses, contacts and bank accounts
**As a** staff user **I want** several addresses, contacts and bank accounts per party **so that** documents and payments use the right ones.
Priority **S** · Points **3** · Sprint **4** · *[Assumption]*

- **Given** a party, **when** I add addresses (Billing/Shipping/Registered), contacts and bank accounts, **then** one of each can be marked default and is used by default on documents and payments.
- **Given** an IBAN, **then** it's validated by country format and checksum. Sensitive values are masked for users without the sensitive permission.

### MD-10 Number series
**As a** finance manager **I want** to configure document numbering per type and legal entity **so that** numbers follow our format and tax invoices are sequential with no gaps.
Priority **M** · Points **5** · Sprint **2** · Spec §11.4, BR-NUM-*

- **Given** a new org, **then** the template's series are seeded (e.g. `INV-{YYYY}-{000000}` per legal entity, yearly reset; `CUS-{0000}` org-wide, never reset).
- **Given** Number series, **when** I change a prefix or padding for a series that hasn't been used this period, **then** the next number previews live (e.g. "Next: INV-2026-000001").
- **Given** a series already used in the current period, **then** its format is locked until the next reset.
- **Given** two users issue invoices at the same moment, **then** each gets a distinct consecutive number (concurrency test with row locking).
- **Given** a draft is deleted or a posting transaction fails, **then** no gapless-series number is consumed.

### MD-11 Fiscal years and periods
**As a** finance manager **I want** fiscal years and monthly periods for each legal entity **so that** postings are controlled by period.
Priority **M** · Points **3** · Sprint **2** · BR-FIS-01/05, Q-C4

- **Given** a new legal entity with fiscal-year start month January, **then** the current fiscal year and 12 monthly periods are created automatically.
- **Given** "Create next fiscal year", **then** the following year and its periods are generated. Overlapping years are rejected.
- **Given** a posting date outside any open fiscal year, **then** posting is blocked with "No open fiscal year for <date>".

### MD-12 Lock and reopen books
**As a** finance manager **I want** to lock an entity's books up to a date **so that** closed months can't be changed.
Priority **M** · Points **3** · Sprint **3** · Spec §4.4, §5.9, BR-FIS-02/03/04

- **Given** Legal entities → ⋮ → Lock / reopen period, **when** I set "Books locked to" = 30 Sep 2026, **then** any journal (manual or automatic, including voids and credit notes) dated on or before 30 Sep 2026 for that entity is rejected with "Books are locked to 30 Sep 2026. Date the correction in an open period."
- **Given** I reopen (move the date earlier or clear it), **then** a reason is mandatory, `finance.periods.reopen` is required (setting the date needs `finance.periods.lock`), and an audit event records the old and new dates and the reason.
- **Given** the database, **then** a trigger also rejects journal inserts dated on or before the lock date (backstop).

### MD-13 Master data CSV export and list conventions
**As a** staff user **I want** every master-data list to support search, filters, paging and CSV export **so that** I can work with the data in Excel.
Priority **M** · Points **3** · Sprint **3** · Spec §4 (global conventions)

- **Given** any master-data list (entities, business units, parties, accounts, tax codes, currencies, rates, series), **then** it has a search box, filters, refresh, CSV download, 25 rows per page with a page-size selector and a record count.
- **Given** CSV export, **then** it respects the current filters, entity scope and permission masking, and streams from the server.

---

## Epic LED: Ledger core

Grounded in the spec's roadmap sprint "Ledger core" (§13: chart of accounts, bank accounts with opening balance, manual journals, posting service, period lock, trial balance and GL reports), plus §4.18, §4.19, §5.9, §5.10, §6.3, §6.4 and `core-domain.md` Sections 3.5–3.6. The chart of accounts (MD-05/06), fiscal years (MD-11) and the lock date (MD-12) are already in the MD epic, so they aren't repeated here. Planned for **sprint 5**.

### LED-01 Posting service and journal model
**As a** finance manager **I want** every business event to post a balanced journal through one service **so that** the books are always consistent and traceable.
Priority **M** · Points **8** · Sprint **5** · §5.10, §10.5, BR-GL-*, P2

- **Given** a domain event (e.g. `InvoiceIssued`) with a posting rule, **when** it's published, **then** the posting service creates one journal `JV-YYYY-000000` with status Posted, source (event type), document link and lines with account, debit or credit, dimensions (business unit, cost centre, module dimensions) and memo.
- **Given** any journal, **when** Σ debits ≠ Σ credits (in base currency, after rounding), **then** posting fails, the whole transaction is rolled back, and the error is logged.
- **Given** a journal dated on or before the legal entity's "Books locked to" date (MD-12), **then** the posting service rejects it, including voids and credit notes, with "Date the correction in an open period" (§5.9).
- **Given** the same event is delivered twice, **then** only one journal exists (idempotency key = event id).
- **Given** a posted journal, **then** it can't be edited or deleted through any API. Corrections are reversals (P6).
- **Given** a foreign-currency document, **then** each line stores the transaction amount, currency, rate and base amount (BR-FX-*).

### LED-02 Posting rule defaults per template
**As a** finance manager **I want** to see and adjust which accounts each event posts to **so that** postings follow our chart without code changes.
Priority **M** · Points **3** · Sprint **5** · §5.10, core Section 7 (posting rules)

- **Given** a new org from the real estate template, **then** the posting rules are seeded from §5.10 (e.g. receipt → Dr bank GL / Cr 1100; bill approved → Dr expense, Dr 1200 / Cr 2000).
- **Given** `finance.accounts.manage`, **when** I change the default account for an event (e.g. other income → 4900), **then** later postings use it, and the change is audited. Posted journals are unchanged.
- **Given** an event whose rule points to an inactive or non-posting account, **then** saving the rule is blocked.

### LED-03 Bank and cash accounts with opening balance
**As a** finance manager **I want** to set up bank and cash accounts, each with its own ledger account **so that** balances shown are what the books say.
Priority **M** · Points **5** · Sprint **5** · §4.18, §5.10 (opening balance)

- **Given** `finance.bankaccounts.manage`, **when** I enter legal entity, account name ("Operating account"), bank, account no., IBAN (23 characters, AE format), currency, opening balance and opening date, **then** the account is created with its own GL account (next free code under 1010, e.g. 1011), and it's Active.
- **Given** an opening balance of AED 6,500,000.00 on 18 Aug 2025, **then** a journal Dr 1011 / Cr 3900 Opening balance equity is posted ("Posted against opening balance equity").
- **Given** an opening date in a locked period, **then** saving is blocked.
- **Given** the list, **then** I see summary cards (total cash at bank, per legal entity) and account cards (name, "Bank · GL code · Entity", balance, IBAN, opened date), respecting the header entity scope.
- **Given** an account with postings, **when** I try to delete it, **then** only deactivation is offered.

### LED-04 Bank account detail and register
**As an** accountant **I want** a register of every movement on a bank account **so that** I can check it against the bank statement.
Priority **M** · Points **3** · Sprint **5** · §4.18

- **Given** a bank account and a date range, **then** KPI cards show Opening, Money in (n receipts), Money out (n payments) and Closing, where Closing = Opening + in − out.
- **Given** the register, **then** each line shows Date, Journal, Source, Document, Memo, Money in, Money out and running Balance, and CSV export matches the screen.
- **Given** a line, **when** I click its journal or document, **then** it opens.

### LED-05 Manual journals: draft, save and post
**As an** accountant **I want** to enter manual journals for accruals, prepayments, opening balances and corrections **so that** the ledger is complete.
Priority **M** · Points **5** · Sprint **5** · §4.19

- **Given** `finance.journals.create`, **when** I enter legal entity, date, reference, memo and at least two lines (account, debit or credit, description, dimensions), **then** the footer shows Debits / Credits / Difference, which is green at zero.
- **Given** a line with both a debit and a credit, or fewer than two lines with amounts, or Σ debits ≠ Σ credits, **then** "Save & post" is disabled with the message from §4.19.
- **Given** Save draft, **then** the journal is saved as Draft with no ledger effect and can be edited or deleted.
- **Given** `finance.journals.post` and the setting "Manual journals require approval" off, **when** I click Save & post, **then** it's numbered `JV-YYYY-000000` and posted.
- **Given** the example accrual Dr 5600 22,500 / Cr 2400 22,500 dated 18 Oct 2026, **then** it posts and shows in the GL for both accounts.

### LED-06 Journal approval and reversal
**As a** finance manager **I want** manual journals to go through approval when required, and posted journals to be reversible **so that** corrections are controlled.
Priority **M** · Points **3** · Sprint **5** · §4.19, §5.8, P6

- **Given** "Manual journals require approval" on, **when** I submit a draft, **then** an approval request is raised, and only a different user with `core.approvals.decide` can approve (posts) or reject (back to Draft).
- **Given** a posted journal and `finance.journals.reverse`, **when** I choose Reverse with a date in an open period, **then** a new journal with swapped debits and credits is posted, and both are linked ("Reversed by" / "Reverses").
- **Given** a journal that was posted automatically by a document (source not Manual), **then** Reverse isn't offered. The document's own void or credit note must be used.

### LED-07 Journals list and detail
**As an** accountant **I want** to find any posting **so that** I can trace a balance back to its documents.
Priority **M** · Points **2** · Sprint **5** · §4.19

- **Given** the list, **then** I can search "Number, memo or doc…" and filter by Source (Manual, Invoice, Receipt, Vendor bill, Vendor payment, Cheque, …), Status, Account and date range, with 25 rows per page and CSV.
- **Given** a journal, **then** the detail shows Date, Legal entity, Source, Document (link), Reference, Posted, Total, and tabs Lines (with totals), Documents and History.
- **Given** any document with "View journal", **then** it opens this detail.

### LED-08 Trial balance
**As a** finance manager **I want** a trial balance for any date range **so that** I can check the books balance before reporting.
Priority **M** · Points **3** · Sprint **5** · §6.3

- **Given** a date range, **then** each account shows Code, Account, Type, Opening Dr/Cr, Period Dr/Cr and Closing Dr/Cr, where opening = balance before the start date, period = movements in the range, and closing = opening + period shown on the side of its sign.
- **Given** any range, **then** each column pair balances (Σ Dr = Σ Cr), and a non-zero difference is shown as an error banner.
- **Given** the header entity scope "All entities", **then** amounts are summed across entities in base currency. With one entity, only its journals are included.
- **Given** CSV or Print, **then** the output matches the screen.

### LED-09 General ledger report
**As an** accountant **I want** a general ledger with running balances **so that** I can review every line on an account.
Priority **M** · Points **3** · Sprint **5** · §6.4

- **Given** a date range, accounts and (optional) module dimension filters, **then** each account is an expandable row "code name · type · n lines" with "Opening → Closing".
- **Given** an expanded account, **then** lines show Date, Journal, Source, Document, Memo, Debit, Credit and a running balance in the account's normal direction (debit for assets/expenses, credit for liabilities/equity/income).
- **Given** 1100 Accounts receivable for the demo range, **then** opening 0.00 → closing 155,362.50 with 8 lines (§6.4 demo).

### LED-10 Year-end close
**As a** finance manager **I want** a year-end close wizard **so that** income and expenses move to retained earnings and the year is locked.
Priority **S** · Points **5** · Sprint **5** · §5.9

- **Given** a fiscal year with all periods ended and `finance.fiscalyears.manage`, **when** I run the wizard for a legal entity, **then** it previews a closing journal Dr income accounts / Cr expense accounts / net to 3100 Retained earnings.
- **Given** I confirm, **then** the journal is posted on the last day of the year (source Year-end close), and the books are locked to that date (needs `finance.periods.lock`).
- **Given** the year is already closed, **then** the wizard is blocked. Reopening needs `finance.periods.reopen` and reverses the closing journal.
- **Given** the balance sheet before closing, **then** it notes "Earnings are shown until a year-end closing journal moves them to retained earnings."

---

## Epic DOC: Finance documents (FIN-DOC)

Sales invoices, credit notes, vendor bills and vendor payments as **core finance** documents (`core-domain.md` Section 10, FIN-DOC-FR-*, BR-DOC-*). **Receipts and their allocation are in the AR epic** (AR-01…05), and the cheque auto-receipt is CHQ-03, so they aren't repeated here. Tax comes from the localisation pack's tax codes (MD-04); numbering from the core number series (MD-10); approvals from the core approval engine. Planned for **sprints 6–7**.

### DOC-01 Draft a sales invoice
**As an** accountant **I want** to draft a sales invoice with lines and tax **so that** I can review it before it becomes a tax invoice.
Priority **M** · Points **5** · Sprint **6** · §4.11, FIN-DOC-FR-01, BR-TAX-*

- **Given** `finance.invoices.create`, **when** I choose customer, issuing legal entity, invoice date (today), service period and lines (description, qty > 0, unit price, tax code, income account), **then** line totals (qty × price), subtotal, VAT and total are calculated, and the draft is saved unnumbered with no ledger effect.
- **Given** a customer with payment terms of 15 days, **when** the due date is blank, **then** it defaults to invoice date + 15 days.
- **Given** a line, **then** its tax code defaults from the pack's defaulting rule (or the module's rule, e.g. lease VAT treatment) and can be changed only with `finance.taxcodes.override` (audited).
- **Given** a standard-rated line and an issuing entity that isn't VAT-registered, **then** saving is blocked with "Standard-rated lines need a VAT-registered entity".
- **Given** service period "to" earlier than "from", **then** saving is blocked.
- **Given** a foreign-currency customer (USD), **then** the rate is taken from MD-03 and stored on the invoice.

### DOC-02 Issue a sales invoice
**As an** accountant **I want** to issue an invoice **so that** it gets its tax invoice number and posts to the ledger.
Priority **M** · Points **5** · Sprint **6** · §4.11, §5.3, §5.10, BR-DOC-01, BR-NUM-03

- **Given** a valid draft and `finance.invoices.issue`, **when** I click Issue (or Save & issue), **then** the next gapless number for that entity and year is allocated (e.g. `INV-2026-000028`) inside the posting transaction, and Dr 1100 (gross) / Cr income (net) / Cr 2100 Output VAT is posted.
- **Given** two users issue invoices for the same entity at the same moment, **then** both get consecutive numbers with no gap and no duplicate.
- **Given** posting fails (e.g. locked period), **then** no number is consumed, and the invoice stays Draft.
- **Given** an issued invoice, **then** its lines, amounts, customer and dates can't be edited. Only Notes stay editable (audited).
- **Given** a module-created invoice (source link, e.g. lease instalment), **then** the module receives `InvoiceIssued`.

### DOC-03 Invoice list, detail and tax invoice PDF
**As an** accountant **I want** to find invoices and print a compliant tax invoice **so that** I can send it to the customer.
Priority **M** · Points **5** · Sprint **6** · §4.11, BR-TAX-09, BR-FX-07

- **Given** the list, **then** I can search "Invoice no. or customer", use segments All / Unpaid / Overdue and filters Status, Type and date range. Columns are Number, Date, Customer, Source (e.g. Lease), Due (red "n days overdue"), Net, VAT (grey when 0), Total, Balance and Status.
- **Given** an issued invoice, **then** the detail shows the actions Record payment (opens AR-01 pre-filled), Credit note, Print and Void, the info card (with "View journal"), lines, and a summary (Subtotal, VAT, Total, Paid, Credited, Balance due).
- **Given** Print, **then** the PDF uses the pack's layout: title "TAX INVOICE", issuer name, address and TRN, customer and customer TRN if any, sequential number, dates, lines with taxable amount, VAT % and VAT, totals, notes and the org footer. It shows the configured org name only, no product brand.
- **Given** a USD invoice, **then** the VAT amount is also shown in AED (BR-FX-07).
- **Given** `finance.invoices.read` only, **then** the actions are hidden.

### DOC-04 Void an invoice
**As an** accountant **I want** to void an invoice issued in error **so that** the books are corrected with a full audit trail.
Priority **M** · Points **2** · Sprint **6** · §4.11, §5.3, BR-DOC-03

- **Given** an issued invoice with no allocations or credit notes and `finance.invoices.void`, **when** I void it with a reason, **then** a full reversing journal is posted, the status becomes Void, and the number is kept (never reused).
- **Given** an invoice with any allocation, **then** Void is blocked with "Remove allocations or issue a credit note".
- **Given** the original date is in a locked period, **then** the reversal is dated in the first open period (§5.9).

### DOC-05 Credit note against an invoice
**As an** accountant **I want** to issue a full or partial credit note **so that** I can correct an issued invoice without voiding it.
Priority **M** · Points **5** · Sprint **6** · §4.11, §5.3, FIN-DOC-FR-02

- **Given** an issued invoice and `finance.invoices.credit`, **when** I choose Credit note and select lines or amounts (≤ the uncredited amount per line) and a reason, **then** a credit note `CN-YYYY-000000` (gapless) is issued, posting Dr income / Dr 2100 / Cr 1100.
- **Given** the credit note is issued, **then** the invoice balance falls by its total, and the summary shows "Credited".
- **Given** an invoice that's fully paid, **when** credited, **then** the credit becomes customer credit (On account) that can be allocated or refunded.
- **Given** the PDF, **then** it's titled "TAX CREDIT NOTE" and references the original invoice number and date (pack layout).
- **Given** a credit note, **then** it's reported negative in the tax return source data (VAT201 from §6.5).

### DOC-06 Invoice payment status and overdue
**As a** finance user **I want** invoice statuses kept up to date **so that** lists and ageing show the truth.
Priority **M** · Points **2** · Sprint **6** · §4.11, §5.3

- **Given** allocations totalling less than the balance, **then** the status is Partly paid. When the balance reaches 0, it's Paid.
- **Given** the nightly job, **when** balance > 0 and due date < today, **then** the invoice is flagged Overdue, and the list shows "n days overdue".
- **Given** an allocation is removed (receipt void), **then** the status is recalculated.

### DOC-07 Draft and submit a vendor bill
**As an** accountant **I want** to enter supplier invoices **so that** costs and input VAT are recorded.
Priority **M** · Points **5** · Sprint **7** · §4.14, §5.4, FIN-DOC-FR-04, BR-PTY-06

- **Given** `finance.bills.create`, **when** I enter legal entity (buyer), vendor, vendor invoice no., bill date, optional due date (blank = vendor terms), optional dimensions (e.g. property/unit from a module) and lines (description, expense account, qty, unit price, tax code, Input VAT recoverable checkbox), **then** Net / VAT / Total are calculated and I can Save draft.
- **Given** the same vendor invoice no. already exists for that vendor, **then** saving is blocked with "Duplicate vendor invoice" (BR-PTY-06).
- **Given** Save & submit with total ≤ the bill approval threshold (default AED 10,000), **then** the bill is numbered `BILL-YYYY-000000`, posted and Approved at once.
- **Given** a bill created from a module record (e.g. a work order), **then** it carries the source link.

### DOC-08 Bill approval and posting
**As a** finance manager **I want** large bills approved before they post **so that** spending is controlled.
Priority **M** · Points **5** · Sprint **7** · §4.14, §5.4, §5.8, BR-DOC-06

- **Given** a submitted bill above the threshold, **then** it becomes Pending approval with the banner "Waiting for approval…", has "Ledger posting: Not posted", and can be withdrawn by the maker (back to Draft).
- **Given** an approver with `core.approvals.decide` who isn't the maker, **when** they approve, **then** Dr expense (net + blocked VAT) / Dr 1200 (recoverable VAT) / Cr 2000 AP (total) is posted, and the bill becomes Approved and payable.
- **Given** the BILL-2026-000015 example (18,000 + 900 VAT marked blocked), **then** Dr 5000 18,900 / Cr 2000 18,900 is posted with no 1200 line.
- **Given** the maker tries to approve their own bill, **then** it's blocked.
- **Given** a rejection with a comment, **then** the bill returns to Draft (Rejected shown in History).
- **Given** the threshold setting is 0, **then** every bill needs approval. With −1, none do.

### DOC-09 Vendor payment with allocation to bills
**As an** accountant **I want** to pay suppliers against approved bills **so that** payables are settled and the bank is credited.
Priority **M** · Points **5** · Sprint **7** · §4.15, §5.4, FIN-DOC-FR-05

- **Given** `finance.payments.create`, **when** I choose paying entity → vendor → bank account (same entity and currency) → method (Bank transfer / Cash / Cheque) and allocate amounts to approved unpaid bills, **then** the button reads "Record payment of AED x" with the allocated total.
- **Given** amount ≤ the payment approval threshold (default AED 25,000), **then** `PAY-YYYY-000000` posts Dr 2000 / Cr Bank, and the bills become Partly paid / Paid.
- **Given** amount above the threshold, **then** it's Pending approval ("the bills are settled and the bank is credited only after it is approved"), and the maker can Cancel request (`finance.payments.cancel`).
- **Given** method Cheque, **then** the outgoing cheque is handled by CHQ-07 (leaf assigned after approval; post-dated → 2080 PDC payable).
- **Given** an allocation above a bill's balance, **then** saving is blocked.
- **Given** Print voucher, **then** a payment voucher PDF lists the bills settled.

### DOC-10 Void a bill or a payment
**As a** finance manager **I want** to void a wrong bill or payment **so that** the payables ledger is corrected.
Priority **M** · Points **3** · Sprint **7** · BR-DOC-03, §5.3

- **Given** an approved bill with no payments and `finance.bills.void`, **when** I void it with a reason, **then** the posting is reversed and the status becomes Void.
- **Given** a bill with payments, **then** Void is blocked until the payment is voided.
- **Given** a posted payment, **when** I void it, **then** Dr Bank / Cr 2000 is posted, its allocations are removed, and the bills' balances reopen. If it was paid by an outgoing cheque, the cheque's stop/void (CHQ-08) is required first.

### DOC-11 Payables ageing and vendor statement
**As a** finance manager **I want** payables ageing and vendor statements **so that** I can plan payments and reconcile with suppliers.
Priority **M** · Points **3** · Sprint **7** · §6.7, §4.13, FIN-DOC-FR-06

- **Given** an as-of date, **then** approved open bills are bucketed by days past due (Current / 1–30 / 31–60 / 61–90 / 90+), with Unapplied credit and Net balance, by vendor or by document.
- **Given** the demo data, **then** the total is 33,390 with 3,990 overdue (§6.7).
- **Given** a vendor and a date range, **then** the statement lists opening balance, bills, payments, credits and closing balance, with PDF and CSV.
- **Given** the vendor detail page, **then** the KPI cards Open balance and Spend this year (approved bills, VAT inclusive) match these figures.

### DOC-12 Document service for modules
**As a** module developer **I want** to create invoices and bills through the core service **so that** modules never post their own journals.
Priority **M** · Points **3** · Sprint **7** · FIN-DOC-FR-07, P2

- **Given** a module command (e.g. billing run) calling the invoice service with customer, entity, lines, source link and line defaults (account, tax code, dimensions), **then** a draft or issued invoice is created exactly as through the UI, with the same validation and permissions.
- **Given** a document with a source link, **when** it's issued, paid, credited or voided, **then** the module receives `InvoiceIssued`, `InvoicePaid`, `InvoiceCredited` or `InvoiceVoided` (and `BillApproved` / `BillPaid` for bills).
- **Given** a module tries to write journal lines directly, **then** there's no API for it (architecture test).

---

## Epic AR: Receipts and receivables

Grounded in the spec's roadmap sprint "Receipts & AR" (§13: receipt with allocation, on-account, cheque-clear auto receipt, customer statement, receivables ageing) and §4.12, §5.3, §6.6. The cheque auto receipt is **CHQ-03**, which reuses AR-01/02. The rent roll belongs to the Real Estate module. Planned for **sprints 6–7**.

### AR-01 Record a receipt
**As an** accountant **I want** to record money received by bank transfer, cash or card **so that** the customer's balance and the bank are updated.
Priority **M** · Points **5** · Sprint **6** · §4.12, §5.10, FIN-DOC-FR-03

- **Given** `finance.receipts.create`, **when** I enter received from (customer), received by (legal entity), purpose (Receivable settlement by default; modules add purposes such as Rent or Security deposit), date, amount > 0, method (Bank transfer / Cash / Card), deposited into (a bank account of that entity, enabled after the entity) and reference, **then** `RCT-YYYY-000000` is posted: Dr bank GL / Cr 1100 (or the purpose's account, e.g. 2200 for a security deposit).
- **Given** the RCT-2026-000031 example (30,000 from Vertex Consulting FZ-LLC to Oasis Investments LLC, ref FT264328588), **then** it posts and appears in the bank register (LED-04).
- **Given** method Cheque, **then** the form says "Cheques are recorded through the cheque register" and links to CHQ-01.
- **Given** a date in a locked period, **then** posting is blocked.

### AR-02 Allocate a receipt to invoices
**As an** accountant **I want** receipts allocated oldest-first or by hand **so that** the right invoices are marked paid.
Priority **M** · Points **5** · Sprint **6** · §4.12, §5.3, BR-DOC-04

- **Given** customer and entity chosen, **then** the "Allocate to invoices" grid lists their open issued invoices (Invoice, Source, Date, Due, Total, Balance, Allocate). If there are none: "No open invoices – the full amount will be held on account."
- **Given** the switch "Oldest first, automatically" is on, **when** I save, **then** the amount is allocated to the oldest due invoices first, and any remainder is On account.
- **Given** manual allocation, **then** each allocation ≤ the invoice balance and Σ allocations ≤ the receipt amount. Otherwise saving is blocked.
- **Given** RCT-2026-000031 allocated 30,000 to INV-2026-000020 (59,062.50), **then** that invoice is Partly paid with balance 29,062.50 (DOC-06).
- **Given** "Record payment" on an invoice (DOC-03), **then** the receipt opens pre-filled and allocated to that invoice.

### AR-03 Allocate on-account money later
**As an** accountant **I want** to apply on-account money to invoices later **so that** customer credits are used.
Priority **M** · Points **3** · Sprint **6** · §5.3

- **Given** a receipt with an On account balance and `finance.receipts.allocate`, **when** I allocate it to an open invoice, **then** an allocation record is created with **no new journal** (AR was already credited).
- **Given** a credit note's customer credit (DOC-05), **then** it can be allocated the same way.
- **Given** the receipts list segment "On account", **then** it shows only receipts with unallocated money.

### AR-04 Void a receipt
**As a** finance manager **I want** to void a receipt recorded in error **so that** the bank and receivables are corrected.
Priority **M** · Points **2** · Sprint **6** · §4.12, BR-DOC-03

- **Given** a posted receipt and `finance.receipts.void`, **when** I void it with a reason, **then** Dr 1100 / Cr bank is posted, its allocations are removed, and the invoices' statuses are recalculated (DOC-06).
- **Given** a receipt created by a cheque clearing, **then** Void isn't offered. The cheque's bounce (CHQ-04) is used instead.

### AR-05 Receipts list and detail
**As a** finance user **I want** to find receipts and see where the money went **so that** I can answer customer queries.
Priority **M** · Points **3** · Sprint **7** · §4.12

- **Given** the list, **then** I can search, use segments All / On account and filters Purpose, Status and date range. Columns are Number, Date, Customer, Purpose, Method, Reference (e.g. "Cheque 100381 · Mashreq"), Bank account, Amount, Unallocated and Status.
- **Given** a receipt, **then** the detail shows KPI cards Amount, Allocated (n invoices) and On account ("Available to allocate"), the info grid (with "From cheque" and "View journal" links), the Allocations table, Documents and History, and Print and Void actions.

### AR-06 Customer statement
**As an** accountant **I want** to send a customer statement **so that** the customer can reconcile and pay.
Priority **M** · Points **3** · Sprint **7** · roadmap §13 "customer statement", FIN-DOC-FR-06

- **Given** a customer, a legal entity and a date range, **then** the statement shows opening balance, invoices, credit notes, receipts, closing balance and an ageing summary, in the document currency.
- **Given** PDF, **then** it uses the org's statement template and footer. **When** I choose Email, it's sent to the customer's billing email and logged on the customer's History.
- **Given** On account money, **then** it's shown as "Unapplied credit".

### AR-07 Receivables ageing
**As a** finance manager **I want** receivables ageing **so that** I can chase overdue customers.
Priority **M** · Points **5** · Sprint **7** · §6.6

- **Given** an as-of date, **then** the bucket = as-of date − due date: ≤ 0 Current, 1–30, 31–60, 61–90, > 90. Columns are Customer, buckets, Unapplied credit (negative) and Net balance = Σ buckets + unapplied credit.
- **Given** the "By customer / By document" toggle, **then** the view switches, and KPI cards (Total outstanding, Overdue) and a bar chart by bucket are shown.
- **Given** the demo data as of 22 Oct 2026, **then** the total is 155,362.50 and overdue is 54,562.50 for 2 customers (§6.6).
- **Given** the header entity scope, **then** only that entity's documents are included.

### AR-08 Realised FX difference on allocation
**As a** finance manager **I want** FX gains and losses posted when foreign-currency invoices are settled **so that** AR in base currency is correct.
Priority **S** · Points **3** · Sprint **7** · BR-FX-04, BR-DOC-08

- **Given** a USD invoice issued at rate 3.6725 and a USD receipt at 3.6700 allocated to it, **then** the difference in AED is posted to the FX gain/loss account at allocation, and the allocation stores it.
- **Given** a same-currency, same-rate allocation, **then** no FX journal is posted.

---

## Epic CUS: Customization (custom fields)

Planned for **sprints 4, 8 and 9** (it depends on parties, the master-data list conventions and the audit interceptor). Rules are in `core-domain.md` Section 9.

### CUS-01 Manage-fields permission
**As an** org admin **I want** a dedicated permission for managing custom fields **so that** only trusted people can change the data model.
Priority **M** · Points **2** · Sprint **4** · CF-SEC-01/02

- **Given** a new org, **then** `core.customfields.manage` is granted to Org admin only, and `core.customfields.read` to Org admin, Finance manager and Accountant.
- **Given** a user without `core.customfields.manage`, **when** they open Settings → Custom fields, **then** definitions are read-only (if they have `core.customfields.read`) or the page is hidden.
- **Given** the API, **when** such a user calls `POST /api/v1/custom-fields`, **then** it returns 403.

### CUS-02 Create a custom field definition
**As an** org admin **I want** to add a field to a record type, choosing its type **so that** we capture information the standard form lacks.
Priority **M** · Points **8** · Sprint **4** · CF-NAM-*, CF-LIM-01, Section 9.3

- **Given** Settings → Custom fields → Record type "Party" → New field, **when** I enter label "Credit rating" and choose a type from the supported list (text, long text, integer, decimal, currency, percent, date, date-time, boolean, single select, multi select, lookup, email, phone, URL), **then** the API key `cf_credit_rating` is proposed and can be edited before saving.
- **Given** I edit the key to `cf_Credit-Rating`, `credit_rating` or a key longer than 63 characters, **then** saving is blocked with "Key must match `^cf_[a-z0-9_]{1,60}$`". The same check applies to the API, imports and field packs (CF-NAM-01).
- **Given** I save, **then** the API key is locked permanently. Its uniqueness within the record type is enforced, and keys of deleted fields are reserved for 90 days.
- **Given** type Lookup, **then** I must choose a target record type and may add a filter (e.g. Party role = Supplier).
- **Given** the record type already has 50 custom fields (active or inactive, including field-pack fields), **then** creating another is blocked with "Limit reached: 50 custom fields per record type" (CF-LIM-01).
- **Given** "Computed" or "Rollup" type, **then** it isn't offered (later phase).

### CUS-03 Option lists for select fields
**As an** org admin **I want** to define and maintain options for select fields **so that** users pick consistent values.
Priority **M** · Points **3** · Sprint **8** · CF-LIF-03

- **Given** a single or multi select field, **when** I add options (label, key generated, colour, order), **then** they appear in that order on forms.
- **Given** an option in use, **when** I deactivate it, **then** existing records keep it (shown with an "inactive" hint) but it can't be picked on new edits. Deleting it is blocked.
- **Given** I rename an option label, **then** the key stays unchanged and existing values show the new label.

### CUS-04 Place fields on forms and detail pages
**As an** org admin **I want** to choose where a field appears and in which order **so that** forms stay logical for users.
Priority **M** · Points **5** · Sprint **8** · Section 9.5

- **Given** a field, **when** I choose section "Contact" and "insert after Phone", **then** the field appears after Phone in the create/edit form and in the detail info grid.
- **Given** no placement chosen, **then** the field appears in a "Custom fields" section at the end of the form.
- **Given** the field-order screen, **when** I drag fields to reorder them, **then** the new order applies for every user on next load (schema version bump).
- **Given** help text, **then** it's shown in grey under the field.

### CUS-05 Validation rules and default values
**As an** org admin **I want** validation and defaults on custom fields **so that** data quality is enforced.
Priority **M** · Points **5** · Sprint **8** · CF-VAL-*

- **Given** a field marked required, **when** a user saves a new record without it in the UI, API or import, **then** the save is rejected with a field-level error.
- **Given** min/max, regex or max length, **then** values outside the rule are rejected with the configured message (or a default one).
- **Given** "Unique", **when** a duplicate value is saved, **then** it's rejected. **And** turning on Unique while duplicates exist is blocked, with the duplicates listed.
- **Given** a default value, **then** it's pre-filled on create only. **And** existing records are back-filled only through the explicit audited "Apply default to empty records" action.
- **Given** I make an existing field required, **then** I see "N existing records have no value; they'll need one when next edited" before confirming.
- **Given** a system process (billing run, auto-posting), **then** required custom fields never block it.

### CUS-06 Custom fields in lists, filters and search
**As a** staff user **I want** to see, filter and search by custom fields in lists **so that** I can find records by our own data.
Priority **M** · Points **5** · Sprint **9** · Section 9.5, CF-LIM-01a

- **Given** a field with "Show in list", **then** it's available in the list's column chooser. It's sortable only if it's a scalar type flagged "Sortable".
- **Given** a field with "Filterable", **then** it appears in list filters with operators suitable for its type (equals/in for selects and lookups; ranges for numbers and dates; yes/no for booleans).
- **Given** a field with "Searchable" (text, email, phone), **then** the list search box and global search match its values.
- **Given** a record type already has 10 indexed fields (any mix of Searchable, Filterable, Sortable or Unique), **when** I flag an 11th, **then** saving is blocked with "Limit reached: 10 searchable/indexed fields per record type" (CF-LIM-01a).
- **Given** a list of 100,000 records, **when** I filter by a filterable custom field, **then** results return in < 500 ms p95 (NFR-01).

### CUS-07 Custom fields in the REST API
**As an** integration developer **I want** custom fields exposed predictably in the API **so that** I can read and write them safely.
Priority **M** · Points **5** · Sprint **8** · Section 9.5

- **Given** `GET /api/v1/parties/{id}`, **then** the response contains `"customFields": { "<apiKey>": value, … }` with only active fields the caller may see.
- **Given** `PUT`/`PATCH` with `customFields`, **then** values are validated like UI input. Unknown or inactive keys return 400 ProblemDetails naming the key.
- **Given** `GET /api/v1/parties?cf.cf_credit_rating=A&sort=cf.cf_credit_rating`, **then** filtering works for Filterable fields and sorting for Sortable fields (both indexed). Other fields return 400.
- **Given** `GET /api/v1/custom-fields?recordType=Party`, **then** the definitions (key, label, type, options, rules, version) are returned, and responses include `customFieldsVersion`.

### CUS-08 Custom fields in import and export templates
**As a** staff user **I want** custom fields included in import templates and CSV exports **so that** bulk data moves include our own data.
Priority **S** · Points **3** · Sprint **9** · Section 9.5

- **Given** I download an import template for a record type, **then** it contains a column per active custom field, headed by API key, with the label in a second header row.
- **Given** an import file with custom-field columns, **when** I run the dry run, **then** custom-field validation errors appear per row and cell.
- **Given** CSV export of a list, **then** visible custom columns are included, and sensitive fields are masked for users without permission.

### CUS-09 Deactivate or delete a custom field
**As an** org admin **I want** to retire a field without losing data **so that** I can tidy forms safely.
Priority **M** · Points **3** · Sprint **8** · CF-LIF-01/02

- **Given** an active field, **when** I deactivate it, **then** it disappears from forms, lists, filters, imports and the API write path. Values are kept and visible to admins in an "Inactive fields" panel.
- **Given** an inactive field, **when** I reactivate it, **then** all earlier values reappear unchanged.
- **Given** an inactive field from a tenant (not a pack), **when** I delete it and type its API key to confirm, **then** the definition and values are soft-deleted (purged after 30 days) and the deletion is audited.
- **Given** an active field, **then** Delete isn't offered.

### CUS-10 Field visibility and edit rights per role
**As an** org admin **I want** to limit who sees or edits a custom field **so that** confidential data stays private.
Priority **S** · Points **5** · Sprint **9** · CF-PRM-*

- **Given** a field visible to "Selected roles: Finance manager", **when** a Leasing manager opens the record, **then** the field isn't shown in UI, API responses, exports or audit diffs.
- **Given** a field marked Sensitive, **then** users without the sensitive permission see it masked (`••••`).
- **Given** edit rights "Selected roles", **then** other users see the field read-only, and an API write from them returns 403 for that key.

### CUS-11 Audit of definitions and values
**As an** org admin **I want** changes to custom fields and their values audited **so that** the audit trail stays complete.
Priority **M** · Points **2** · Sprint **9** · CF-SEC-06, BR-AUD-01

- **Given** a definition is created, changed, deactivated or deleted, **then** an audit event with before/after values and the new version number is written.
- **Given** a record's custom value changes, **then** the record's audit event lists `customFields.<apiKey>` in changed fields with before/after values, and the History tab shows it.

### CUS-12 Module field packs
**As a** module owner (e.g. Real Estate module) **I want** to ship optional predefined fields **so that** tenants of that industry get useful fields on day one.
Priority **S** · Points **5** · Sprint **9** · CF-PCK-*

- **Given** an org enables the Real Estate module, **then** its field pack (keys `cf_re_*`, e.g. `cf_re_parking_bays` on Unit) is installed and marked "Pack: realestate@1.0".
- **Given** a pack field, **then** the tenant can relabel, reorder, change visibility or deactivate it, but can't delete it or change its key or type.
- **Given** a pack upgrade to 1.1 that adds a field and changes a help text, **then** the new field is added, and the help text is updated only if the tenant hadn't edited it.

### CUS-13 Restrict a field to selected legal entities
**As an** org admin of a group **I want** some fields to apply only to certain legal entities **so that** each company sees only relevant fields.
Priority **C** · Points **3** · Sprint **9** · CF-SEC-04

- **Given** a field with "Applies to: Entity A", **when** a user opens an entity-scoped record of entity B, **then** the field is hidden and its rules (such as required) aren't enforced.
- **Given** a shared master record (Party) opened under entity scope B, **then** the field is hidden, but its stored value is kept and shown under scope A or "All".

---

## Epic CHQ: Cheques (core finance)

Rules are in `core-domain.md` Section 8 (FIN-CHQ-FR-*, BR-CHQ-*). **Dependency**: bank accounts (LED-03), the posting service (LED-01), receipts (AR-01/02) and vendor payments (DOC-09) come first. Planned for **sprints 8–9**.

### CHQ-01 Register incoming cheques
**As an** accountant **I want** to register cheques received from a party, singly or as a post-dated series **so that** every cheque in our safe is tracked.
Priority **M** · Points **5** · Sprint **8** · FIN-CHQ-FR-01, FIN-CHQ-FR-10, BR-CHQ-02

- **Given** `finance.cheques.create`, **when** I enter party, legal entity, cheque no., drawee bank, account holder, amount, currency, cheque date, received on and custodian, **then** the cheque is saved with register no. `CHQ-YYYY-000000` (or the template prefix, e.g. `PDC-`) and status In custody, and no journal is posted.
- **Given** "Add series" with first cheque no. 100379, count 12, first date 01 Aug 2026, interval 1 month and amount 8,166.67, **then** 12 cheques 100379–100390 are proposed with consecutive dates, and I can edit any row before saving.
- **Given** a cheque with the same cheque no. + drawee bank + account holder already exists and isn't Returned, **then** saving is blocked with "Duplicate cheque".
- **Given** a module supplies a source (e.g. a lease schedule), **then** the series is pre-filled from it and each cheque stores its source link.
- **Given** no custodian, **then** saving is blocked.

### CHQ-02 Cheque register and detail page
**As a** finance user **I want** a cheque register with KPIs and a detail page **so that** I can see what's in custody, due, deposited and bounced.
Priority **M** · Points **3** · Sprint **8** · FIN-CHQ-FR-02, FIN-CHQ-FR-16

- **Given** `finance.cheques.read`, **then** the register shows KPI cards (In custody amount and count, Due within 7 days, Deposited awaiting clearance, Bounced awaiting resolution) and the segments To deposit / Incoming / Outgoing / All, with filters for status, party, bank, cheque-date range and source module, CSV export and 25 rows per page.
- **Given** the header entity scope is entity A, **then** only entity A's cheques and KPIs appear.
- **Given** I open a cheque, **then** I see the actions allowed by its status and my permissions, the info grid, source links, linked receipt ("View journal"), Documents and History.

### CHQ-03 Deposit and clear (automatic receipt)
**As an** accountant **I want** to deposit cheques and mark them cleared **so that** the receipt is created and allocated without retyping.
Priority **M** · Points **5** · Sprint **8** · FIN-CHQ-FR-03/04, BR-CHQ-03/05/06/10

- **Given** In-custody cheques selected in bulk and `finance.cheques.deposit`, **when** I choose Deposit with a date and a bank account of the same legal entity and currency, **then** they become Deposited, no journal is posted (unless the in-transit memo setting is on), and a deposit slip PDF is available.
- **Given** a cheque dated after the deposit date, **then** depositing is blocked unless I enter an override reason (audited).
- **Given** a Deposited cheque and `finance.cheques.clear`, **when** I mark it cleared, **then** one receipt (method Cheque, reference "Cheque <no> · <bank>") is posted Dr Bank / Cr AR (or the purpose account) and allocated through the source module's hook, else oldest-first, else On account, and the cheque becomes Cleared.
- **Given** the cheque is already Cleared, **when** clear is called again (UI or API), **then** it's rejected and no second receipt exists.
- **Given** the cleared date falls in a locked period, **then** clearing is blocked with the period-lock message.

### CHQ-04 Bounce and re-present
**As an** accountant **I want** to record a bounced cheque with the bank's reason **so that** the receivable reopens and the right people are alerted.
Priority **M** · Points **5** · Sprint **8** · FIN-CHQ-FR-05/06, BR-CHQ-04, core Section 8.9

- **Given** a Deposited or Cleared cheque and `finance.cheques.bounce`, **when** I enter the bounce date, reason code and attach the bank return memo, **then** the status becomes Bounced, an alert is raised for the entity's finance users, and the source module is notified.
- **Given** the cheque was Cleared, **then** its receipt is reversed (Dr AR / Cr Bank) in an open period, and the original receipt remains visible.
- **Given** the localisation pack enables partial clearing (UAE), **when** the bank paid part of the amount, **then** I can record the paid part as a receipt and the unpaid balance stays Bounced.
- **Given** a reason code marked "may re-present", **when** I choose Re-present with a new deposit date, **then** the status returns to Deposited and the history shows both presentations.
- **Given** a Bounced cheque and `finance.cheques.followup`, **then** I can set its **legal follow-up status** (core list: None / Reminder sent / Legal action / Settled / Written off; packs may rename or add steps) and add notes, dates and documents for each step. Each change writes a ChequeEvent (Decision, 22 Oct 2026).
- **Given** the UAE pack, **then** the steps show as None / Notice sent / Filed for execution (executory instrument) / Settled / Written off, and a bounced cheque without a bank return memo can't move to "Filed for execution" (`core-domain.md` Section 8.9).

### CHQ-05 Replace and return
**As an** accountant **I want** to replace a bounced or held cheque, or return it to the drawer **so that** the debt's history stays traceable.
Priority **M** · Points **3** · Sprint **9** · FIN-CHQ-FR-07/08, BR-CHQ-08

- **Given** a Bounced or In-custody cheque and `finance.cheques.replace`, **when** I register one or more replacement cheques and/or link another receipt, **then** the amounts must sum to the original (or a difference reason is required), the old cheque becomes Replaced, and replaced-by links are shown both ways.
- **Given** the original had source links, **then** they're carried to the replacements, and the source module may re-map them (e.g. to instalments).
- **Given** an In-custody or Bounced cheque and `finance.cheques.return`, **when** I return it with the date and the name of the person who collected it, **then** the status becomes Returned with no ledger effect.
- **Given** an In-custody cheque with no receipt, **then** Edit and Delete are available (`finance.cheques.edit` / `.delete`). For any other status they aren't.

### CHQ-06 Cheque reminders and custody
**As a** finance manager **I want** reminders for cheques due, uncleared or unresolved, and a custody log **so that** nothing is missed in the safe.
Priority **M** · Points **3** · Sprint **9** · FIN-CHQ-FR-10/14/15, core Section 5 settings

- **Given** legal entity A's **cheque-due alert days** is 7 (core finance setting, org default, overridable per legal entity) and entity B's is 3, **when** the alerts job runs, **then** A's cheques dated within 7 days and B's within 3 days they appear in "To deposit", on the dashboard alerts and in a daily digest to users with `finance.cheques.deposit` for the entity.
- **Given** a Deposited cheque not cleared after 5 days, or a Bounced cheque unresolved after 7 days, **then** an alert is raised, and the bounced one escalates to the Finance manager.
- **Given** I transfer custody (`finance.cheques.edit`) from "Finance safe – Marina office" to another custodian, **then** a custody event with from/to, date and user is logged, and the custody report shows current holdings per custodian.
- **Given** cheques in custody, **then** they appear in the cash forecast by cheque date.

### CHQ-07 Outgoing cheques: cheque books, issue and print
**As an** accountant **I want** to pay suppliers by cheque from our cheque books **so that** leaves are controlled and printed correctly.
Priority **M** · Points **5** · Sprint **9** · FIN-CHQ-FR-11/12, BR-CHQ-02/09

- **Given** `finance.cheques.create`, **when** I register a cheque book for a bank account with leaves 000101–000150, **then** it's Active with next leaf 000101.
- **Given** a vendor payment with method Cheque and `finance.cheques.issue`, **when** the payment is approved/posted, **then** the next leaf is assigned, an outgoing cheque (register no. `CHO-YYYY-000000`) is created as Issued, and it can be printed on the pack's layout with the amount in words.
- **Given** the cheque date is in the future, **then** the payment posts Dr 2000 AP / Cr 2080 Post-dated cheques payable (not the bank) (Decision, 22 Oct 2026, BR-CHQ-11).
- **Given** a current-dated cheque, **then** the payment posts Dr 2000 / Cr Bank directly.
- **Given** a printed cheque, **when** it's printed again, **then** the reprint is audited.
- **Given** the payment is above the approval threshold, **then** no leaf is assigned until it's approved.

### CHQ-08 Outgoing cheque actions
**As an** accountant **I want** to mark outgoing cheques presented, stopped, voided or stale **so that** bank and payables stay correct.
Priority **M** · Points **3** · Sprint **9** · FIN-CHQ-FR-13, core Section 8.3

- **Given** an Issued post-dated cheque, **when** the daily job reaches its cheque date, **then** Dr 2080 / Cr Bank is posted once (source "Cheque maturity"), and the cheque stays Issued.
- **Given** a matured or current-dated cheque, **when** it's marked presented, **then** the status becomes Cleared with no further posting (bank line reconciled).
- **Given** a stop or void **before** the cheque date, **then** Dr 2080 / Cr 2000 is posted. **After** the cheque date, Dr Bank / Cr 2000.
- **Given** an Issued cheque and `finance.cheques.stop`, **when** I record a stop-payment, **then** the payment posting is reversed (per the rule above) and the bill's balance reopens.
- **Given** a spoiled leaf and `finance.cheques.void`, **when** I void it, **then** it's kept as Voided and the leaf number is never reused.
- **Given** an outgoing post-dated cheque whose date is within the entity's cheque-due alert days, **then** it appears in the "Due to be presented" alert so the bank balance can be checked.
- **Given** an Issued cheque older than the pack's validity period (UAE 6 months), **then** it's flagged Stale with an alert, and I can Re-issue (new leaf, old one Replaced) or Void it.

---

## Definition of Done (applies to every story)

1. API endpoint(s) with permission attribute, FluentValidation rules and ProblemDetails errors. The OpenAPI document is updated.
2. Angular screen(s) with ng-zorro following the shared components (page header, data table with search/filters/CSV/25 rows, status tags, info grid, Documents and History tabs where a detail page exists).
3. Unit tests for domain rules, plus integration tests (Testcontainers SQL Server) for each Given/When/Then marked **M**.
4. Tenant-isolation and permission tests for every new endpoint (TEN-07, USR-06).
5. Audit events written for every state change (TEN-09).
6. No product brand names in code, seed data or UI strings other than the configured org name.
7. Demo seed updated where relevant (spec §14.1 for the real estate template).

## Dependencies

| Story | Depends on |
|---|---|
| All | TEN-07 (isolation), USR-06 (permissions), TEN-09 (audit) |
| TEN-03 wizard | TEN-05, MD-04, MD-05, MD-10, MD-11, USR-01 |
| USR-07 business unit part | TEN-06 |
| AUTH-12 | AUTH-01, AUTH-02, USR-02 (memberships) |
| MD-12 | MD-11, TEN-05 |
| CUS-* | MD-07 (first customisable record type), MD-13 (list conventions), TEN-09 |
| CHQ-01, CHQ-02 | MD-07 (parties), MD-01 (currencies), MD-10 (CHQ/CHO number series), TEN-05 |
| LED-01 | MD-05 (accounts), MD-11 (periods), MD-12 (lock date), MD-01/02 (currencies, rates) |
| LED-03…10 | LED-01 |
| LED-10 | MD-11, LED-01, LED-08 |
| DOC-01…06 | LED-01, LED-02, MD-04 (tax codes), MD-07 (parties), MD-10 (INV/CN series) |
| AR-01…04 | LED-03 (bank accounts), DOC-02 (issued invoices) |
| AR-05…08 | AR-01, AR-02 |
| DOC-07, DOC-08 | LED-01, MD-04, MD-07, approval engine (core BR-APR-*) |
| DOC-09…11 | DOC-08, LED-03 |
| DOC-12 | DOC-02, DOC-08 |
| CHQ-03, CHQ-04 | LED-03, AR-01, AR-02, CHQ-01 |
| CHQ-05, CHQ-06 | CHQ-03, CHQ-04 |
| CHQ-07, CHQ-08 | DOC-09 (vendor payments), CHQ-01 |

## Open questions for this backlog

Resolved on 22 Oct 2026 and removed: Q-S1 (velocity 40, rebalanced plan above), Q-S7 (Ledger core and Receipts & AR inserted from sprint 5), Q-S8 (outgoing cheques are core, so CHQ-07/08 are Must), Q-S9 (sprint 1 uses only the legal entity created at signup), Q-S10 (FIN-DOC epic written) and Q-S6 (custom fields and cheques in sprints 8–9 accepted).

| # | Question | Default if no answer |
|---|---|---|
| Q-S2 | Lockout values (10 attempts / 15 min) and password policy (10 chars, 3 of 4 classes, 5 history): OK? | Use them |
| Q-S3 | Public signup (TEN-02) in v1 or sales-led only? | Sales-led in v1, signup behind a flag |
| Q-S4 | MFA mandatory for admins/approvers by default? | Optional, with an org setting to require it |
| Q-S5 | Chart of accounts pulled into sprint 2 (spec had it in sprint 3): OK? | Yes, the wizard and tax codes need it |
| Q-S11 | The VAT return (VAT201, §6.5) isn't in this backlog. The spec has it in its sprint 13. Keep it there, or add it right after FIN-DOC? | Keep with the reports sprint. FIN-DOC already stores the per-line tax data it needs |
| Q-S12 | LED-10 year-end close (Should) is a stretch in sprint 5. Keep it there, or move it nearer the first real year-end? | Keep as a stretch; move it if sprint 5 runs over |
| Q-S13 | Sprint 6 assumes the receipt purposes Rent and Security deposit are registered by the Real Estate module. Until that module exists, core offers only Receivable settlement / Advance. OK? | Yes |
