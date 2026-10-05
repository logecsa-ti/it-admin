import { HttpClient, HttpContext, HttpParams, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '@env/environment';
import { ApiEnvelope, DownloadedFile, PageRequest, Paged, QueryParams } from '@core/models';

/**
 * Cliente base de la API: arma URLs desde environment.apiUrl, desenvuelve ApiResponse<T>
 * y omite filtros vacios. Los componentes nunca construyen URLs (CONVENTIONS.md 3.2).
 */
@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  get<T>(path: string, params?: QueryParams, context?: HttpContext): Observable<T> {
    return this.http
      .get<ApiEnvelope<T>>(this.url(path), { params: toHttpParams(params), context })
      .pipe(map(unwrap));
  }

  getPaged<T>(path: string, page: PageRequest, filters?: QueryParams): Observable<Paged<T>> {
    return this.get<Paged<T>>(path, { ...pageParams(page), ...filters });
  }

  post<T>(path: string, body?: unknown, params?: QueryParams): Observable<T> {
    return this.http
      .post<ApiEnvelope<T>>(this.url(path), body ?? null, { params: toHttpParams(params) })
      .pipe(map(unwrap));
  }

  put<T>(path: string, body: unknown): Observable<T> {
    return this.http.put<ApiEnvelope<T>>(this.url(path), body).pipe(map(unwrap));
  }

  patch<T>(path: string, body: unknown): Observable<T> {
    return this.http.patch<ApiEnvelope<T>>(this.url(path), body).pipe(map(unwrap));
  }

  delete(path: string): Observable<string | null> {
    return this.http
      .delete<ApiEnvelope<unknown>>(this.url(path))
      .pipe(map((envelope) => envelope?.message ?? null));
  }

  /** Respuesta completa (endpoints que devuelven un archivo o 202 con un trabajo en segundo plano). */
  getResponse(path: string, params?: QueryParams): Observable<HttpResponse<Blob>> {
    return this.http.get(this.url(path), {
      params: toHttpParams(params),
      observe: 'response',
      responseType: 'blob',
    });
  }

  download(path: string, params?: QueryParams): Observable<DownloadedFile> {
    return this.getResponse(path, params).pipe(map((response) => toDownloadedFile(response)));
  }

  upload<T>(path: string, form: FormData, params?: QueryParams, context?: HttpContext): Observable<T> {
    return this.http
      .post<ApiEnvelope<T>>(this.url(path), form, { params: toHttpParams(params), context })
      .pipe(map(unwrap));
  }

  url(path: string): string {
    return `${this.baseUrl}/${path.replace(/^\/+/, '')}`;
  }
}

function unwrap<T>(envelope: ApiEnvelope<T>): T {
  return envelope.data as T;
}

function pageParams(page: PageRequest): QueryParams {
  return {
    page: page.page,
    pageSize: page.pageSize,
    search: page.search,
    sortBy: page.sortBy,
    sortDirection: page.sortBy ? page.sortDirection : undefined,
  };
}

export function toHttpParams(params?: QueryParams): HttpParams {
  let result = new HttpParams();
  for (const [key, value] of Object.entries(params ?? {})) {
    if (value !== null && value !== undefined && value !== '') {
      result = result.set(key, String(value));
    }
  }
  return result;
}

export function toDownloadedFile(response: HttpResponse<Blob>): DownloadedFile {
  const disposition = response.headers.get('Content-Disposition') ?? '';
  const encoded = /filename\*=UTF-8''([^;]+)/i.exec(disposition)?.[1];
  const plain = /filename="?([^";]+)"?/i.exec(disposition)?.[1];
  const fileName = encoded ? decodeURIComponent(encoded) : (plain ?? 'archivo');
  return { blob: response.body ?? new Blob(), fileName };
}

/** Entrega un archivo descargado al navegador con su nombre. */
export function saveFile(file: DownloadedFile): void {
  const url = URL.createObjectURL(file.blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = file.fileName;
  anchor.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
