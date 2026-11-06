# Platform console: registration, plans and billing

Two kinds of people use this ERP. **Clients** work inside their own workspace. The **platform team** runs the business of selling it, at `/platform`. The code says "workspace" and the console says "client" for the same thing.

## Who is on the platform team

`AppUser.IsPlatformAdmin`, read from the account on every console request, so removing someone works at once. The first owner comes from `Platform:FounderEmail` at startup (see [devops/operations.md](devops/operations.md#platform-owner-bootstrap)). More people are added under Team. Holding every permission inside a workspace does not make someone platform staff.

The team has an internal workspace (`Organization.IsInternal`, code `platform-hq`). It exists because every session needs a workspace. It is never billed or listed as a client.

## A client's life

| Status | Meaning | Client can |
|---|---|---|
| `trialing` | Free trial is running | Everything |
| `active` | Paid up, or free of charge | Everything |
| `pastDue` | An invoice is overdue | Everything during the grace days, then view only |
| `expired` | Trial ended with no plan | View only |
| `suspended` | Blocked by the founder, or automatically after the suspend-after days | Sign in and see billing only |
| `cancelled` | Ended by the founder. Data is kept | Sign in and see billing only |

A workspace with no subscription row is unrestricted. That covers the internal workspace and any created before billing existed. They appear in the console as having no plan, until a plan is assigned.

"View only" and "blocked" are enforced by `SubscriptionGateMiddleware`, which answers HTTP 402. Signing in, the session and `/billing` always work so a client can see why. No client data is touched by any of this.

## Registration

`SignupMode` in Settings decides what happens on `/register`:

- **Review** (the default): every registration waits for a person in Registrations.
- **Open**: a workspace is created at once, or as soon as the email is confirmed.
- **Closed**: the page shows a message and refuses.

With *Require email verification* on, the registrant confirms by a link first. If no email server is configured, nobody can confirm, so the registration goes to the review queue rather than through unverified. Passwords are stored as a hash on the request and cleared once it is handled.

A paid plan with no trial days starts running straight away and issues its first invoice.

## The public pages

The site's home address shows the pricing page (`/pricing`) to signed-out visitors, and the workspace to signed-in people, in any tab. The screens drawn on that page copy the real web Overview and mobile app by hand, with sample figures. They are not screenshots, because the repo holds none, so when a real screen changes noticeably, update `pricing-web-dash.component.ts` and `pricing-mobile.component.ts` to match.

`/terms` and `/privacy` show the wording from Settings. It starts as `LegalDefaults`, which is starter text that needs a lawyer's review before selling. The time someone accepted is kept on their registration.

## Billing

There is no payment gateway. Invoices are issued by the system and **payments are recorded by hand**. Adding a gateway later means calling the same payment handler (`RecordInvoicePayment`) from a webhook.

The daily run (`ISubscriptionLifecycle`, from `ScheduledJobRunner`, or "Run billing now") is safe to repeat. It:

1. Ends trials that are over.
2. Sends trial reminders 7, 3 and 1 days before the end, when email is on.
3. Issues the renewal invoice `RenewalLeadDays` before a period ends, due on the last day of the period.
4. Marks a client `pastDue` the day after an invoice is due.
5. Suspends after `SuspendAfterDays`.
6. Expires unconfirmed registrations after 48 hours.

Recording a payment extends the period and restores a client that lapsed. It does **not** lift a suspension the founder applied by hand, or a cancellation: those need Reactivate.

Things it deliberately does not do yet:

- One billing currency for the whole platform, set in Settings. Changing it does not convert old invoices.
- One tax rate for all clients.
- No proration: a plan change applies from the next invoice. Issue a one-off invoice if a difference is owed.
- The mobile app shows no billing screens. It gets the 402 answers like any other client.

## Plan limits

`ISubscriptionLimits` is checked when inviting or reactivating a user, creating a legal entity and enabling a module. Moving a client to a plan with fewer modules switches off the others. Their data stays and comes back with the module. Lowering user limits never removes anyone: it only stops new additions.

## Where the code is

- `ERP.Domain/Platform/Subscriptions.cs`: the tables. None is org-scoped, so workspace-side reads filter on the caller's `OrgId` explicitly.
- `ERP.Application/Platform/`: `SubscriptionCore.cs` (rules, limits, daily run), `RegistrationFeatures.cs`, `BillingFeatures.cs` (client side), `ConsoleFeatures.cs` (founder side).
- `ERP.Infrastructure/Services/PlatformServices.cs`: everything that reads across workspaces. `PlatformBootstrap.cs`: startup.
- `ERP.Api/Endpoints/PlatformAdminEndpoints.cs`: routes. The `platform-admin` policy sits on the whole group.
- Tests: `tests/ERP.IntegrationTests/PlatformConsoleTests.cs`, `tests/ERP.UnitTests/SubscriptionRulesTests.cs`.
