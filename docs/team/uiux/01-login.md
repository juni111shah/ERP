# 01 · Login and workspace picker

Routes: `/auth/login`, `/auth/select-workspace` (public until a workspace is chosen). Sign-up is in **01a-sign-up.md**.
Spec §4.1, §7.3, §8 #1 #14, §10.3–10.4, §12 (Auth). Coordinator decisions applied: **sign in by email**, one email can belong to **several workspaces**, a **workspace picker after login** when there is more than one, and the password and lockout policy below.
UI word: **Workspace**. Backend name: `Organization` (00 §4).

## Policy (approved)
| Rule | Value | Where it shows in the UI |
|---|---|---|
| Password | Minimum 10 characters, with at least one upper-case letter, one lower-case letter and one digit, and **not equal to the email** | 01a sign-up and 02 reset/change: checklist and helper text. Login does **not** check strength. |
| Lockout | **5 failed attempts → sign-in is paused for 15 minutes.** Failed *change-password* attempts count too. A successful password reset clears the lockout. The UI shows only a generic message and the retry time, never the account's state. | Login §A "Locked out" (countdown); 02 §C |
| Email verification / resend | Verification link valid for **24 h**. **30 s** cooldown on every resend link (verification, reset, invite). | 01a, 02 |
| Account existence | Never revealed by sign-up, reset, lockout or invite responses | 01a, 02, 04 |
| Reset token | Single-use, expires after **30 minutes** | 02 |

## A. Sign in — `/auth/login`

### Layout (≥ md: split screen, as in spec Figure 9)

```
┌───────────────────────────────────┬──────────────────────────────────────┐
│  BRAND PANEL (--erp-color-hero-*)│                                      │
│  ◆ <Product name>                 │   ┌──────────────────────────────┐   │
│  EYEBROW (tagline, uppercase)     │   │ Sign in                      │   │
│                                   │   │ [!] nz-alert (errors/lockout)│   │
│  Headline (1–2 lines)             │   │ Email                        │   │
│  Product description sentence     │   │ [✉ you@company.com        ]  │   │
│                                   │   │ Password                     │   │
│                                   │   │ [🔒 ••••••••          👁 ]   │   │
│                                   │   │ [ ] Keep me signed in        │   │
│                                   │   │              Forgot password?│   │
│                                   │   │ [        Sign in        ]    │   │
│                                   │   │ ─────────────────────────    │   │
│                                   │   │ New here? Create a workspace │   │
│                                   │   └──────────────────────────────┘   │
└───────────────────────────────────┴──────────────────────────────────────┘
< md: brand panel collapses to a compact top band (logo + tagline); card full width.
```

The brand-panel copy is **configurable per product/white-label**. The spec's real-estate copy is an example only. The core default copy is industry-neutral, e.g. "Run every part of your business in one place."
There is **no workspace field or selector** on the login card (QA-3). The workspace picker (§B) appears only **after a correct password** (and 2FA), never before.

### Fields
| Label | Type | ng-zorro control | Req. | Validation | Placeholder / help |
|---|---|---|---|---|---|
| Email | email | `nz-input-group [nzPrefixIcon]="'mail'"` + `input type=email autocomplete="username"` | Yes | Valid email format. Trimmed and lower-cased. | "you@company.com" |
| Password | password | `nz-input-group [nzPrefixIcon]="'lock'" [nzSuffix]` (eye toggle `button`, `aria-label="Show password"`, `aria-pressed`) | Yes | Not empty. No strength check at login. | — |
| Keep me signed in | checkbox | `label nz-checkbox` | No | — | Tooltip: "Keeps you signed in on this device for 30 days." (QA decision; the spec §7.3 suggested 14, and the refresh-token lifetime must be set to 30 days to match.) **Assumption:** when unchecked, the session ends when the browser closes. |

### Actions
| Action | Kind | Behaviour | Permission |
|---|---|---|---|
| Sign in | Primary, full width (`nzBlock`, `nzLoading`) | `POST /auth/login {email, password}` returns the user and the list of their workspaces. Then: 2FA if required, then **0 workspaces** → §B empty state, **1 workspace** → enter it directly, **> 1** → `/auth/select-workspace`. After entering a workspace: load `/me`, then go to `returnUrl`, or to `/onboarding` if that workspace's setup is incomplete (and the user is its admin), or to `/`. | Public |
| Forgot password? | Link | `/auth/forgot-password`, carrying the email as a query param | Public |
| Create a workspace | Link | `/auth/sign-up`, carrying the email (01a) | Public |
| Show/hide password | Icon button | Toggles the input type. The focus stays in the field. | — |
| Sign in with SSO | Secondary (hidden by default) | Shown only if SSO is configured for the email's domain (spec §9 "Could") | — |

