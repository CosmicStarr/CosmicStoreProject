import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../env/environment';
import { IShippingOption, IShippingQuoteRequest } from '../../features/models/order';

@Injectable({ providedIn: 'root' })
export class ShippingService {
  private http = inject(HttpClient);
  private apiUrl = environment.baseUrl;

  getQuote(request: IShippingQuoteRequest): Observable<IShippingOption[]> {
    return this.http.post<IShippingOption[]>(`${this.apiUrl}Shipping/quote`, request);
  }
}
