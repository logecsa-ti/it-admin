import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { finalize } from 'rxjs';
import { LoadingService } from '@core/services/loading.service';
import { BACKGROUND_REQUEST } from './http-context';

export const loadingInterceptor: HttpInterceptorFn = (request, next) => {
  if (request.context.get(BACKGROUND_REQUEST)) {
    return next(request);
  }

  const loading = inject(LoadingService);
  loading.start();
  return next(request).pipe(finalize(() => loading.stop()));
};
