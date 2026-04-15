import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { SessionService } from '../session/session.service';

export const roleGuard: CanActivateFn = (route) => {
  const sessionService = inject(SessionService);
  const router = inject(Router);

  const requiredRoles = (route.data?.['roles'] as string[] | undefined)?.map((role) => role.toLowerCase()) ?? [];
  const currentRole = sessionService.currentUser?.role?.toLowerCase();

  if (!currentRole) {
    return router.createUrlTree(['/login']);
  }

  if (requiredRoles.length === 0 || requiredRoles.includes(currentRole)) {
    return true;
  }

  return router.createUrlTree(['/dashboard']);
};
