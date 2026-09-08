import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

export const guestGuard: CanActivateFn = () => {
  const router = inject(Router);
  const user = localStorage.getItem('cosmicStockUser');

  if (!user) {
    return true;
  }

  router.navigate(['/store']);
  return false;
};
