import { Observable } from 'rxjs';
import { ApiService } from '@core/api/api.service';
import { PageRequest, Paged, QueryParams } from '@core/models';

/** Cliente CRUD tipado para un recurso REST (`/api/v1/<path>`). Los servicios de features lo componen. */
export class ResourceApi<TDto, TCreate = unknown, TUpdate = TCreate> {
  constructor(
    protected readonly api: ApiService,
    readonly path: string,
  ) {}

  list(page: PageRequest, filters?: QueryParams): Observable<Paged<TDto>> {
    return this.api.getPaged<TDto>(this.path, page, filters);
  }

  all(filters?: QueryParams): Observable<TDto[]> {
    return this.api.get<TDto[]>(this.path, filters);
  }

  get(id: number): Observable<TDto> {
    return this.api.get<TDto>(`${this.path}/${id}`);
  }

  create(body: TCreate): Observable<TDto> {
    return this.api.post<TDto>(this.path, body);
  }

  update(id: number, body: TUpdate): Observable<TDto> {
    return this.api.put<TDto>(`${this.path}/${id}`, body);
  }

  remove(id: number): Observable<string | null> {
    return this.api.delete(`${this.path}/${id}`);
  }

  /** POST /<path>/{id}/<action> (transiciones de flujo). */
  action<T = TDto>(id: number, action: string, body?: unknown): Observable<T> {
    return this.api.post<T>(`${this.path}/${id}/${action}`, body);
  }
}
