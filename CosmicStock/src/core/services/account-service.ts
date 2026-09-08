import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { environment } from '../../env/environment';
import { IUser, ILoginValues, IRegisterValues } from '../../features/models/UserInfo';
import { Router } from '@angular/router';
import { BehaviorSubject, map, of, switchMap, tap } from 'rxjs';
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

  private currentUserSource = new BehaviorSubject<IUser | null>(this.getStoredUser());
  currentUser$ = this.currentUserSource.asObservable();

  private getStoredUser(): IUser | null {
    const storedUser = localStorage.getItem('cosmicStockUser');
    return storedUser ? JSON.parse(storedUser) : null;
  }

  get currentUserValue(): IUser | null {
    return this.currentUserSource.value;
  }

  private mergeCartIfNeeded() {
    const guestCartId = this.cartService.getCartId();
    if (guestCartId) {
      return this.cartService.mergeGuestCart(guestCartId);
    }
    return null;
  }

  register(values: IRegisterValues) {
    return this.http.post<IUser>(`${this.apiUrl}account/register`, values).pipe(
      switchMap((user) => {
        localStorage.setItem('cosmicStockUser', JSON.stringify(user));
        this.currentUserSource.next(user);
        const merge = this.mergeCartIfNeeded();
        return merge ? merge.pipe(map(() => user)) : of(user);
      })
    );
  }

  login(credentials: ILoginValues) {
    return this.http.post<IUser>(`${this.apiUrl}account/login`, credentials).pipe(
      switchMap((user) => {
        localStorage.setItem('cosmicStockUser', JSON.stringify(user));
        this.currentUserSource.next(user);
        const merge = this.mergeCartIfNeeded();
        return merge ? merge.pipe(map(() => user)) : of(user);
      })
    );
  }

  logout() {
    localStorage.removeItem('cosmicStockUser');
    this.currentUserSource.next(null);
    this.router.navigate(['/login']);
  }

  loadCurrentUser() {
    return this.http.get<IUser>(`${this.apiUrl}account`).pipe(
      tap((user) => {
        if (user) {
          localStorage.setItem('cosmicStockUser', JSON.stringify(user));
          this.currentUserSource.next(user);
        }
      })
    );
  }

  confirmEmail(userId: string, token: string) {
    return this.http.post<{ message: string }>(`${this.apiUrl}account/confirm-email`, { userId, token });
  }

  forgotPassword(email: string) {
    return this.http.post<{ message: string }>(`${this.apiUrl}account/forgot-password`, { email });
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

  changePassword(payload: { currentPassword: string; newPassword: string; confirmPassword: string }) {
    return this.http.post<{ message: string }>(`${this.apiUrl}account/change-password`, payload);
  }
}
