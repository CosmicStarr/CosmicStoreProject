import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

export const guestGuard: CanActivateFn = () => {
  const router = inject(Router);
  const user = localStorage.getItem('cosmicStockUser');

  if (!user) {
    return true;
  }

  try {
    const parsed = JSON.parse(user) as { isGuest?: boolean };
    if (parsed.isGuest) {
      localStorage.removeItem('cosmicStockUser');
      return true;
    }
  } catch {
    return true;
  }

  router.navigate(['/store']);
  return false;
};
