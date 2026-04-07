import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { SessionService } from '../services/session.service';

export const jwtInterceptor: HttpInterceptorFn = (req, next) => {
  const sessionService = inject(SessionService);
  const router = inject(Router);
  const token = sessionService.accessToken;

  if (!token) {
    return next(req).pipe(
      catchError((error) => {
        if (error?.status === 401) {
          sessionService.clearSession();
          router.navigate(['/login']);
        }
        return throwError(() => error);
      })
    );
  }

  const authorizedRequest = req.clone({
    setHeaders: {
      Authorization: `Bearer ${token}`
    }
  });

  return next(authorizedRequest).pipe(
    catchError((error) => {
      if (error?.status === 401) {
        sessionService.clearSession();
        router.navigate(['/login']);
      }
      return throwError(() => error);
    })
  );
};
