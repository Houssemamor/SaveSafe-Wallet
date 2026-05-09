import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { SessionService } from '../session/session.service';

/**
 * Guard for public routes (login, register).
 * Redirects authenticated users to the dashboard to prevent redundant access.
 * Allows unauthenticated users to proceed to the public route.
 */
export const publicGuard: CanActivateFn = () => {
  const sessionService = inject(SessionService);
  const router = inject(Router);

  // If user is already authenticated, redirect to dashboard
  if (sessionService.isAuthenticated) {
    return router.createUrlTree(['/dashboard']);
  }

  // Allow access to public route for unauthenticated users
  return true;
};
