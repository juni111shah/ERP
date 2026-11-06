# AI assistants

There are two assistants, sharing one engine and nothing else. Both are off until the platform owner turns them on at `/platform/assistant`.

| | Public assistant | Workspace assistant |
|---|---|---|
| Who | Visitors on the pricing, terms, privacy and register pages | Signed-in staff in a workspace |
| Knows | Public facts only | What that person may see |
| Turned on by | Platform owner | Platform owner, then each workspace's administrator (module `assistant`), then the `assistant.chats.use` permission |
| Stored in | `BrandConversations`, `BrandMessages`, `BrandLeads`, belonging to no workspace | `ast_Conversations`, `ast_Messages`, `ast_PendingActions`, scoped by OrgId and private to their owner |
| Kept for | Chats 30 days. Leads until deleted | 90 days by default (`assistant.retentionDays`) |

Settings for services, keys, limits and usage are described in [platform-console.md](platform-console.md#ai-assistant-settings).

## How a reply is made

`AssistantRunner` (`ERP.Application/Assistant`) checks the daily limit, tries the owner's services in order and runs tools. It never logs message text. Replies stream as server-sent events through `AssistantStreamResult`.

- **The model writes the words; the server supplies the numbers.** Price cards come straight from the price list. `recommend_plan` is arithmetic in code. Reports and lists come from the same queries as the screens.
- **Code answers when it can.** Public facts are written into the prompt as plain sentences (`BrandAssistant.FactSheet`), and the best matching written answers are added by keyword search. If every service fails or the limit is reached, the visitor still gets the written answer in their language, plus the contact form.
- **Public facts have one source.** `GetPublicContent` feeds both the pricing page and the assistant, so they cannot disagree. Change the FAQ in `PublicContent.cs`, not in the page.

## Workspace tools and safety

In-process calls skip the web layer's module and permission checks, so these rules live in the tool layer:

- `AssistantCatalog` decides what this person may use, checking both the module and the permission. To add something, register an `IAssistantReader` or `IAssistantAction` from your module's `ConfigureServices`. The core never names an industry.
- A reader returns safe fields only. No contact details, bank details or identity numbers. HR and payroll have no reader on purpose.
- Tools never take an organisation id. Tenant and legal-entity scope come from the signed-in token and the normal query filters.
- **The model cannot change data.** `propose_action` only stores a suggestion and shows a card built from the server's preview. The model never sees the suggestion's id. The change runs only from the Confirm button, as a fresh request that passes the whole pipeline, including the view-only block. It sends the same command as the screens, so validation, numbering and approval rules still apply, and the change itself is audited as usual. A suggestion expires after 15 minutes.
- **Records are treated as data.** A hostile customer name can at worst cause a suggestion that the person sees and can refuse. There are no web tools, and the reply renderer has no HTML, no images and no links off the site (`parseAssistantText`), so there is nowhere to send data.
- Chat text is kept out of the audit trail (`INotAudited`), because that trail is immutable.

Confirmed changes available today: `create_party`, `create_draft_quote` (Sales module) and `log_customer_note` (CRM module). Keep payments, posting, deletion, payroll, users, roles and billing out.

## Abuse and cost

Public: 30 questions an hour per address (`RateLimiting:AssistantPerHour`), 20 questions per chat, 1,000 characters per question, a hidden honeypot field, and the daily token limit as the backstop.

Workspace: 30 messages per person per day (`assistant.dailyMessagesPerUser`), a monthly allowance for the whole workspace set on each plan (`AssistantMonthlyMessages`: empty means no limit, 0 leaves the assistant out of the plan), and its own daily token limit. A workspace with no plan has no monthly limit. Messages are counted in `ast_MessageCounts`, apart from the chats, so deleting a chat cannot reset a limit. People with `assistant.usage.read` see the month's count in the panel. It is only a count: nobody can read another person's chats.

## Help articles

`search_help` answers how-to questions from short articles. Each module owns its own (`IAssistantHelp`, registered from `ConfigureServices`), and core has a pack for the screens every workspace has, so the core never holds an industry's words. An article is hidden when its module is off or the person lacks its permission. Search is by shared words; the model translates the question to English first. When a screen changes, change its article too.

## Audit link

The audit rows from a confirmed change carry `CorrelationId` = `assistant-conversation:<id>`, so a change can be traced to the chat it came from. The chat's words stay out of the trail.

## Mobile

The phone app has the same assistant and uses the same API, including Confirm and Cancel cards. See `mobile/README.md`.

## Checking quality

`tools/assistant-eval/run.mjs` asks real questions through the public API and checks prices against the live price list, plus the refusals and the language. It uses real tokens, so run it after changing the prompt, the facts or the model. Automatic checks are a floor: have a person read the replies, and a native speaker read the Arabic, before launch.

## Before going live

1. Final prices, company details and the About text in the console.
2. Have the privacy wording and the Arabic reviewed. The starter privacy text now mentions the assistant, but a founder who has edited their policy must add this themselves.
3. Read the provider's terms for commercial use on a free plan. For client data, turn on zero data retention, and only then switch on "Clear for client data".
4. Use the named Cloudflare tunnel, and confirm the rate limit sees real visitor addresses rather than one shared address.
