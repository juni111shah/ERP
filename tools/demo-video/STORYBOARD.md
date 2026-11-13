# HigherSquare 360: client demo video storyboard

Two videos with the same look:

| File | What it shows | Size | Length |
|---|---|---|---|
| `HigherSquare360-Web-Demo.mp4` | The whole web journey: pricing page, registration of a new workspace, first sign-in, then every module of a workspace that has been running for months | 1920x1080, 30 fps | about 8.5 min |
| `HigherSquare360-Mobile-Demo.mp4` | The Flutter phone app on an Android phone, shown in a phone frame on a branded stage | 1920x1080, 30 fps | about 2 min |

The first brief said 5 + 2 minutes. Covering every module, from the pricing page to the last industry module, takes about 8.5 minutes of web, so each module gets a short, focused segment.

**Style:** animated title cards in brand colours (green `#4FA077`, teal `#2A8A93`, navy `#1F3A44`), section labels that slide in, a soft cursor with click ripples, a spotlight on key numbers, burned-in captions. Voice-over: Microsoft neural voice *Andrew* (US English). Music: "Upbeat Corporate" by AtlasAudio (Pixabay), about 12 dB under the voice and lower still while the voice speaks.

**Data:** the registration creates a brand-new Qatari workspace live ("Pearl Coast Trading", country Qatar, currency QAR). A new workspace is empty, so the module tour then moves to the seeded Qatari company **Al Rayyan Trading**, which has months of documents posted through the product's own workflows. The word "demo" never appears on screen: `ci/rebrand.sql` renames the seeded workspaces, sign-in keys, e-mail addresses, people and memos before recording. All companies and people are invented.

**Logo:** the web app, the title cards and the phone stage all use the same vector mark as the phone app (`frontend/public/highersquare-mark.svg`, a copy of `mobile/assets/brand/hs360-mark.svg`).

The spoken words are in [script.json](script.json). Each line below is one narration beat; it starts when its action starts on screen.

## Web video

