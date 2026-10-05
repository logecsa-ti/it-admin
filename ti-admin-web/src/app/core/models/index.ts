// Modelos de la API: los DTO se generan desde openapi/tiadmin-api.json (npm run api:types);
// aqui solo viven los tipos genericos que el generador expande por cada T.
export type * from './api.generated';

/** Envoltura estandar de respuestas (ApiResponse / ApiResponse<T> del backend). */
export interface ApiEnvelope<T> {
  success: boolean;
  data?: T | null;
  message?: string | null;
  errors: { code: string; message: string }[];
  traceId?: string | null;
}

/** PagedResult<T> del backend. */
export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
  hasPrevious: boolean;
  hasNext: boolean;
}

/** PagedQuery del backend. */
export interface PageRequest {
  page?: number;
  pageSize?: number;
  search?: string | null;
  sortBy?: string | null;
  sortDirection?: 'Ascending' | 'Descending';
}

/** Parametros de consulta: los valores vacios se omiten al construir la URL. */
export type QueryParams = Record<string, string | number | boolean | null | undefined>;

/** Archivo descargado (exportaciones, documentos, plantillas). */
export interface DownloadedFile {
  blob: Blob;
  fileName: string;
}
