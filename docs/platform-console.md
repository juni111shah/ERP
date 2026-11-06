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

## AI assistant settings

`/platform/assistant` decides where the AI assistant appears and which AI services answer for it. Anyone on the platform team can look. Only the platform owner can change anything or run a test.

- **Owner:** `PlatformSetting.OwnerUserId`. It is set at startup from `Platform:FounderEmail`, and is kept when later restarts leave that setting out. With no owner yet and exactly one platform admin, that admin becomes the owner. With several, restart once with `-FounderEmail`. The owner cannot be removed from the team.
- **Services:** any service that uses the OpenAI-style `/chat/completions` format works by entering its address, model and key. The presets in `frontend/src/app/pages/platform/ai-providers.ts` are the one place in the code that names them.
- **Keys:** encrypted in `AiProviders.EncryptedApiKey` and never returned to a browser, written to the audit trail or put in the activity log. Changing a service's address requires pasting the key again, and addresses must be public HTTPS. **Delete** removes the key for good.
- **Order:** the top service answers first. A service answers for the public assistant or for client data only if its switch for that is on.

The assistants themselves are built in later steps, so these settings have no visible effect on clients yet.

### Trying it, limits and usage

- **Try it** talks to the connected services directly, even with the assistants switched off, using `POST /platform-admin/assistant/try`. Replies arrive as server-sent events (`AssistantStreamResult`). Nothing is saved, and only the owner can use it.
- **Moving between services:** `AssistantRunner` tries each service in order. It moves on only while nothing has been shown, so a reply is never answered twice. A service that reports its limit is skipped until the wait it asked for has passed (60 seconds if it does not say, one hour at most). This is held in memory, so a restart clears it.
- **Daily limits** are in tokens per assistant, counted per UTC day in `AssistantUsageDays`, and include replies the person stopped early. A service that reports no usage is estimated at four characters a token. At the limit the assistant stops until the next day.
- **Tools:** at most `AssistantRunner.MaxToolCallsPerTurn` per reply. Each tool must check for itself that the caller may see what it returns.
