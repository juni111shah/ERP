# Real estate module: what is built

Module key `realestate` · UI `/real-estate/*` · API `/api/v1/m/realestate/*` · tables `re_*` · permissions `realestate.<resource>.<action>`.
Spec: `docs/team/ba/real-estate-module.md`, schema `docs/team/db/real-estate-schema.md`.

## Built (Portfolio, Leasing, Operations)
| Area | Behaviour |
|---|---|
| Unit types | CRUD; default usage and bedrooms; codes unique (stored upper-case). |
| Properties | Auto-numbered `PROP-0001`; owning legal entity locked once units/leases exist; "managed for owner" needs an owner party; KPIs (registered/occupied units, occupancy %, contracted rent, gross yield); total units never below registered units (grows automatically when units are added). |
| Units | Unique number per property; usage defaults from the unit type; **lifecycle** Vacant ⇄ Reserved, Unavailable (reason required), Occupied only through leases; asking-rent history; bulk create (floors x units per floor, max 500); list shows tenant, current rent, lease end, days vacant. |
| Leases | Draft → Active → Ended / Renewed / Terminated / HoldingOver (Draft can be Cancelled). `LSE-YYYY-000000`. Activation checks overlap (also guarded by a filtered unique index) and fixes the **rent schedule**: periods follow the plan (1/2/4/6/12), due on the first day, short last period prorated by actual days/365 (366 over 29 Feb), 2 dp with the rounding remainder on the last instalment, optional rent-free days. **Renewal** creates the next-term draft; activating it renews the old lease with no gap. **Termination** cancels later instalments, pro-rates the one in progress, lists invoiced instalments that need a credit note, vacates the unit (or parks it as Unavailable while repairs are open). **Daily job** (`POST /leases/process-expiries`): ends or holds over expired leases, releases lapsed reservations; idempotent. |
| Work orders | `WO-YYYY-000000`; lifecycle New → Assigned → InProgress ⇄ OnHold → Completed → Closed (Cancelled); assigning needs a vendor or technician; a unit parked for repairs returns to Vacant when its last open work order is done; permission per target status. |
| Deals | `DEAL-YYYY-000000`; sale or lease; commission = value x rate (2 dp) with agent and team-lead shares; pipeline Lead → Viewing → Negotiation → Agreed, or Lost (reason required). |
| Country packs | Country-specific fields are **not** in the module: the UAE pack adds Ejari/Tawtheeq registration (lease), DEWA/ADDC premise no. (unit), title deed and Makani no. (property) as ordinary custom fields. |
| Custom fields | Property, Unit, Lease, Work order and Deal are customizable (`realestate.*` entity types). |

## Deferred (needs the Finance sprint)
Billing run and rent invoices (the schedule rows carry `invoiceId` and status `Invoiced` for it), receipts and allocation, security-deposit ledger and settlement, cheque (PDC) links, credit notes on termination, deal commission invoicing/payouts (stages Invoiced, Collected, AgentPaid), VAT tax-point rules, tenancy-registration workflow and certificate expiry, owner statements, acquisitions and funding, reports, and the scheduled (daily) trigger for the expiry job.
