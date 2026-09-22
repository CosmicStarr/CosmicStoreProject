import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../env/environment';
import { IPublicWishlist, IWishlist, IWishlistItem } from '../../features/models/UserInfo';
import { Observable } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class WishlistService {
  private http = inject(HttpClient);
  private apiUrl = `${environment.baseUrl}Wishlist`;

  getWishlist(): Observable<IWishlist> {
    return this.http.get<IWishlist>(this.apiUrl);
  }

  getPublicWishlist(publicId: string): Observable<IPublicWishlist> {
    return this.http.get<IPublicWishlist>(`${this.apiUrl}/public/${encodeURIComponent(publicId)}`);
  }

  updateWishlist(payload: {
    name?: string;
    shippingAddressId?: number | null;
    isGiftRegistry: boolean;
  }): Observable<IWishlist> {
    return this.http.put<IWishlist>(this.apiUrl, payload);
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
