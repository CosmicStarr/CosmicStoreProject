import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../env/environment';
import { ICheckoutRequest, IOrder } from '../../features/models/order';
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

  getOrder(orderId: string): Observable<IOrder> {
    return this.http.get<IOrder>(`${this.apiUrl}Orders/${orderId}`);
  }

  requestCancellation(orderId: string): Observable<IOrder> {
    return this.http.post<IOrder>(`${this.apiUrl}Orders/${orderId}/cancel`, {});
  }
}
