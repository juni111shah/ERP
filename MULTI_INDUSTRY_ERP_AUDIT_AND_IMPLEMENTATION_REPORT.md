# Multi-industry ERP: audit and implementation report

Status: **work in progress**. This first version holds the audit outcome and the risk-aware plan, committed before any major change. Sections are completed as the work lands, and the final version replaces this note with what was finished and what was not.

Date of audit: 2026-11-06. Base: `main` at 2c631b6. Working branch: `claude/multi-industry-erp`.

## Summary of the audit

The product is a modular core plus 13 modules and 7 country packs. Finance, real estate and HR are deep and well tested. The other modules are breadth-first. Tenant read isolation is sound. The largest gaps for a multi-industry product are:

1. **No industry engine.** `IndustryCode` is a label used once at sign-up. Nothing in navigation, dashboards or settings reads it, and it cannot be changed afterwards.
2. **Six of the twelve requested industries have no functionality:** healthcare, education, logistics, hospitality, agriculture, and the construction layer on top of projects.
3. **Connected workflows break at the seams:** partial billing of purchase orders, sales returns, negative stock through documents, manufacturing WIP journals, stranded project WIP.
4. **Missing core:** fixed assets, year-end close, cash-flow statement, expense claims, contracts, units of measure, consolidation, real multi-currency.
5. **Security:** one critical cross-tenant account takeover through pre-claimed accounts and four high findings inside a tenant.
6. **The test suite was red on `main`:** 65 integration failures, from one root cause (see below).

### A defect found while taking the baseline

`Directory.Build.props` set `LangVersion` to `latest`. With the installed .NET 10 SDK that selects C# 14, which binds `array.Contains(x)` inside expression trees to a span overload that EF Core 8 cannot evaluate. Those queries returned HTTP 500. It reached real endpoints (for example inviting a user), not only tests. The version is now pinned.

## Risk-aware plan

Principles: additive changes, no rewrites, nothing deleted, real estate and existing APIs preserved, core kept free of industry vocabulary.

### Decisions that need explaining

| Decision | Why | Risk and mitigation |
|---|---|---|
| Industry functionality for the six missing verticals is delivered by one **record engine** plus thin module definitions, not six hand-written modules | Six typed modules would duplicate the same lists, forms, status rules and billing, which the brief forbids. A definition gives each record type its fields, statuses, transitions, permissions, numbering, audit history, billing and stock consumption | Less depth than a purpose-built module, weaker reporting on JSON fields. Stated plainly per industry in the matrix. Modules can later replace a record type with typed tables without changing routes |
| The demo seeder runs **real command handlers** in process, not direct table writes | Accounting effects come from the real posting code, so balances are right by construction and defects surface instead of being hidden | Slower than bulk inserts. Dates are handled by a pinned clock (`AppClock`), reachable only from seeding code |
| Seeding is an **explicit command**, never automatic at start-up | Automatic seeding wrote 21,000 rows into seven workspaces on 2026-11-06 | `ProgramSeeder` is also narrowed to its two named demo workspaces |
| Schema changes are additive and reach `ERP_Dev` through one normal migration | Other sessions share the database and their code must keep working | New tables and nullable or defaulted columns only |
| Critical C1 (account pre-claim) is **not** rushed into this change | The correct fix needs invitation acceptance by email, which changes sign-up behaviour and depends on mail being on in production | Reported at the top of the security section with a design, proposed as its own piece of work |

### Phases

1. Baseline green: language version pin, ambient clock.
2. Security hardening: audit log secrets and field access, role escalation, company boundary on writes, cross-tenant references.
3. Workflow correctness: Procure to Pay, Order to Cash, inventory, Plan to Produce, projects, payroll, POS returns.
4. Finance core: year-end close, cash-flow statement, fixed assets.
5. Configuration engine: industry catalogue, business types, capabilities, module dependencies, numbering, units, categories, change of industry.
6. Industry modules on the record engine.
7. Owner dashboard from real transactions.
8. Demo organizations seeded and verified in `ERP_Dev`.
9. Tests, report, push.
