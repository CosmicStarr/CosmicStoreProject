import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AccountService } from '../services/account-service';

export const emailConfirmedGuard: CanActivateFn = () => {
  const router = inject(Router);
  const account = inject(AccountService);
  const user = account.currentUserValue;

  if (account.isGuestCheckout() || !user) {
    return true;
  }

  if (user.emailConfirmed !== true) {
    router.navigate(['/cart'], { queryParams: { confirmEmail: 'required' } });
    return false;
  }

  return true;
};
