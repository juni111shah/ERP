# 02 · Forgot password, reset and change password

Routes: `/auth/forgot-password`, `/auth/reset-password?token=…`, `/auth/change-password` (signed-in, also used for a forced change after a temporary password).
Spec: §4.1 (no self-service in the demo), §4.26 (admin "Reset password" issues a temporary password), §4.31 (Change password was not captured: "implement current password, new password and confirm"), §8 #3, §9 "Self-service password reset", §12 `/auth/forgot`, `/auth/reset`, `/auth/change-password`.

All three screens reuse the login split layout and card (01-login.md).

**Approved policy (coordinator):**
* **Password:** minimum 10 characters, with at least one upper-case letter, one lower-case letter and one digit, and not equal to the email.
* **Reset tokens:** single-use, and they expire after **30 minutes**.
* **Lockout:** 5 failed sign-in attempts pause sign-in for 15 minutes (shown on login, 01 §A). A successful reset **clears the lockout** (approved). Failed change-password attempts **count toward** the lockout (approved).
* **Resend cooldown:** 30 s (approved).

Standard helper text (the same on every password-setting screen): "At least 10 characters, with upper- and lower-case letters and a digit. Can't be your email." 

## A. Request reset — `/auth/forgot-password`

```
┌──────────────────────────────┐
│ ← Back to sign in            │
│ Reset your password          │
│ Enter the email you sign in  │
│ with and we'll send a link.  │
│ Email                        │
│ [✉ you@company.com        ]  │
│ [     Send reset link     ]  │
└──────────────────────────────┘
```

| Label | Type | Control | Req. | Validation | Placeholder / help |
|---|---|---|---|---|---|
| Email | email | `nz-input-group` (mail icon) | Yes | Valid email format | Prefilled from the login screen when available |

| Action | Kind | Behaviour |
|---|---|---|
| Send reset link | Primary block | `POST /auth/forgot`. The UI **always** shows the confirmation, whether or not the email exists, so accounts cannot be enumerated. |
| Back to sign in | Link | `/auth/login` |

States:

| State | Display |
|---|---|
| Default | Email field focused. |
| Loading | Button spinner. |
| Validation | Inline field errors. |
| Rate limited | Alert with countdown (same pattern as login). |
| Network error | Warning alert with **Try again**. |
| Success | Confirmation view (C1). |

### C1 · Confirmation (check your email)
```
┌──────────────────────────────┐
│   (✉ icon, nz-result info)   │
│ Check your email             │
│ If an account exists for     │
│ j***@company.com, we sent a  │
│ link. It expires in 30 min   │
│ and works once.              │
│ [ Back to sign in ]          │
│ Didn't get it? Resend (0:30) │
└──────────────────────────────┘
```
This view uses `nz-result nzStatus="info"`. **Resend** is a link that stays disabled for 30 s, with a countdown. Sending a new link invalidates the previous one (single-use tokens). The token lifetime is **30 min (approved)**. The 30 s resend cooldown is approved.

## B. Set new password — `/auth/reset-password?token=…`

```
┌──────────────────────────────┐
│ Choose a new password        │
│ for j***@company.com         │
│ New password                 │
│ [••••••••••            👁 ]  │
│ ▓▓▓▓▓▓░░░ 4 of 5 met         │
│ ✓ At least 10 characters     │
│ ✓ An upper-case letter       │
│ ✓ A lower-case letter        │
│ ✗ A digit                    │
│ ✓ Not the same as your email │
│ Confirm new password         │
│ [••••••••••            👁 ]  │
│ [ ] Sign me out of other devices │
│ [      Update password      ]│
└──────────────────────────────┘
```

| Label | Type | Control | Req. | Validation | Help |
|---|---|---|---|---|---|
| New password | password | `nz-input-group` with eye toggle, `autocomplete="new-password"` | Yes | **Approved policy:** ≥ 10 characters, ≥ 1 upper-case letter, ≥ 1 lower-case letter, ≥ 1 digit, **not equal to the email** (kept per QA-5). Client and server validate the same rules. The rules are read from `GET /auth/password-policy` (**proposed**) so the checklist never drifts from the server. | Helper text (above). A live **strength checklist** under the field has 5 items (At least 10 characters / An upper-case letter / A lower-case letter / A digit / Not the same as your email), as `role="list"`, with each item showing ✓/✗ and the text "met"/"not met". A progress meter (`nz-progress nzSteps=5`) counts the rules met ("4 of 5 met"), so colour is never the only signal. Error on submit: "Your password needs: a digit." (lists the unmet rules). |
| Confirm new password | password | same | Yes | Must equal New password | "Passwords don't match" |
| Sign me out of other devices | checkbox | `nz-checkbox` | No (optional, QA-5) | — | **Optional; default unchecked**, the user decides. When checked, other refresh tokens are revoked (RefreshToken table, spec §10.4). The current session (or the reset session) always continues. |

| Action | Kind | Behaviour |
|---|---|---|
| Update password | Primary | `POST /auth/reset {token, password}`. On success it shows C2. |

States:

| State | Display |
|---|---|
| Token checking | `nz-skeleton` card while the token is pre-validated (**assumed** `GET /auth/reset/validate?token`, proposed). |
| Token invalid, expired (> 30 min) or already used | `nz-result nzStatus="warning"` reads "This link has expired or was already used. Reset links work once and expire after 30 minutes." with the primary action **Request a new link** (→ A, email prefilled when known). |
| Token expires while the form is open | On submit, the server rejects the token and the same warning result replaces the form. **Assumption:** there is no client-side timer, because the token's issue time isn't exposed. |
| Validation | Inline errors and the checklist. |
| Success | C2. |

### C2 · Password updated
`nz-result nzStatus="success"` reads "Password updated. You can now sign in with your new password." Primary **Sign in** (the email is prefilled). Any sign-in lockout on the account is cleared. There is no auto sign-in. **Assumption:** this keeps the flow simple and safe. If the product owner prefers, the user can be signed in automatically.

## C. Change password — `/auth/change-password` (signed in)
Opened from **Avatar menu → Change password** (spec §4.31) as an `nz-modal` (width md), **or** as a full page when forced after an admin-issued temporary password (spec §4.26 "Reset password").

| Label | Type | Control | Req. | Validation |
|---|---|---|---|---|
| Current password | password | `nz-input-group` + eye | Yes (hidden when forced via a temporary password, because the token proves possession) | Server-verified. Error: "Current password is incorrect." |
| New password | password | as in B (same helper text and 5-item checklist) | Yes | Approved policy, and must differ from the current password |
| Confirm new password | password | as in B | Yes | Match |

Actions: **Cancel** (secondary; hidden when forced) and **Change password** (primary, `nzLoading`). On success the toast reads "Password changed". Other sessions are revoked.

Wrong current password: failures here **count toward** the same 5-attempt / 15-minute lockout (approved). After the 5th failure the modal shows `nz-alert error` "Too many attempts. Try again in 14:32." (generic, as on login) with a countdown, and the form is disabled.

Forced-change state: the page is shown without the app shell, with the banner `nz-alert info` "Your administrator issued a temporary password. Choose a new one to continue." The user cannot navigate away except via **Sign out**.

## D. Admin-issued temporary password (from Users, see 04)
This flow is kept as an admin recovery path (spec §4.26). The result dialog was not captured in the spec (§8.1), so it is designed in 04-users-roles-permissions.md §User actions.

## Accessibility
* The confirmation and success views move focus to the result title (`tabindex="-1"`).
* Each checklist item has text such as "met" or "not met". Changes are announced politely and debounced.