| # | On screen | Voice-over |
|---|---|---|
| Opening | Animated title card | Meet HigherSquare 360: one system to run your finance, sales, purchasing, inventory and people, with industry modules you switch on when you need them. |
| Pricing | Hero and product mock-ups | It all starts on our website. HigherSquare 360 brings your finance, sales and teams into one place, so everyone works from the same numbers. |
| | Plans, monthly/annual switch | Pricing is simple and up front. Choose monthly or annual billing, and see exactly how many users and which modules each plan includes. |
| | Mobile app section | You can even preview the mobile app before you sign up. |
| Registration | Choose the Business plan | Signing up takes about a minute. Pick a plan, here the Business plan, with a fourteen-day free trial. |
| | Business name; workspace code fills in | Enter your business name, and your workspace address is filled in and checked for you. |
| | Industry (Wholesale) and country (Qatar) | Choose your industry and country. The currency, taxes and chart of accounts are set up to match. |
| | Contact, password, terms, "Your workspace is ready" | Add your name, work email and a password, accept the terms, and create your workspace. |
| First sign-in | Sign in, the new dashboard ("Welcome, Mariam Al Kuwari") | Your workspace is ready straight away. Sign in, and you land on your own dashboard, ready for your first customers, products and invoices. |
| Bridge | Title card "A few months later" | Now let's step into a company that has been running on HigherSquare 360 for a few months. |
| Dashboard | Executive highlights | The dashboard shows the whole business at a glance. The executive highlights show portfolio value, receivables, and your net result for the period. |
| | Charts, alerts | Charts compare what you billed with what you collected, and age what customers still owe. Alerts flag anything that needs your attention. |
| | Global search for "Apex" | And global search finds any customer, item or document in a second. |
| Sales | Quotes | Sales flows from quote, to sales order, to invoice, and each step carries over to the next, so nothing is typed twice. |
| | Invoice totals | The invoices page shows what you invoiced this year, what is outstanding, and what is overdue. |
| | New invoice for Apex Global Trading | Here we raise a new invoice. Pick the customer, describe the work, enter the quantity and price, and choose the tax. The totals are worked out for you. |
| | Save and issue | Save and issue it, and the invoice is numbered, posted to the general ledger, and ready to print or email with your branding. |
| Receipts and customers | Receipts | When the customer pays, record the receipt, and see every receipt in one list. |
| | Customer page | Open the customer to see their total sales, what they owe, their payment terms, and the full history of invoices and receipts. |
| Purchasing and inventory | Requisitions | Purchasing follows the same path. A requisition asks for what is needed. |
| | Purchase order | It becomes a purchase order to the supplier, with every line tracked from ordered, to received, to billed. |
| | Goods receipts | Goods receipts bring the stock in, and supplier bills are matched and scheduled for payment. |
| | Stock on hand | Stock on hand shows every item by warehouse, with quantity, average cost and value, updated by every receipt and every sale. |
| Finance | Chart of accounts, journals | Finance is built in, not bolted on. Here is the chart of accounts, and the journals that every sale, purchase and payroll posts automatically. |
| | Trial balance, income statement, balance sheet | Reports are always up to date: the trial balance, the income statement and the balance sheet, for any period, in a click. |
| | Budgets | Set budgets for the year, and compare them with what actually happened. |
| Approvals | Request approval for a delivery van | Approvals keep you in control. Anyone can request approval for a purchase, a payment or an expense, |
| | Submitted, under My requests | and it waits for the right person's sign-off, on the web or on their phone. |
| HR and payroll | Employees | HR and payroll live in the same place. Every employee has a record with their role, their department and their pay. |
| | Leave requests | Attendance and leave are tracked day by day, and leave requests go through approval. |
| | Payroll runs, November run | Payroll runs every month in a few clicks. Each run shows gross pay, deductions and net pay for every employee, and posts straight to the books. |
| Industry modules | Title card | Then, switch on the modules your industry needs. |
| | Properties, leases | Real estate manages properties, units and occupancy, with leases and rent billing. |
| | Task board, timesheets | Projects track tasks on a board, with timesheets and project billing. |
| | POS terminal | The point of sale terminal sells at the counter, from your live product list and stock. |
| | Sales pipeline | CRM follows every lead and opportunity through your sales pipeline. |
| | Bills of materials, work orders | Manufacturing plans bills of materials and work orders. |
| | Engagements, stores, routes | And there is more: services and retainers, retail stores and price lists, and distribution routes with van sales. |
| Control and security | Modules | You decide which modules are switched on for your business. |
| | Users | Users and roles control who can see and do what. |
| | Two-factor authentication | Everyone can protect their sign-in with two-factor authentication. |
| | Arabic, right to left | And the whole app also works in Arabic, right to left. |
| Closing | Title card | HigherSquare 360. Your whole business, in one place. Start your free trial today. |

## Mobile video

| # | On screen | Voice-over |
|---|---|---|
| Opening | Title card | And your business goes wherever you go, with the HigherSquare 360 app. |
| Secure sign-in | Intro tour, sign-in, choose workspace, first tips | A short tour shows what the app can do. Then sign in securely, with two-factor if you use it, and choose your workspace. |
| Overview | Highlight cards, finance filter, charts | The overview shows the same live numbers as the web dashboard: sales, cash, receivables and alerts, right on your phone. |
| Invoices, receipts, customers | Invoice list and detail, receipts, a customer | Look up any invoice, receipt or customer in seconds. Open an invoice to see every line, its status and what is still owed. |
| Approvals on the go | Approvals, My requests | Approve requests on the go, so nothing waits for you to get back to your desk. |
| HR on the go | Employees and payroll runs, opened through the app's search | HR travels with you too: look up any employee, and review each month's payroll run, right from your phone. |
| Every module | Notifications, the More menu | Every module you use is one tap away, from real estate and projects to point of sale and manufacturing. |
| Closing | Title card | HigherSquare 360. On the web, and in your pocket. |
