# Bug Report Template

> Copy this into the tracker. One bug per report. **Never paste real passwords, tokens, cookies, or real customer PII.** Redact JWTs to their header and claims names only.

**Title:** `[Area] Short, specific symptom`. Example: `[Tenancy] GET /api/v1/invoices/{id} returns Org B invoice to Org A user`

| Field | Value |
|---|---|
| ID | (tracker) |
| Reporter / date (PKT) | |
| Environment | Local / CI / Staging / Prod · build/commit SHA · API version · browser + version · viewport |
| Org / legal entity | e.g. Org A `demo` · ENT-0002 (never real customer orgs) |
| User / role(s) | e.g. `qa.leasing.oasis` · Leasing manager · entity access: Only ENT-0002 |
| Modules enabled | e.g. core + realestate (+ sample) |
| Area | Tenancy · Entity scope · Permissions · Auth · Modules · Industry-agnostic core · Custom fields · Ledger · UI · Other |
| Severity | S1 Critical / S2 Major / S3 Minor / S4 Trivial (definitions below) |
| Priority | P0 / P1 / P2 / P3 (set at triage) |
| Security impact? | No / Yes. If Yes: restrict visibility, do not post in public channels |
| Related test case | e.g. TI-002, AUTH-024, CF-070 |
| Spec reference | e.g. §7.3, §10.3 |
| Regression? | Yes (last good build: …) / No / Unknown |
| Frequency | Always / Intermittent (x of y) / Once |

## Preconditions
- Data and setup required (seed name, records, settings such as approval thresholds)

## Steps to reproduce
1.
2.
3.

## Expected result
(quote the spec or acceptance criteria where possible)

## Actual result

## Evidence
- Screenshot / video / Playwright trace (`trace.zip`)
- HTTP request and response (method, URL, status, relevant headers; **tokens redacted**), ProblemDetails body, `traceId`
- Server log excerpt (Seq query link), with correlation id
- DB evidence (query and result), when relevant to data integrity

## Impact
Who is affected, data exposed or corrupted, workaround (if any).

## Notes / suspected cause (optional)

---

### Severity definitions
| Severity | Meaning | Examples |
|---|---|---|
| **S1 Critical** | Data leak across orgs or entities, authz bypass, data loss or corruption, ledger imbalance, outage, core broken with no modules enabled | Cross-org record visible; refresh reuse not detected; journal Σdr ≠ Σcr |
| **S2 Major** | Core flow broken, no workaround; wrong financial figure in a report | Cannot log in after a hard reload; module disable deletes data |
| **S3 Minor** | Function impaired, workaround exists | Filter on a custom date field ignores the time zone; misleading error text |
| **S4 Trivial** | Cosmetic | Misaligned tag colour; typo |

S1 → P0 (stop-ship, hotfix). Every S1/S2 fix must ship with a regression test (strategy §10).
