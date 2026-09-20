import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../env/environment';
import { IOrder } from '../../features/models/order';
import { Observable } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class AdminOrdersService {
  private http = inject(HttpClient);
  private apiUrl = environment.baseUrl;

  getOrders(): Observable<IOrder[]> {
    return this.http.get<IOrder[]>(`${this.apiUrl}AdminOrders`);
  }

  getOrder(orderId: string): Observable<IOrder> {
    return this.http.get<IOrder>(`${this.apiUrl}AdminOrders/${orderId}`);
  }

  cancelOrderItem(orderId: string, itemId: number): Observable<IOrder> {
    return this.http.post<IOrder>(`${this.apiUrl}AdminOrders/${orderId}/items/${itemId}/cancel`, {});
  }
}
