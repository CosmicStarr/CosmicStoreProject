import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../env/environment';
import { Observable } from 'rxjs';

export interface IAdminUser {
  id: string;
  email: string;
  userName: string;
}

@Injectable({ providedIn: 'root' })
export class AdminUsersService {
  private http = inject(HttpClient);
  private apiUrl = environment.baseUrl;

  getUsers(): Observable<IAdminUser[]> {
    return this.http.get<IAdminUser[]>(`${this.apiUrl}AdminUsers`);
  }
}
