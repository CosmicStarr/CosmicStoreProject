import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AccountService } from '../services/account-service';

export const registeredAccountGuard: CanActivateFn = () => {
  const router = inject(Router);
  const account = inject(AccountService);
  const user = account.currentUserValue;

  if (!user) {
    router.navigate(['/login']);
    return false;
  }

  if (user.isGuest) {
    router.navigate(['/store']);
    return false;
  }

  return true;
};
