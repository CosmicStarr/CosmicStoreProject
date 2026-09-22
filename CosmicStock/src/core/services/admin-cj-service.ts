import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../env/environment';
import { ICjBalance, IOrder } from '../../features/models/order';
import { ICjProductImport } from '../../features/models/editProduct';

@Injectable({ providedIn: 'root' })
export class AdminCjService {
  private http = inject(HttpClient);
  private apiUrl = `${environment.baseUrl}AdminCj`;

  previewProduct(pidOrSku: string): Observable<ICjProductImport> {
    return this.http.post<ICjProductImport>(`${this.apiUrl}/preview-product`, { pid: pidOrSku, sku: pidOrSku });
  }

  getBalance(): Observable<ICjBalance> {
    return this.http.get<ICjBalance>(`${this.apiUrl}/balance`);
  }

  syncVariants(): Observable<{ mappedVariants: number }> {
    return this.http.post<{ mappedVariants: number }>(`${this.apiUrl}/sync/variants`, {});
  }

  syncStock(): Observable<{ updatedVariants: number }> {
    return this.http.post<{ updatedVariants: number }>(`${this.apiUrl}/sync/stock`, {});
  }

  syncPendingOrders(): Observable<{ updatedOrders: number }> {
    return this.http.post<{ updatedOrders: number }>(`${this.apiUrl}/orders/sync`, {});
  }

  syncOrder(orderId: string): Observable<IOrder> {
    return this.http.post<IOrder>(`${this.apiUrl}/orders/${orderId}/sync`, {});
  }

  cancelOrder(orderId: string): Observable<IOrder> {
    return this.http.post<IOrder>(`${this.apiUrl}/orders/${orderId}/cancel`, {});
  }

  refundOrder(orderId: string): Observable<IOrder> {
    return this.http.post<IOrder>(`${this.apiUrl}/orders/${orderId}/refund`, {});
  }

  openReturnDispute(orderId: string, returnTrackingNumber: string, message?: string): Observable<IOrder> {
    return this.http.post<IOrder>(`${this.apiUrl}/orders/${orderId}/return-dispute`, {
      returnTrackingNumber,
      message,
    });
  }

  refreshReturnDispute(orderId: string): Observable<IOrder> {
    return this.http.post<IOrder>(`${this.apiUrl}/orders/${orderId}/return-dispute/refresh`, {});
  }
}
