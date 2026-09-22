import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../env/environment';
import { Observable } from 'rxjs';

export interface IAdminUser {
  id: string;
  email: string;
  userName: string;
  roles: string[];
  emailConfirmed: boolean;
  isLocked: boolean;
  isPermanentlyLocked: boolean;
}

@Injectable({ providedIn: 'root' })
export class AdminUsersService {
  private http = inject(HttpClient);
  private apiUrl = environment.baseUrl;

  getUsers(): Observable<IAdminUser[]> {
    return this.http.get<IAdminUser[]>(`${this.apiUrl}AdminUsers`);
  }

  grantAdmin(id: string): Observable<IAdminUser> {
    return this.http.post<IAdminUser>(`${this.apiUrl}AdminUsers/${id}/roles/admin`, {});
  }

  revokeAdmin(id: string): Observable<IAdminUser> {
    return this.http.delete<IAdminUser>(`${this.apiUrl}AdminUsers/${id}/roles/admin`);
  }

  lockUser(id: string): Observable<IAdminUser> {
    return this.http.post<IAdminUser>(`${this.apiUrl}AdminUsers/${id}/lock`, {});
  }

  unlockUser(id: string): Observable<{ message: string; user: IAdminUser }> {
    return this.http.post<{ message: string; user: IAdminUser }>(`${this.apiUrl}AdminUsers/${id}/unlock`, {});
  }

  sendResetPassword(id: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.apiUrl}AdminUsers/${id}/reset-password`, {});
  }

  deleteUser(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}AdminUsers/${id}`);
  }
}
