# HigherSquare 360: client demo video storyboard

Two videos, one per experience, with the same look:

| File | What it shows | Size | Target length |
|---|---|---|---|
| `HigherSquare360-Web-Demo.mp4` | The whole web journey: pricing page, registration, then every module | 1920x1080, 30 fps | about 7.5 min |
| `HigherSquare360-Mobile-Demo.mp4` | The Flutter phone app on an Android phone | 1080x1920 (phone in a branded frame), 30 fps | about 2 min |

The target was 5 + 2 minutes. Covering every module from pricing to the last industry module needs about 7.5 minutes of web, so each module gets a short, focused segment.

**Style:** animated title cards in brand colours (green `#4FA077`, teal `#2A8A93`, navy `#1F3A44`), section lower-thirds, a soft cursor with click ripples, smooth cross-fades between scenes and gentle zoom on key numbers. Voice-over: Microsoft neural voice *Andrew* (US English). Low background music that ducks under the voice. Captions are burned in at the bottom.

**Data:** the registration creates a brand-new workspace live. Because a new workspace is empty, the module tour then switches to the seeded **Demo** workspace, which has several months of real documents posted through the product's own workflows. All companies and people in the demo data are invented.

---

## Web video

| # | Scene | On screen | Voice-over |
|---|---|---|---|
| W1 | Opening title | Animated logo, "HigherSquare 360: one ERP for your whole business" | Meet HigherSquare 360: one system to run your finance, sales, purchasing, inventory and people, with industry modules you switch on when you need them. |
| W2 | Pricing page | Hero, product mock-ups, the web and mobile previews, plans with the monthly/annual switch | It starts on our website. Every plan is clear and up front: choose monthly or annual billing, see exactly which modules and how many users each plan includes, and preview the web and mobile apps before you sign up. |
| W3 | Registration | Choose a plan, fill in business name, workspace code, industry, country and currency, contact and password, accept the terms | Signing up takes a minute. Pick a plan, tell us your business name, industry, country and currency, and choose a workspace address. Your workspace is set up with a chart of accounts, taxes and settings for your country. |
| W4 | First sign-in | Sign in to the new workspace, first dashboard | Sign in, and your new workspace is ready to use straight away. |
| W5 | Bridge card | "A few months later: a workspace in full swing" | Now let's step into a company that has been running on HigherSquare 360 for a few months. |
| W6 | Dashboard | KPI cards, charts, alerts, global search, quick create | The dashboard gives you the whole business at a glance: sales, cash, receivables and payables, with alerts for what needs attention. Search finds any customer, item or document in a second. |
| W7 | Sales | Quotes, orders, a new invoice with lines and tax, posted, print preview | Sales flows from quote to order to invoice. Here we raise an invoice: pick the customer, add items, and tax is worked out for you. Post it, and the ledger is updated automatically. Every document prints cleanly with your branding. |
| W8 | Receivables | Record a receipt against the invoice, the customer's balance and statement | When the customer pays, record the receipt against the invoice. The customer's balance and statement update immediately. |
| W9 | Purchasing and inventory | Requisition, purchase order, goods receipt, supplier bill; stock levels and movements by warehouse | Purchasing follows the same path: requisition, purchase order, goods receipt and supplier bill. Stock levels move with every receipt and sale, warehouse by warehouse. |
| W10 | Finance and reports | Chart of accounts, journals, profit and loss, balance sheet, trial balance, budgets | Finance is built in, not bolted on. Every transaction lands in the general ledger, and your profit and loss, balance sheet and trial balance are always up to date. Set budgets and compare them with actuals. |
| W11 | Approvals | Approval inbox, approve a request | Approvals keep you in control: purchases, payments and leave can wait for the right person's sign-off. |
| W12 | HR and payroll | Employees, attendance, leave, a posted payroll run, payslip | HR and payroll run in the same place: employee records, attendance, leave and a monthly payroll that posts straight to the books. |
| W13 | Industry modules | Real estate (properties, units, leases, rent billing), Projects (board, timesheets), Point of sale terminal, CRM pipeline, Manufacturing (bills of materials, work orders), then a quick look at Services, Retail, Distribution and Fixed assets | Switch on the modules your industry needs. Real estate manages properties, leases and rent. Projects track tasks, time and billing. The point of sale terminal sells at the counter. CRM follows every lead and opportunity. Manufacturing plans materials and work orders. And there is more: services, retail, distribution and fixed assets. |
| W14 | Control and security | Modules page, users and roles, two-factor sign-in, Arabic right-to-left view | You decide which modules are on, who can see what, and how people sign in, with two-factor security. The whole app also works in Arabic, right to left. |
| W15 | Closing title | Logo, "Start your free trial", website | HigherSquare 360: your whole business, in one place. Start your free trial today. |

## Mobile video

| # | Scene | On screen | Voice-over |
|---|---|---|---|
| M1 | Opening title | Phone frame, logo | And your business goes wherever you go, with the HigherSquare 360 app. |
| M2 | Intro and sign-in | App intro, sign in, choose a workspace | Sign in securely, with two-factor if you use it, and choose your workspace. |
| M3 | Overview | Dashboard cards and charts, pull to refresh | The overview shows the same live numbers as the web dashboard. |
| M4 | Invoices, receipts, customers | Search invoices, open one, receipts list, a customer | Look up any invoice, receipt or customer in seconds, right from your phone. |
| M5 | Approvals | Approval list, approve a request | Approve requests on the go, so nothing waits for you to get back to your desk. |
| M6 | My HR | Leave balance, request leave, payslips | Employees can check their leave, request time off and see their payslips. |
| M7 | Notifications and modules | Notifications, the More menu with industry modules | Notifications keep you up to date, and every module you use is one tap away. |
| M8 | Closing title | Logo, "Available on Android and iOS" | HigherSquare 360, on the web and in your pocket. |

The voice-over lines above are the script. The final wording is in `script.json`, which the build reads.