### States
| State | Display |
|---|---|
| Default | Email field focused. If the email is remembered, the password field is focused instead. |
| Loading (submit) | Button spinner with "Signing in…". Inputs read-only. |
| Field validation | Inline: "Enter your email", "Enter a valid email address", "Enter your password". |
| Invalid credentials (401), attempts 1–2 | `nz-alert error` "Email or password is incorrect." The message never reveals which field was wrong. The password is cleared and refocused. |
| Failures 3–4 | **No "attempts left" counter** (removed: a counter implies the account exists). The message stays "Email or password is incorrect." and adds a "Forgot password?" link inside the alert. |
| **Locked out** (5th failure, or locked when submitting) | `nz-alert error` with a **generic** message and the retry time only: "Too many attempts. Try again in **14:32**." Nothing says whether the account exists, is locked or is active. A live mm:ss countdown runs, from `Retry-After`. The **Sign in** button is disabled and labelled "Try again in 14:32". Email can still be edited, and changing it re-enables the form. **Forgot password?** stays available; a successful reset **clears the lockout** (approved). The same response and message are returned for unknown emails, so lockout cannot be used to enumerate accounts. |
| Rate limited (429, per IP) | `nz-alert` "Too many attempts. Try again in **0:45**." (same generic wording as lockout) with a countdown (fixes spec §8 #14). |
| Account deactivated (in every workspace) | Shown **only after a correct password**, so it never tells a stranger anything. `nz-alert error` "Your account is inactive. Contact your workspace administrator." |
| Email not verified (self sign-up) | Shown only after a correct password. `nz-alert warning` "Verify your email to continue." with **Resend verification email** (01a §B). |
| Temporary password | After sign-in, redirect to `/auth/change-password?forced=1` (02 §C) before the workspace picker. |
| 2FA required (spec §9 Must; later sprint) | The card swaps to a "Verification code" step: 6-digit `nz-input` with `autocomplete="one-time-code"`, **Verify**, and a "Use a recovery code" link. This step comes before the picker. |
| Session expired | `/auth/login?returnUrl=…&reason=expired` shows `nz-alert info` "Your session expired. Sign in again to continue." |
| Silent refresh on hard reload | Full-screen `nz-spin` while `/auth/refresh` runs. No blank page (spec §8 #1). |
| Server/network error | `nz-alert warning` "We couldn't reach the server. Check your connection and try again." |
| Success | Redirect (no toast). |

Accessibility: the form has `aria-labelledby` pointing to the "Sign in" heading. Alerts have `role="alert"`. The countdown is announced at most once a minute (`aria-live="polite"`); the visible timer updates every second.

## B. Workspace picker — `/auth/select-workspace`
Shown after the credentials (and 2FA) are accepted when the email belongs to **more than one** workspace. It is also reached from **User menu → Switch workspace** (07 §2), where it opens as an `nz-modal` with the same content.

```
┌──────────────────────────────────────────────────────────┐
│ Choose a workspace                                       │
│ Signed in as j.ahmed@acme.com · Not you? Sign out        │
│ [🔍 Search workspaces…]          (shown when > 6)        │
│ ┌──────────────────────────────────────────────────────┐ │
│ │ (A) Acme Group            Workspace admin   Last used ›│ │  ← nz-list item, button
│ ├──────────────────────────────────────────────────────┤ │
│ │ (B) Beta Trading LLC      Accountant                ›│ │
│ ├──────────────────────────────────────────────────────┤ │
│ │ (G) Gamma Services        [Setup in progress]       ›│ │
│ ├──────────────────────────────────────────────────────┤ │
│ │ (D) Delta Co              [Suspended]  (disabled)    │ │
│ └──────────────────────────────────────────────────────┘ │
│ PENDING INVITATIONS (1)                                  │
│ │ Omega Group invited you as Viewer  [Decline] [Accept] │ │
│ [ ] Always open my last-used workspace                   │
│ + Create a new workspace                                 │
└──────────────────────────────────────────────────────────┘
```

| Element | Control | Notes |
|---|---|---|
| Workspace list | `nz-list` of items, each a real `button` (or `a`) | Shows logo or initial `nz-avatar`, display name, the user's role(s) in that workspace (muted), a status tag when not active, and "Last used" on the most recent one. Sorted by last used, then by name. |
| Search | `nz-input` with search icon | Shown when there are more than 6 workspaces. Filters client-side by name. |
| Pending invitations | `nz-list` with **Accept** (primary small) / **Decline** (default small) | Shown only if there are invitations. Accept adds the workspace to the list and selects it. Decline needs an `nz-popconfirm`. **Assumption:** invitations to an existing email appear here, not only in email. |
| Always open my last-used workspace | `nz-checkbox` | Default off. When on, later logins skip the picker. The user can still switch from the user menu. |
| Create a new workspace | Link | `/auth/sign-up?existing=1`. An already signed-in user skips the account step and goes straight to the onboarding wizard (01a §C). |
| Sign out | Link | Clears the session and returns to `/auth/login`. |

Actions and behaviour:

| Action | Behaviour |
|---|---|
| Choose a workspace | `POST /auth/select-workspace {organizationId}` (**proposed**; the backend name is Organization) returns an access token scoped to that workspace (claims `org`, `perms`, `ents`). Then the app routes as in §A. |
| Keyboard | ↑/↓ moves between items (roving tabindex), Enter selects, and typing focuses the search. |

States:

| State | Display |
|---|---|
| Loading | 3 `nz-skeleton` list items with avatars |
| Selecting | The chosen item shows an `nz-spin` small. The other items are disabled. |
| Setup in progress | `nz-tag` "Setup in progress". Admins continue into `/onboarding`. Other users see `nz-result info` "Acme Group is still being set up by <admin name>." with **Back to workspaces**. |
| Suspended / no access | The item is disabled with a tag ("Suspended", "Access removed") and a tooltip "Contact the workspace administrator". |
| Exactly one workspace | The picker is skipped, unless it was opened from Switch workspace, where it shows the single item plus **Create a new workspace**. |
| No workspaces (user removed from all, or invitation declined) | `nz-result info` "You're not a member of any workspace yet." with the primary action **Create a workspace** and the secondary **Sign out**. A muted hint reads "If a colleague invited you, check your email for the invitation." |
| Error | `nz-alert error` "We couldn't open Acme Group. Try again." The list stays usable. |
| Switching from inside the app | The modal shows the current workspace with the tag "Current". Selecting another one runs the dirty-form guard first, then reloads the shell (full state reset) and lands on `/` of the new workspace. |
