import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../env/environment';
import { IWishlistItem } from '../../features/models/UserInfo';
import { Observable } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class WishlistService {
  private http = inject(HttpClient);
  private apiUrl = `${environment.baseUrl}Wishlist`;

  getWishlist(): Observable<IWishlistItem[]> {
    return this.http.get<IWishlistItem[]>(this.apiUrl);
  }

  addItem(productId: string): Observable<IWishlistItem> {
    return this.http.post<IWishlistItem>(`${this.apiUrl}/${productId}`, {});
  }

  removeItem(productId: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${productId}`);
  }

  isInWishlist(productId: string): Observable<boolean> {
    return this.http.get<boolean>(`${this.apiUrl}/contains/${productId}`);
  }
}
