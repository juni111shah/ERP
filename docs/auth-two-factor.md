# Two-factor sign-in (TOTP)

Staff sign-in (web and mobile) uses an authenticator app (Google Authenticator, Microsoft Authenticator, 1Password, ...) as a second step.

## Flow

1. `POST /api/v1/auth/login` with email and password.
   - Account without a confirmed authenticator and `Auth:TwoFactor:Enforce` on: **no session**; the answer is
     `{ twoFactorSetupRequired: true, challengeToken, challengeExpiresIn }`.
   - Account with two-factor on: `{ requiresTwoFactor: true, twoFactorRequired: true, challengeToken, challengeExpiresIn }`.
   - Otherwise the normal session (or the workspace choice).
2. First sign-in only: `POST /api/v1/auth/login/2fa/setup` `{ challengeToken }` creates the key and returns
   `{ otpAuthUri, secret, qrCodeDataUri, issuer, account, digits: 6, period: 30 }`.
   `otpAuthUri` is `otpauth://totp/<Issuer>:<email>?secret=<base32>&issuer=<Issuer>&digits=6&period=30` (SHA1).
   `qrCodeDataUri` is a PNG data URI of that link; `secret` is the key in groups of four for typing in by hand.
3. `POST /api/v1/auth/login/2fa/verify` `{ challengeToken, code }` checks the code and issues the normal session
   (access token + refresh cookie, or the workspace choice). After a first-time setup it also turns two-factor on and
   returns `recoveryCodes` (shown once). Returning users may send a recovery code instead of a TOTP code.

Rules: codes are accepted one 30-second step either side of the server clock; a step that was already used is refused (no replay);
wrong codes count towards the Identity lockout (5 tries, then HTTP 423 for the lockout period); both calls share the `login` rate limit.
The challenge token is short-lived (`ChallengeMinutes`, default 10), scoped to its purpose (setup or verify), signed and encrypted with
ASP.NET Data Protection, and void once the password changes. It is not an access token. Keys and codes are never logged.
Storage: the authenticator key and the last used step live in `AspNetUserTokens` (no schema change).

## Configuration

```json
"Auth": {
  "TwoFactor": { "Enforce": true, "Issuer": "ERP", "ChallengeMinutes": 10 }
}
```

- `Enforce` defaults to **true** in code and in `appsettings.json` (production and staging). `appsettings.Development.json` sets it to
  `false` so local development and demos sign in with a password only. Accounts that already turned two-factor on are always asked
  for a code, whatever the flag.
- Turn it on locally: set `"Enforce": true` in `appsettings.Development.json`, or start the API with the environment variable
  `Auth__TwoFactor__Enforce=true` (PowerShell: `$env:Auth__TwoFactor__Enforce = 'true'; dotnet run --project src/ERP.Api`).
- `Issuer` is the name the authenticator app shows above the account; set it per deployment.
