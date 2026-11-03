# Building screens for the ERP mobile app

Read this, then copy `lib/features/receivables/invoice_screens.dart`. It is the reference: list, filter chips, create/edit form,
detail with actions, and the registration in `screens.dart`.

## The pieces (`lib/kit/`)

| Piece | Use |
|---|---|
| `Field` (`field.dart`) | One input: `text`, `multiline`, `integer`, `decimal`, `money`, `date`, `select`, `lookup`, `toggle`, `lines`. `lookup` searches a list endpoint and can `copy` values of the chosen record into other fields (item -> price). |
| `FormSpec` + `openForm` (`form_page.dart`) | A form screen. `submit(ref, body, raw)` receives the cleaned JSON body and returns the saved record. Server field errors are shown under the field. |
| `ListScreen` (`list_screen.dart`) | A list endpoint with search, paging, status chips and a "New" button (shown only when the user holds `createPermission`). |
| `DetailPage` + `ActionSpec` (`detail_page.dart`) | Loads one record; app-bar edit; buttons for state changes (confirm / ask a reason / open a form), each gated by a permission and by the record's state. |
| `InfoCard`, `InfoRow`, `DetailHeader`, `DetailBody`, `money()` (`widgets.dart`) | Building blocks for detail bodies. |
| `MobileScreen` (`registry.dart`) | Registers a screen for one or more server menu ids. The More tab lists the server menu and opens the screens you register. |
| `meOrEmptyProvider` (`core/me.dart`) | `ref.read(meOrEmptyProvider).can('finance.invoices.write')`, `.legalEntities`, `.defaultEntity`, `.hasModule('pos')`. |
| `ApiClient` (`ref.read(apiProvider)`) | `get`, `post`, `put`, `delete`. Returns the unwrapped `data`; a list answer comes back as `{items,total,page,pageSize}`. Throws `ApiException` (has `.message`, `.fieldErrors`). |

After every successful save or action the kit bumps `dataVersionProvider`, so open lists and details reload by themselves.

## How to add an area

1. Find the area's menu entries (`id`, label, permission) in `backend/src/ERP.Modularity/CoreMenu.cs` or `backend/modules/ERP.Modules.<X>/<X>Module.cs`.
2. For each entry read the API: the endpoints file / module `Features/*.cs` (input records are the request bodies, `...Dto` records the answers).
   Names in JSON are camelCase, enums are camelCase strings (`"partlyPaid"`), dates are `yyyy-MM-dd`, ids are GUID strings.
   Permission keys are in `backend/src/ERP.Application/Security/Permissions.cs` or the module's permissions class.
3. Put your code in `lib/features/<area>/` and register your screens in `lib/features/<area>/screens.dart` (the file already exists with an empty list).
   Do not edit other areas' files or `lib/kit/` except to fix a real bug (then say so in your report).
4. Cover each entry: list (+ filters), detail, create/edit where the API has it, and the actions the API offers (issue, approve, void, convert...).
5. Money is shown with `formatMoney`; never show raw enum names, use `humanize`.

## Verify against the real API

Do not guess field names. Run a throwaway API on your own port and database and look at real answers:

```bash
cd mobile
tool/dev_api.sh 5091 ERP_Mobile_<Area> &            # takes ~1 minute the first time; use YOUR port and database name
T=$(tool/api_token.sh 5091 demo)                     # workspace code: demo (general) or demo-re (real estate)
curl -s "http://localhost:5091/api/v1/<path>?pageSize=2" -H "Authorization: Bearer $T"
# when you are done:
tool/stop_api.sh 5091 ; tool/drop_db.sh ERP_Mobile_<Area>
```

Try your create bodies with `curl -X POST` before relying on them. Never use ERP_Dev, ERP_Staging or ERP_Prod, and never touch any other port.
A module screen only returns data when its module is enabled for the workspace (`demo-re` has real estate; check `GET /api/v1/me` -> `enabledModules`).

## Tests and checks

- `flutter analyze` must report no issues; `flutter test` must pass.
- Add tests next to `test/` (own files per area, e.g. `test/<area>_test.dart`): form specs (required fields, the body they produce),
  list tiles, and detail actions with `FakeAdapter` (see `test/support/fake_adapter.dart`, `test/kit_test.dart`).
- Only run the whole suite at the end; while working use `flutter test test/<your_file>.dart`.
