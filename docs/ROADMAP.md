# Roadmap

| Sprint | Scope |
| --- | --- |
| 0 (this) | Core platform: tenancy, legal entities, business units, auth (multi-workspace, refresh rotation, lockout, reset), roles and permissions, module system with per-workspace enablement, onboarding with industry templates, GL/party/item/tax/currency schema, numbering, approvals engine, attachments, audit, notifications, settings, custom fields, generic repository + paging + API envelope, Angular shell (login, workspace picker, shell, parties, custom fields, modules). |
| 1 | Master data screens (items, accounts, tax codes, legal entities, users, roles), custom-field filters/sort in lists, identity reads behind Application services. **Real estate portfolio and leasing (done):** unit types, properties, units (lifecycle, bulk create, rent history), leases (activation, rent schedule, renewal, termination, expiry job), work orders, deals. |
| 2 | Finance: journals and posting rules, invoices and bills, receipts/payments, cheques lifecycle with approvals, reports (trial balance). |
| 3 | Real estate on top of Finance: billing run (rent invoices), receipts and deposits, cheque links, credit notes on termination, deal commission invoicing and payouts, owner statements, reports; Sales and Purchasing modules. |
| 4 | Inventory (stock moves, valuation), POS/Retail (till sessions: `retail.tillsessions.create`). |
| 5 | HR & Payroll (aggregated postings per run), Projects. |
| 6 | Manufacturing, CRM, country packs (VAT returns, e-invoicing). |
