# Frontend architecture

Angular 20, standalone components, signals, ng-zorro-antd. Reference implementation: **Sales** (`features/sales`).

## Folders

```
src/app/
  core/                    app-wide, no UI of its own
    api/<area>.api.ts      every HTTP client (CrudApiService subclasses) and its DTOs; the only code that calls HttpClient
    auth/ guards/ http/    session, route guards, interceptors, problem-details helpers
    format/                pure formatting helpers (dates, labels, done-text...)
    i18n/ session/ services/ custom-fields/
  shared/                  reusable across features, knows no business area
    components/<name>/     <name>.component.ts | .html | .scss
    directives/  pipes/
    forms/                 form helpers (revealErrors, ...)
    utils/                 small helpers (loadInto, record links, ...)
  layout/<name>/           the app shell, sidebar, global search, drawers
  features/<area>/         one folder per business area (sales, finance, hr, realestate, ...)
    <area>.routes.ts       when the area has its own routes
    pages/<name>/          routed pages: <name>.page.ts | .html | .scss
    components/<name>/     components used only inside this area
    <area>-*.ts            the area's shared logic (lookups, line maths, mappers, view helpers)
```

Imports between areas use the aliases `@core/...`, `@shared/...`, `@layout/...`, `@features/...`; imports inside one area stay relative.

## Rules

1. **One component per file**, in its own folder. A template longer than ten lines lives in `<name>.html`, its styles in `<name>.scss`.
2. **Components stay thin.** A page wires signals, forms and user actions together; calculations, mapping and lookups live in plain
   functions or small `@Injectable()` helpers in the area (`sales-lines.ts`, `sales-lookups.ts`), shared code in `shared/` or `core/`.
3. **HTTP only through `core/api`.** A page never injects `HttpClient`; an API client never lives in a page file.
4. **Reuse before writing.** Common helpers:
   - forms: `revealErrors(form)` (show validation errors), `showFormSaveError` / `showDrawerSaveError` (`shared/forms/save-errors.ts`),
     `setControlsEnabled`, `lineTotal` / `documentTotals` (priced lines, `shared/forms/line-totals.ts`);
   - loading: `loadInto(signal, source$)` (fill a side list, ignore failure), `loadOrFallback`, `itemsSignal`, `loadRecord` (detail pages);
   - dates and money: `toIsoDate` / `toUtcIsoDate` (`core/format/dates.ts`), `fromIsoDate`, `formatMoney` (`shared/utils/money.ts`),
     `shared/utils/date-range.ts` (period presets);
   - KPIs: `KpiStats`, `countRows`, `fetchAllRows` (`shared/utils/kpi-stats.ts`), `<erp-status-counts>`;
   - UI: `createPagedList` + `<erp-paged-table>`, `<erp-stat-card>`, `<erp-status>`, `saveBlob`, `deleteWithConfirm`.
5. **Typed code**: no `any`; give form values an interface (`QuoteFormValue`) and cast `form.value` to it.
6. **Members a template uses keep stable names**; move their implementation, not the template.
7. Lookups that a page needs only while it is open are provided by the page (`providers: [SalesLookups]`), so every visit loads fresh lists.

## Checks

- `npx ng build`, `npx ng test --watch=false --browsers=ChromeHeadless`, `npm run lint:a11y`.
