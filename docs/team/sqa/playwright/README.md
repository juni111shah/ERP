# ERP E2E (Playwright, TypeScript)

Starter E2E project owned by SQA. It is industry-agnostic: core journeys live in `tests/`, and module-specific journeys will go in `tests/modules/<moduleKey>/`.

## Prerequisites
- Node 20+
- The Angular app running locally (default `http://localhost:4200`) against an API seeded with Org A (spec §14.1), or a staging URL

## Setup
```bash
cd sqa/playwright
npm install
npx playwright install --with-deps chromium     # first time only
cp .env.example .env                            # fill in E2E_USER_PASSWORD (never commit .env)
```

## Run
```bash
npx playwright test --list                 # list tests (no browser needed)
npm run test:smoke                         # @smoke tests, all projects
npx playwright test --project=chromium     # PR gate (Chromium only)
npx playwright test --project=firefox --project=webkit --project=mobile   # nightly
npm run report                             # open the HTML report
E2E_BASE_URL=https://staging.example.app npx playwright test --project=chromium
```
- Tests that need credentials are **skipped** (not failed) when `E2E_USER_EMAIL` or `E2E_USER_PASSWORD` is unset.
- Features not built yet are `test.fixme` until their readiness flag is set: `E2E_KEEP_SIGNED_IN_READY`, `E2E_WORKSPACE_PICKER_READY`, `E2E_PASSWORD_RESET_READY`, `E2E_ONBOARDING_READY`.
- `tests/onboarding.industries.spec.ts` is data-driven over `tests/data/industries.ts` (real estate, trading & distribution, retail, manufacturing, services, other/general) and needs a test-only provisioning endpoint (`tests/support/provision.ts`).
- `E2E_START_SERVER=1` makes Playwright start `ng serve` from `E2E_WEB_DIR`.
- CI: `retries: 2`, and a test that passes on retry is reported as flaky (see `../test-strategy.md` §9).

## Layout
```
playwright.config.ts                 baseURL from E2E_BASE_URL (default http://localhost:4200), testIdAttribute=data-testid
tests/support/env.ts                 env vars + readiness flags (features stay test.fixme until their flag is 1)
tests/support/provision.ts           test-only workspace provisioning via /api/v1/test-support (DECIDED; API must run in the Testing environment)
tests/pages/login.page.ts            /auth/login (UX 01)
tests/pages/workspace-picker.page.ts workspace step after login (decision b, UX 01 "Workspace")
tests/pages/app-shell.page.ts        sider, header, user menu (UX 07)
tests/pages/password.page.ts         /auth/forgot-password, /auth/reset-password (UX 02)
tests/pages/onboarding.page.ts       5-step wizard /onboarding/* (UX 03)
tests/login.smoke.spec.ts            AUTH-001/004/015/029/050/053
tests/keep-signed-in.spec.ts         AUTH-019/070/071 (30-day persistent vs session-only)
tests/workspace-picker.spec.ts       WS-001/002/003/017
tests/password-reset.spec.ts         PWR-001/014
tests/onboarding.industries.spec.ts  CORE-021/022/028/029, parameterized over tests/data/industries.ts
```

## Selector policy
1. `getByTestId('…')` (attribute `data-testid`): stable and the first choice.
2. `getByRole` / `getByLabel` with an accessible name: used as a fallback via `.or()` so the tests also verify a11y. Accessible names follow the UX copy (e.g. "Sign in", "Keep me signed in", "Send reset link", "Update password").
3. Never CSS classes from ng-zorro (`.ant-*`) or XPath.

## data-testid contract for the Angular team
The UX docs (`/workspace/erp-team/uiux/`) define no test ids, so this list is the contract. Naming: kebab-case `<area>-<element>`; dynamic items `<area>-<kind>-<key>`; state goes in `data-state`, not in the id. Routes follow UX 08 (`/auth/*`, `/onboarding/*`, `/finance/*`, `/sales/*`, `/purchases/*`, `/admin/*`, `/real-estate/*`).

| Screen (UX doc) | data-testid | Element |
|---|---|---|
| Login `/auth/login` (01) | `login-email`, `login-password`, `login-password-toggle` (`aria-pressed`) | Inputs and eye toggle |
| | `login-keep-signed-in` | "Keep me signed in" checkbox (30 days, DECIDED) |
| | `login-submit` | "Sign in" |
| | `login-error` (`role="alert"`) | Error alert (401, locked, 429, network) |
| | `login-retry-after` | Countdown for 429 and lockout (`aria-live="polite"`) |
| | `login-forgot-password`, `login-change-workspace` | Links |
| | `login-session-expired` | Info alert for `reason=expired` |
| Workspace step (01, decision b) | `workspace-picker` (radiogroup), `workspace-option-<slug>`, `workspace-picker-continue` | Picker after login |
| Forgot `/auth/forgot-password` (02-A) | `forgot-email`, `forgot-submit`, `forgot-confirmation`, `forgot-resend` | |
| Reset `/auth/reset-password` (02-B) | `reset-new-password`, `reset-confirm-password`, `reset-submit`, `reset-invalid-token` | **No** `reset-signout-others` checkbox: a reset always signs out other devices (DECIDED, PWR-009b asserts it is absent) |
| | `password-rule-length`, `password-rule-case`, `password-rule-digit`, `password-rule-email` (`data-state=met\|unmet`) | Live policy checklist |
| Change `/auth/change-password` (02) | `change-current-password`, `change-new-password`, `change-confirm-password`, `change-submit` | |
| Shell (07) | `app-shell`, `sidebar-nav`, `nav-item-<routeKey>` | Layout and menu |
| | `shell-workspace-name` (`data-workspace=<slug>`) | Workspace identity at the sider top |
| | `entity-switcher`, `entity-option-<entityCode>` | Legal-entity switcher |
| | `notifications-bell`, `notifications-badge`, `global-search`, `theme-toggle` | Header |
| | `user-menu`, `user-menu-change-password`, `user-menu-signout` | User menu |
| | `session-ended-modal` | "Your session has ended" |
| Guards (08 §2) | `page-forbidden`, `page-module-disabled` | 403 and "This feature isn't enabled for your workspace" |
| Onboarding `/onboarding/*` (03) | `onboarding-continue`, `onboarding-back`, `onboarding-save-exit` | Wizard navigation |
| | `onboarding-display-name`, `onboarding-contact-email`, `onboarding-workspace-address`, `onboarding-workspace-address-status` | Step 1 |
| | `onboarding-industry-<key>` (incl. `onboarding-industry-other`) | Step 2 cards (`disabled` when coming soon) |
| | `onboarding-entity-name` | Step 3 |
| | `onboarding-module-<key>` (`data-state=checked\|unchecked\|checked-disabled\|plan-locked`) | Step 4 |
| | `onboarding-finish`, `onboarding-success`, `onboarding-go-to-dashboard` | Step 5 |
| Modules `/admin/modules` (05) | `module-row-<key>` (`data-state=enabled\|disabled\|unavailable`), `module-toggle-<key>` | Module management |
| Custom fields (custom-fields.md) | `cf-<key>`, `cf-display-<key>`, `cf-col-<key>`, `cf-filter-<key>` | Dynamic fields |
| Tables (06) | `table-<resource>`, `table-row-<id>`, `table-search`, `table-export-csv`, `table-refresh` | Lists |
