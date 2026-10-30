# ERP mobile (Flutter)

The phone app for the ERP. It talks to the same API as the web app (`backend/src/ERP.Api`) and changes nothing outside this folder.

## What is in it

| Area | Notes |
|---|---|
| Sign-in | E-mail and password, authenticator code (2FA), "keep me signed in", workspace choice when the account has several, workspace switch in Settings |
| Session | Access token in memory; the refresh cookie (`erp_rt`) is kept in the Android Keystore / iOS Keychain and renewed automatically on a 401 |
| Overview | The same KPI cards, alerts and charts as the web dashboard (`GET /api/v1/dashboard`) |
| Invoices | Searchable, paged list and a detail screen (`/api/v1/finance/invoices`) |
| Receipts, Customers | Searchable, paged lists (`/api/v1/finance/receipts`, `/api/v1/parties`) |

Read-only for now. Creating and editing documents, push notifications, offline mode and Arabic/RTL are the next steps.

## Run it

```bash
cd mobile
flutter pub get
flutter run
```

The API address defaults to `http://10.0.2.2:5080` on the Android emulator (that is your PC's `localhost`) and `http://localhost:5080` elsewhere.
On a real phone, open **Server** on the sign-in screen and enter your PC's address (`http://192.168.x.x:5080`) or the staging/production URL.
The dev API is started with `./run-dev.sh` (demo user `admin@demo.local`, see that script for the password).

Plain `http://` works in debug builds only (Android `usesCleartextTraffic` in the debug manifest; iOS allows local-network http only).
Staging and production must use `https://`.

First time on Android: `flutter doctor --android-licenses` (accepting the licences is a step for you, not the tooling).

## Check it

```bash
flutter analyze
flutter test
```

The tests use a scripted server (`test/support/fake_adapter.dart`) that answers in the API's real shape
(`{success, statusCode, data}`, with list paging in a sibling `meta`). That shape was confirmed against a running API.

## Layout

```
lib/
  core/       api client (token + cookie + refresh), session, models, secure store, formatting
  features/   auth, dashboard, documents (invoices, receipts, customers), settings, home shell
  app.dart    routes and theme
test/         api client, session, models/format and sign-in screen tests
```
