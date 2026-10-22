/** Standard API envelope (ERP.Application.Common.ApiResponse). The envelope interceptor unwraps it. */
export interface ApiResponse<T> {
  success: boolean;
  statusCode: number;
  message?: string;
  data: T;
  errors?: Record<string, string[]>;
  traceId?: string;
  /** paged variant only */
  meta?: PageMeta;
  // RFC 7807 members on errors
  type?: string; title?: string; status?: number; detail?: string;
}

export interface PageMeta { page: number; pageSize: number; total: number; totalPages: number; hasNext: boolean; hasPrevious: boolean; }

/** What services receive for paged endpoints after unwrapping: items + page meta. */
export interface PagedResult<T> extends PageMeta {
  items: T[];
}

export type FilterOperator =
  | 'eq' | 'ne' | 'gt' | 'gte' | 'lt' | 'lte' | 'contains' | 'startsWith' | 'in' | 'has' | 'isNull' | 'isNotNull';

export type FilterValue = string | number | boolean | null | ReadonlyArray<string | number>;

/** Serialized as `filter=field:op:value`; custom fields use their `cf_…` key; `in` values are pipe-joined. */
export interface FilterCriterion {
  field: string;
  op: FilterOperator;
  value?: FilterValue;
}

/** Mirrors ERP.Application.Common.PagedQuery: ?page=&pageSize=&sort=name,-createdAt&search=&filter=field:op:value */
export interface ListQuery {
  page: number;
  pageSize: number;
  /** "name,-createdAt" (only [Sortable] fields) */
  sort?: string | null;
  search?: string | null;
  filters?: FilterCriterion[];
  /** extra endpoint-specific params, e.g. { role: 'customer' } */
  extra?: Record<string, string | number | boolean>;
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
