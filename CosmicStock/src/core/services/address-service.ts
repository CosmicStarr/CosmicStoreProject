import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../env/environment';
import { IUserAddress } from '../../features/models/UserInfo';
import { Observable } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class AddressService {
  private http = inject(HttpClient);
  private apiUrl = `${environment.baseUrl}UserAddresses`;

  getAddresses(): Observable<IUserAddress[]> {
    return this.http.get<IUserAddress[]>(this.apiUrl);
  }

  createAddress(address: Omit<IUserAddress, 'id'>): Observable<IUserAddress> {
    return this.http.post<IUserAddress>(this.apiUrl, address);
  }

  updateAddress(id: number, address: Omit<IUserAddress, 'id'>): Observable<IUserAddress> {
    return this.http.put<IUserAddress>(`${this.apiUrl}/${id}`, address);
  }

  deleteAddress(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }
}
