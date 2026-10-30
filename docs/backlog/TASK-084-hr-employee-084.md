---
id: TASK-084
title: Employee IBAN must have a valid shape and is stored upper case
priority: P2
status: done
schema: no
depends_on: []
area: hr
audit_ref: Claude analysis, HR and payroll
commit: 6423fb4
---

## Problem
EmployeeFeatures.cs EmployeeInputValidator has no IBAN rule; Map stores Iban.Trim() as typed.

## Impact
Bank and WPS files get IBANs with spaces, lower case or garbage, which the bank rejects.

## Change wanted
Validate (when present) 2 letters, 2 digits, then 11 to 30 letters or digits, no spaces; store trimmed and upper case; blank becomes null. Checksum validation is out of scope.

## Acceptance criteria
- [x] 12345, an IBAN with spaces and a short one return 400; lower-case valid IBAN is stored upper case; empty is accepted.

## Tests required
An_iban_must_look_like_one_and_is_stored_upper_case in HrApiTests.

## Do not touch
Payroll maths, posting rules, leave.

## What I did (Claude)
Implemented in 6423fb4 (cards 080-084 share one commit because they touch the same three feature files). Build 0 errors; 253 unit and 283 integration tests pass, 4 SQL Server tests skipped.

## Review (Claude)
Self-implemented; verified by the tests above.
