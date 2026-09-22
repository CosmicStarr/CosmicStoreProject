import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../env/environment';
import { ICheckoutRequest, IGuestOrderAccess, IGuestRefundRequest, IOrder } from '../../features/models/order';
import { IProductResponse } from '../../features/models/productResponse';
import { Observable } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class OrderService {
  private http = inject(HttpClient);
  private apiUrl = environment.baseUrl;

  checkout(request: ICheckoutRequest): Observable<IOrder> {
    return this.http.post<IOrder>(`${this.apiUrl}Orders/checkout`, request);
  }

  getOrders(): Observable<IOrder[]> {
    return this.http.get<IOrder[]>(`${this.apiUrl}Orders`);
  }

  getRecentPurchases(): Observable<IProductResponse[]> {
    return this.http.get<IProductResponse[]>(`${this.apiUrl}Orders/recent-purchases`);
  }

  getOrder(orderId: string): Observable<IOrder> {
    return this.http.get<IOrder>(`${this.apiUrl}Orders/${orderId}`);
  }

  requestCancellation(orderId: string): Observable<IOrder> {
    return this.http.post<IOrder>(`${this.apiUrl}Orders/${orderId}/cancel`, {});
  }

  cancelOrderItem(orderId: string, itemId: number): Observable<IOrder> {
    return this.http.post<IOrder>(`${this.apiUrl}Orders/${orderId}/items/${itemId}/cancel`, {});
  }

  requestItemRefund(
    orderId: string,
    itemId: number,
    payload: { returnTrackingNumber?: string; reason: string },
  ): Observable<IOrder> {
    return this.http.post<IOrder>(`${this.apiUrl}Orders/${orderId}/items/${itemId}/refund`, payload);
  }

  verifyGuestOrder(payload: IGuestOrderAccess): Observable<IOrder> {
    return this.http.post<IOrder>(`${this.apiUrl}Orders/guest/verify`, payload);
  }

  cancelGuestOrder(payload: IGuestOrderAccess): Observable<IOrder> {
    return this.http.post<IOrder>(`${this.apiUrl}Orders/guest/${payload.orderId}/cancel`, payload);
  }

  cancelGuestOrderItem(itemId: number, payload: IGuestOrderAccess): Observable<IOrder> {
    return this.http.post<IOrder>(
      `${this.apiUrl}Orders/guest/${payload.orderId}/items/${itemId}/cancel`,
      payload,
    );
  }

  requestGuestItemRefund(itemId: number, payload: IGuestRefundRequest): Observable<IOrder> {
    return this.http.post<IOrder>(
      `${this.apiUrl}Orders/guest/${payload.orderId}/items/${itemId}/refund`,
      payload,
    );
  }
}
