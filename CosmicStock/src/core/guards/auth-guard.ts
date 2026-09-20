import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

export const authGuard: CanActivateFn = (_route, state) => {
  const router = inject(Router);
  const user = localStorage.getItem('cosmicStockUser');
  if (user) return true;
  void router.navigate(['/login'], {
    queryParams: { returnUrl: state.url },
  });
  return false;
};
