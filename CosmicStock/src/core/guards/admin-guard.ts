import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { isAdminUser } from '../utils/auth-utils';

export const adminGuard: CanActivateFn = () => {
  const router = inject(Router);

  if (isAdminUser()) {
    return true;
  }

  const hasUser = !!localStorage.getItem('cosmicStockUser');
  router.navigate([hasUser ? '/store' : '/login']);
  return false;
};
