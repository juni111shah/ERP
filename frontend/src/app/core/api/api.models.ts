/** Mirrors ERP.Application.Common.Paging + ProblemDetails (spec 12: ?page=&pageSize=25&sort=&q=). */
export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  /** total matching rows (scaffold PagedResult.Total) */
  total: number;
  totalPages: number;
}

export type FilterOperator =
  | 'eq' | 'ne' | 'gt' | 'gte' | 'lt' | 'lte' | 'contains' | 'startsWith' | 'in' | 'isNull' | 'isNotNull';

export type FilterValue = string | number | boolean | null | ReadonlyArray<string | number>;

/** Serialized as `filter=field:op:value`; custom fields use their `cf_…` key; `in` values are pipe-joined. */
export interface FilterCriterion {
  field: string;
  op: FilterOperator;
  value?: FilterValue;
}

export interface ListQuery {
  page: number;
  pageSize: number;
  /** "name,-createdAt,cf_floor_count" */
  sort?: string | null;
  q?: string | null;
  filters?: FilterCriterion[];
}

/** RFC 7807 as produced by ProblemDetailsExceptionHandler. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  traceId?: string;
  code?: string;
  /** camelCase paths, custom fields as "customFields.{cf_key}" */
  errors?: Record<string, string[]>;
}

export type CustomFieldValueMap = Record<string, unknown>;

export interface MasterDataDto {
  id: string;
  code: string;
  name: string;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string | null;
  /** base64 rowversion → sent back as If-Match */
  rowVersion: string;
  customFields: CustomFieldValueMap;
}

export interface MasterDataUpsert {
  code: string;
  name: string;
  isActive: boolean;
  customFields?: CustomFieldValueMap;
}
