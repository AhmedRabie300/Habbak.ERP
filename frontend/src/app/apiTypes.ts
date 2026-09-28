/** Standard error contract every endpoint returns (00-Frontend-Specs.md, section 12.1). */
export interface ApiErrorResponse {
  errorCode: string;
  message: string;
  details?: { field: string; message: string }[];
  correlationId: string;
}

/**
 * Raw ASP.NET model-binding/ValidationProblemDetails shape — returned when a request fails before
 * ever reaching our MediatR/FluentValidation pipeline (malformed JSON, a value that can't convert
 * to its target type, a missing required body), so it never carries our own ApiErrorResponse shape.
 */
export interface AspNetProblemDetails {
  title: string;
  status: number;
  errors?: Record<string, string[]>;
  traceId?: string;
}

/** GetList response contract (00-Frontend-Specs.md, section 6). */
export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface ListQueryParams {
  search?: string;
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDir?: 'asc' | 'desc';
}
