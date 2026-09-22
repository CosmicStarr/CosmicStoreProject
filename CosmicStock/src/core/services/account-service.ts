import { HttpClient } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { environment } from '../../env/environment';
import { IUser, ILoginValues, IRegisterValues } from '../../features/models/UserInfo';
import { Router } from '@angular/router';
import { BehaviorSubject, catchError, map, of, switchMap, tap } from 'rxjs';
import { CartService } from './cart-service';

@Injectable({
  providedIn: 'root'
})
export class AccountService {
  private http = inject(HttpClient);
  private router = inject(Router);
  private cartService = inject(CartService);
  private apiUrl = environment.baseUrl;
  User: IUser | null = null;

  private guestCheckout = signal(false);
  private currentUserSource = new BehaviorSubject<IUser | null>(this.getStoredUser());
  currentUser$ = this.currentUserSource.asObservable();

  private getStoredUser(): IUser | null {
    const storedUser = localStorage.getItem('cosmicStockUser');
    if (!storedUser) {
      return null;
    }

    try {
      const parsed = JSON.parse(storedUser) as IUser;
      if (parsed.isGuest) {
        localStorage.removeItem('cosmicStockUser');
        return null;
      }
      return parsed;
    } catch {
      localStorage.removeItem('cosmicStockUser');
      return null;
    }
  }

  get currentUserValue(): IUser | null {
    return this.currentUserSource.value;
  }

  isGuestCheckout(): boolean {
    return this.guestCheckout();
  }

  beginGuestCheckout() {
    this.guestCheckout.set(true);
  }

  endGuestCheckout() {
    this.guestCheckout.set(false);
  }

  ensureGuestCheckoutIfNeeded() {
    const user = this.currentUserValue;
    if (!user || user.isGuest) {
      this.beginGuestCheckout();
    }
  }

  canPurchase(_user: IUser | null = this.currentUserValue): boolean {
    return true;
  }

  private discardGuestSession() {
    this.endGuestCheckout();
    this.cartService.clearLocalCart();
    localStorage.clear();
    this.currentUserSource.next(null);
  }

  private mergeCartIfNeeded() {
    const guestCartId = this.cartService.getCartId();
    if (!guestCartId) {
      this.cartService.loadCart();
      return of(null);
    }

    return this.cartService.mergeGuestCart(guestCartId).pipe(
      catchError(() => {
        this.cartService.loadCart();
        return of(null);
      })
    );
  }

  register(values: IRegisterValues) {
    return this.http.post<IUser>(`${this.apiUrl}account/register`, values).pipe(
      switchMap((user) => {
        this.endGuestCheckout();
        localStorage.setItem('cosmicStockUser', JSON.stringify(user));
        this.currentUserSource.next(user);
        return this.mergeCartIfNeeded().pipe(map(() => user));
      })
    );
  }

  login(credentials: ILoginValues) {
    this.discardGuestSession();

    return this.http.post<IUser>(`${this.apiUrl}account/login`, credentials).pipe(
      tap((user) => {
        localStorage.setItem('cosmicStockUser', JSON.stringify(user));
        this.currentUserSource.next(user);
        this.cartService.loadCart();
      })
    );
  }

  logout() {
    this.clearSession();
    this.router.navigate(['/login']);
  }

  loadCurrentUser() {
    return this.http.get<IUser>(`${this.apiUrl}account`).pipe(
      tap((user) => {
        if (user) {
          if (user.isGuest) {
            localStorage.removeItem('cosmicStockUser');
            this.currentUserSource.next(null);
            return;
          }
          localStorage.setItem('cosmicStockUser', JSON.stringify(user));
          this.currentUserSource.next(user);
        }
      })
    );
  }

  confirmEmail(userId: string, token: string, email?: string) {
    return this.http.post<{ message: string }>(`${this.apiUrl}account/confirm-email`, {
      userId,
      token,
      email: email || undefined,
    });
  }

  resendConfirmation(email: string) {
    return this.http.post<{ message: string }>(`${this.apiUrl}account/resend-confirmation`, { email });
  }

  forgotPassword(email: string) {
    return this.http.post<{ message: string }>(`${this.apiUrl}account/forgot-password`, { email });
  }

  verifyResetPassword(email: string, token: string) {
    return this.http.post<{ message: string }>(`${this.apiUrl}account/verify-reset-password`, { email, token });
  }

  resetPassword(payload: { email: string; token: string; newPassword: string; confirmPassword: string }) {
    return this.http.post<{ message: string }>(`${this.apiUrl}account/reset-password`, payload);
  }

  updateProfile(userName: string) {
    return this.http.put<IUser>(`${this.apiUrl}account/profile`, { userName }).pipe(
      tap((user) => {
        localStorage.setItem('cosmicStockUser', JSON.stringify(user));
        this.currentUserSource.next(user);
      })
    );
  }

  reauthenticate(currentPassword: string) {
    return this.http.post<{ reauthToken: string; expiresInSeconds: number }>(
      `${this.apiUrl}account/reauthenticate`,
      { currentPassword }
    );
  }

  requestEmailChange(reauthToken: string, newEmail: string) {
    return this.http.post<{ message: string; pendingEmail: string; expiresInMinutes: number }>(
      `${this.apiUrl}account/change-email`,
      { reauthToken, newEmail }
    ).pipe(
      tap((response) => {
        const user = this.currentUserValue;
        if (!user) {
          return;
        }

        const next = { ...user, pendingEmail: response.pendingEmail };
        localStorage.setItem('cosmicStockUser', JSON.stringify(next));
        this.currentUserSource.next(next);
      })
    );
  }

  confirmEmailChange(userId: string, token: string, email: string) {
    return this.http.post<{ message: string }>(`${this.apiUrl}account/confirm-change-email`, {
      userId,
      token,
      email,
    });
  }

  lockAccount(userId: string, token: string) {
    return this.http.post<{ message: string }>(`${this.apiUrl}account/lock-account`, { userId, token }).pipe(
      tap(() => this.clearSession())
    );
  }

  clearSession() {
    localStorage.clear();
    this.endGuestCheckout();
    this.cartService.clearLocalCart();
    this.currentUserSource.next(null);
  }

  changePassword(payload: { currentPassword: string; newPassword: string; confirmPassword: string }) {
    return this.http.post<{ message: string }>(`${this.apiUrl}account/change-password`, payload);
  }
}
