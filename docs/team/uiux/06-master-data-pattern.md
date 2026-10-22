# 06 · Generic master-data list and form pattern (reusable component contract)

Spec basis: §4 "Global conventions for every list page" (toolbar search, filters, refresh, CSV, 25/page, page-size selector, record count, row click → detail, bold codes, Documents and History tabs), §3.3 (tables, modals, row ⋮), §3.6 (responsive), §8 #4 (skeletons), §10.2 (`DataTableComponent`, `PageHeaderComponent`, etc.), §12 (`?page=&pageSize=25&sort=&q=`, `/export.csv`, `If-Match`), §9 (saved views, custom fields, data import).
Custom fields: see **custom-fields.md**. This pattern renders them automatically from metadata (§9 below).

Industry-neutral example used throughout: **Items** (`core.item`). Any module resource (customers, vendors, employees, a module's own records) uses the same contract.

---
## 1. Anatomy

```
Breadcrumb: Inventory / Items
Items   Products and services you buy or sell.                [⤒ Import] [+ New item]
[KPI] [KPI] [KPI] [KPI]   ← optional, config.kpis; clicking one applies a filter
┌────────────────────────────────────────────────────────────────────────────────┐
│ FILTER BAR                                                                     │
│ [🔍 Search name, code…][⌕] [Category ▾] [Status ▾] [Created 📅—📅] [+ Filter]  │
│ Views: [All items ▾] ★     chips: Category: Hardware ×  Status: Active ×  Clear all │
│                                                     [⟳] [⤓ CSV ▾] [⚙ Columns]   │
├────────────────────────────────────────────────────────────────────────────────┤
│ BULK BAR (replaces the filter row's right side when ≥1 selected)               │
│ 3 selected · Select all 248 matching   [Edit field] [Export] [Archive] [Delete] ✕│
├────────────────────────────────────────────────────────────────────────────────┤
│ ☐ │ Code ▲    │ Name                 │ Category │ Price      │ Status  │ ⋮ │
│ ☐ │ ITM-0001  │ Steel bracket        │ Hardware │  1,250.00  │ Active  │ ⋮ │
│   │           │ muted sub-line       │          │            │         │   │
├────────────────────────────────────────────────────────────────────────────────┤
│ 248 records                                     ‹ 1 2 3 … 10 ›  [25 / page ▾]  │
└────────────────────────────────────────────────────────────────────────────────┘
```

## 2. Filter bar
| Element | Control | Behaviour |
|---|---|---|
| Search | `nz-input-group nzSearch` + search button (spec: "search box with a search button") | Submits on Enter or button click. Also debounced at 400 ms. Placeholder comes from `config.searchPlaceholder` ("Search name, code…"). Server param `q`. |
| Quick filters | `nz-select` (single/multi), `nz-range-picker`, `nz-segmented` (for tab-like scopes, e.g. "Open / All") | Defined in `config.filters` with `quick: true`. A maximum of 4 are shown inline; the rest go into **+ Filter**. |
| + Filter (advanced) | `nz-dropdown` → popover filter builder | Lets the user pick a field (including custom fields), an operator (per type, custom-fields.md §B.3) and a value. Each filter becomes a chip. |
| Active filter chips | `nz-tag nzMode="closeable"` | Each chip reads "Label: value". **Clear all** link. |
| Refresh | icon button `aria-label="Refresh"` | Re-fetches the list and keeps the page. |
| Export | `nz-dropdown` button: "Export current view (CSV)", "Export selected", "Export all columns (XLSX)" | Calls `/export.csv` with the current query, visible columns and custom fields (spec §12). Large exports run as a background job with a notification. |
| Column settings | icon button → `nz-dropdown` panel | See §4 |
| Legal-entity scope | — (not in the bar) | Comes from the header switcher (spec §4.30). When `entityScoped`, a muted line under the title reads "Showing: Acme Trading LLC · change in header". |

All query state (`q`, filters, sort, page, pageSize, view) is mirrored into URL query params, so the page can be bookmarked, survives a hard reload (spec §8 #1), and the back button works.

## 3. Saved views
| Item | Behaviour |
|---|---|
| View selector | `nz-select` with groups: **System** (defined by the module, e.g. "All items", "Archived"), **Shared** (published by admins), **My views**. A ★ marks the user's default. |
| What a view stores | Search, filters, sort, page size, visible columns with their order, widths and pinning, and density. |
| Save | When the current state differs from the view, the selector shows "• Modified", plus **Save** (overwrite; only own or permitted views) and **Save as new…** (`nz-modal`: Name* ≤ 50, Visibility: Only me / Everyone in workspace / Specific roles, and "Set as my default"). **Sharing (resolved, round 3):** only users with `core.settings.manage` see the *Everyone* and *Specific roles* options and can edit or delete shared views; everyone else can save **Only me** views. |
| Manage | Rename, Duplicate, Delete (`nz-popconfirm`), Set default. |
| API | **Proposed:** `GET/POST/PUT/DELETE /views?resource=core.item`. Not in spec §12. |
| Broken view | If a view references a deleted or deactivated custom field, that filter or column is dropped. An `nz-alert warning` reads "This view referenced fields that no longer exist; they were removed." with **Save view** to persist the fix. |

## 4. Table
| Feature | Control / behaviour |
|---|---|
| Component | `nz-table` with `[nzFrontPagination]="false"` and `[nzScroll]="{x: 'max-content'}"`. Server-side pagination, sorting and filtering (spec §8 #4 fix). |
| Selection | `nz-table` checkbox column. Header checkbox selects the current page. A banner link "Select all N matching" switches to query-based selection (`selection = {mode:'query', query, excludedIds}`). |
| Sorting | `nzShowSort` with `nzSortFn=true` (server). One column at a time (**assumption**, consistent with the spec API `sort=`). `aria-sort` set on `th`. |
| Pagination | `nz-pagination` (in nz-table): default 25 per page, options 10/25/50/100 (spec 25; audit uses 50). Footer: "N records" (spec) plus a page-size selector. |
| Code column | Bold, pinned left (`nzLeft`) on overflow. |
| Name column | Primary line plus an optional muted `subField` line. |
| Money / number | Right-aligned, using `MoneyPipe` / number formatting (00 §1). |
| Status | `StatusTagComponent` using the status → colour map in the resource config. |
| Row click | `rowClick: 'detail'` (navigate to `:id`, spec default), `'drawer-view'` (open the drawer in view mode; used by simple resources without a detail page) or `'none'`. Rows are focusable and Enter opens them. |
| Row actions (⋮) | `nz-dropdown` on a `button` (`aria-label="Actions for ITM-0001"`), pinned right (`nzRight`). The spec keeps this column visible on narrow screens. Built-in actions: View, Edit, Duplicate, Archive/Restore, Delete. Modules can add custom ones. Each action has a `permission` and a `visible(row)` predicate. An inline pencil icon is allowed for the single most common action (spec §3.3). |
| Column settings | Panel with a checkbox list (`nz-checkbox`) of all columns, **including custom fields** grouped under "Custom fields". It supports drag to reorder (CDK drag-drop, plus keyboard ↑/↓ buttons for accessibility), pinning left or right, a density switch (Comfortable / Compact) and **Reset to default**. The state is saved to the current view, or per-user when the view is a system view. |
| Column resize | Drag handle on headers (`nz-resizable`). Widths persist with the view. |
| Expandable rows | Optional `expand` template (spec audit "+" expander). |

## 5. Bulk actions
* The bulk bar appears when ≥ 1 row is selected (`role="region" aria-label="Bulk actions"`, count announced via `aria-live`).
* Built-in actions: **Export selected**, **Edit field** (pick one editable field, standard or custom, then set a value for all; `nz-modal`), **Archive**, **Restore**, **Delete**. Modules can register their own.
* Each bulk action defines `permission`, `confirm` (`none` | `popconfirm` | `modal` | `typed`; `typed` makes the user type the count for destructive actions over 50 records, **assumption**), and `minSelection`/`maxSelection`.
* Result reporting: a `nz-modal` or toast summarises "45 archived · 3 skipped". It includes a **View skipped** list with reasons, such as "Used in 2 documents".
* Long-running jobs (> 200 records, **assumption**) run in the background, with progress in the notification centre.

## 6. Drawer form (create / edit / view)
Spec §3.3 uses **modals** for simple records and **full pages** for documents with lines. This pattern defaults to **`nz-drawer`** for master data, which keeps the list visible and gives more room for custom fields. `formMode` is configurable (`drawer` | `modal` | `page`). Documents with lines (invoices, bills, journals) stay `page`. This deviation needs owner sign-off (coordination-notes D2).

```
┌ New item ─────────────────────────────────────────────── ✕ ┐   width: `--erp-drawer-width-md` / `-lg`
│ [ General | Pricing | Custom tab… ]   ← tabs only if config defines >1 │
│ GENERAL                                                       │
│ Name*            [                         ]                  │
│ Code (optional)  [         ] Leave blank to auto-number       │
│ Category*        [ ▾ ]        Status [Active ▾]               │
│ ADDITIONAL INFO   ← custom-field section (custom-fields.md)   │
│ Warranty months  [   ]        Supplier ref (opt.) [      ]    │
├───────────────────────────────────────────────────────────────┤
│ [Delete]                       [Cancel] [Save & new] [Save]   │
└───────────────────────────────────────────────────────────────┘
View mode: same layout, values read-only (nz-descriptions style), footer [Edit] [⋮ more].
Edit mode title: "Edit ITM-0001 · Steel bracket".
```

| Aspect | Rule |
|---|---|
| Open | URL updates to `?drawer=new`, `?drawer=edit&id=…` or `?drawer=view&id=…`, so the state is deep-linkable and the back button closes the drawer. |
| Focus | Focus goes to the first field (create/edit) or the title (view). Focus is trapped while open and returns to the triggering control on close. |
| Rendering | Driven by `config.form` (sections → fields). Field metadata (type, required, validators, options) comes from the **field registry**: system fields plus custom fields merged (§9). A module can override a section with a custom component (`component` slot). |
| Footer | **Save** (primary), **Save & new** (create only, optional), **Cancel**, and **Delete** (danger, left side, edit only, permission-gated). In view mode: **Edit**. |
| Validation | Standard (00 §2). Errors in a non-visible tab add a red badge to that tab, and the first error's tab is activated. |
| Saving | Buttons show loading. On success: toast, drawer closes (or resets for Save & new), and the list refreshes and highlights the row (2 s highlight using `--erp-color-highlight-bg`; there is no animation when reduced motion is set). |
| Delete | `nz-popconfirm` "Delete ITM-0001?" If the server reports dependencies (spec §4.5 recommendation "block Delete when … exist"), the dialog becomes an `nz-modal` that lists the usages and offers **Archive instead**. |
| Archive vs delete | Master data uses soft delete (spec §11.1 `IsDeleted`). The UI says "Archive" for records that are in use and "Delete" for unused ones. **Assumption:** the backend returns `canDelete`. |

## 7. States (list and form)
| State | List | Drawer form |
|---|---|---|
| Initial loading | Toolbar renders immediately. 10 skeleton rows in the table (`nz-skeleton`), and KPI skeletons. | Skeleton fields (edit/view) while loading the record. Create renders immediately. |
| Refreshing | `nzLoading` overlay; data kept | — |
| Empty (no records) | `nz-empty` with the module-supplied illustration and copy: "No items yet", then "Items are products and services you buy or sell." Primary **+ New item** (if permitted) and secondary **Import** (spec §9 import wizards). | — |
| Empty (filtered) | "No items match your filters." with **Clear filters** | — |
| Error | `nz-result nzStatus="error"` in the card: "Couldn't load items." with **Retry** and a collapsible technical detail (`traceId`) | `nz-alert error` at the top. The form stays filled. |
| Forbidden | Route guard. If the API returns 403 anyway: `nz-result 403`. | Hidden actions; 403 shows `nz-alert`. |
| Not found | — | `nz-result 404` inside the drawer: "This item was deleted." with **Close** |
| Success | Toasts. Row highlight. | — |
| Disabled | Bulk buttons disabled when the selection is invalid for that action (tooltip shows the reason). | Fields disabled per `field.readOnly(row)` with a tooltip, e.g. a locked period. |
| Validation errors | — | Inline errors, a summary alert and tab badges |
| Conflict 412 | — | 00 §2 conflict alert |
| Offline | Sticky `nz-alert warning` "You're offline. Showing last loaded data." Mutations disabled. | Save disabled |

## 8. Angular component contract

Components (standalone, in `@erp/core/ui`):
* `<erp-master-data-page [config]>` is the full page: header, KPIs, filter bar, table, drawer host.
* `<erp-data-table [config] [query] [data]>` is the table only, for use in detail tabs (e.g. a parent record's related-records tab).
* `<erp-record-drawer [config] [mode] [recordId]>` is the drawer form only.
* `<erp-filter-bar>`, `<erp-view-selector>`, `<erp-column-settings>`, `<erp-bulk-bar>`, `<erp-dynamic-form>` are building blocks used by the components above.

```ts
// @erp/core/ui/master-data/master-data.config.ts
export interface MasterDataConfig<T = any> {
  resourceKey: string;              // 'core.item' | '<module>.<recordtype>' (singular; also the custom-field namespace)
  title: string;                    // i18n key or text: 'Items'
  subtitle?: string;
  breadcrumb?: string[];
  api: { list: string; get?: string; create?: string; update?: string; remove?: string; export?: string };
                                    // e.g. '/api/v1/items' — list must accept page,pageSize,sort,q,filter[...]
  idField?: keyof T & string;       // default 'id'
  codeField?: keyof T & string;     // rendered bold, pinned left
  titleField: keyof T & string;     // used in titles, aria-labels, confirms
  entityScoped?: boolean;           // sends X-Entity-Id, shows scope line
  permissions: { read: string; create?: string; edit?: string; delete?: string; export?: string; import?: string };

  columns: ColumnDef<T>[];
  includeCustomFields?: boolean | { columns?: boolean; filters?: boolean; form?: boolean; export?: boolean }; // default true
  filters?: FilterDef[];
  searchPlaceholder?: string;
  defaultSort?: { field: string; dir: 'asc' | 'desc' };
  pageSize?: number;                // default 25
  pageSizeOptions?: number[];       // default [10,25,50,100]
  systemViews?: SavedViewDef[];
  savedViews?: boolean;             // default true
  kpis?: KpiDef[];

  rowClick?: 'detail' | 'drawer-view' | 'none';    // default 'detail' if detailRoute set
  detailRoute?: (row: T) => any[];                 // e.g. r => ['/inventory/items', r.id]
  rowActions?: RowActionDef<T>[];
  bulkActions?: BulkActionDef<T>[];

  form?: FormDef;                   // sections/tabs/fields; omitted => no create/edit
  formMode?: 'drawer' | 'modal' | 'page';         // default 'drawer'
  drawerWidth?: 'md' | 'lg' | 'xl';               // maps to --erp-drawer-width-md|lg|xl
  statusMap?: Record<string, Tone>;                // Tone = 'success'|'neutral'|'warning'|'error'|'info'|'orange'|'plain' (app-shell.md §2; --erp-tone-<tone>-*)
  emptyState?: { title: string; description?: string; illustration?: string };
  extensionPoint?: string;          // id other modules use to add columns/actions/tabs (see 08 §5)
}

export interface ColumnDef<T> {
  key: string;                      // field path; 'cf.<fieldKey>' for custom fields, e.g. 'cf.cf_credit_limit' (auto-added)
  label: string;
  type?: FieldType;                 // drives renderer & default alignment; v1 = the BA's 15 types (custom-fields.md §A.3, FieldTypeV1); full union in uiux-advanced dynamic-fields.md
  width?: number; minWidth?: number;
  sortable?: boolean; pin?: 'left' | 'right';
  visibleByDefault?: boolean;       // false => available in column settings only
  subField?: string;                // muted second line
  render?: TemplateRef<any> | ((row: T) => string);
  sensitive?: boolean;              // masked unless permission 'core.sensitivedata.read' (BA BR-SEC-08)
}

export interface FilterDef {
  key: string; label: string; type: FieldType;
  control?: 'select' | 'multiselect' | 'dateRange' | 'numberRange' | 'segmented' | 'boolean' | 'lookup';
  options?: { value: any; label: string }[] | { source: string };  // static or remote
  operators?: FilterOperator[];     // default per type (custom-fields.md §B.3)
  quick?: boolean;                  // inline in bar vs under '+ Filter'
  defaultValue?: any;
}

export interface RowActionDef<T> {
  id: string; label: string; icon?: string;
  permission?: string; visible?: (row: T) => boolean; disabled?: (row: T) => string | null; // string = tooltip reason
  danger?: boolean; confirm?: ConfirmDef;
  handler: 'edit' | 'view' | 'duplicate' | 'archive' | 'delete' | ((row: T) => void | Observable<unknown>);
}

export interface BulkActionDef<T> extends Omit<RowActionDef<T>, 'visible' | 'handler'> {
  minSelection?: number; maxSelection?: number;
  confirm?: ConfirmDef & { mode?: 'popconfirm' | 'modal' | 'typed' };
  handler: (sel: Selection<T>) => Observable<BulkResult>;
}
export type Selection<T> = { mode: 'ids'; ids: string[]; rows: T[] } | { mode: 'query'; query: ListQuery; excludedIds: string[]; total: number };

export interface FormDef {
  layout?: 'sections' | 'tabs';
  sections: { id: string; label?: string; tab?: string; columns?: 1 | 2; fields: FormFieldDef[]; component?: Type<any> }[];
  // custom fields are injected into sections by id (custom-fields.md §B.1); unknown section ids => 'Additional information'
}
export interface FormFieldDef {
  key: string; label: string; type: FieldType;
  required?: boolean | ((v: any) => boolean); readOnly?: boolean | ((row: any) => string | null);
  hidden?: (v: any) => boolean;     // conditional visibility
  placeholder?: string; help?: string; validators?: ValidatorSpec[];
  options?: FilterDef['options']; lookup?: LookupDef; defaultValue?: any;
  createOnly?: boolean; editOnly?: boolean; span?: 1 | 2;
}

export interface ListQuery { q?: string; filters: AppliedFilter[]; sort?: { field: string; dir: 'asc'|'desc' }; page: number; pageSize: number; viewId?: string }
```

Outputs on `<erp-master-data-page>` (all optional; the defaults handle CRUD through `api`):

| Output | Payload | When |
|---|---|---|
| `(queryChange)` | `ListQuery` | Search, filter, sort, page or view changed |
| `(rowClick)` | `T` | Row activated (overrides `rowClick`) |
| `(selectionChange)` | `Selection<T>` | Selection changed |
| `(actionInvoked)` | `{ actionId, row? , selection? }` | Any custom row or bulk action |
| `(saved)` | `{ mode: 'create'|'edit', record: T }` | After a successful save |
| `(deleted)` / `(archived)` | `{ ids }` | After success |
| `(viewChange)` | `SavedView` | View switched or saved |

Inputs beyond `config`: `[query]` (two-way via `queryChange`, for host control), `[extraFilters]` (fixed filters applied by the host, e.g. a parent id in a detail tab, hidden from chips), `[readOnly]`.

Template slots (`ng-template` with directives): `*erpCell="'columnKey'"`, `*erpEmpty`, `*erpToolbarExtra`, `*erpDrawerSection="'sectionId'"`, `*erpDetailHeaderExtra`.

Services consumed: `PermissionService.has(key)`, `EntityScopeService.current$`, `FieldRegistryService.fields(resourceKey)` (system and custom field metadata, custom-fields.md §B.5), `SavedViewService`, `ExportService`.

## 9. Custom fields in this pattern (summary; full detail in custom-fields.md §B)
* **Form:** custom fields render in the drawer in their configured section or tab and order. Fields in unknown sections go into an auto "Additional information" section.
* **Table:** every active custom field is available in Column settings under "Custom fields". They are hidden by default unless "Show in list by default" is set.
* **Filters:** custom fields appear in **+ Filter**, with operators by type. They can be saved in views.
* **Export/import:** custom fields are included in CSV/XLSX export (header = field label, plus the API name in a second header row for import templates) and in import column mapping.
* **Permissions:** field-level visibility and edit rules from the custom-field definition are applied by the renderer. Hidden fields are omitted from the columns, filters and export for that user.
* The contract stays the same. Setting `includeCustomFields: false` opts a resource out.

## 10. Responsive (see 10-responsive-accessibility.md)
≥ lg: full bar. md: quick filters collapse into a **Filters (n)** button that opens an `nz-drawer` from the right. < md: search full width, table horizontal scroll with code and ⋮ pinned, drawer full screen, bulk bar fixed at the bottom.
