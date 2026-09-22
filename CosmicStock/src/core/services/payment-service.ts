import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom, Observable } from 'rxjs';
import { loadStripe, Stripe, StripeCardElement, StripeElements } from '@stripe/stripe-js';
import { environment } from '../../env/environment';
import { IPaymentIntent, IPaymentIntentRequest } from '../../features/models/order';

@Injectable({ providedIn: 'root' })
export class PaymentService {
  private http = inject(HttpClient);
  private apiUrl = environment.baseUrl;

  private stripePromise?: Promise<Stripe | null>;
  private elements?: StripeElements;

  createOrUpdateIntent(cartId: string, request: IPaymentIntentRequest): Observable<IPaymentIntent> {
    return this.http.post<IPaymentIntent>(`${this.apiUrl}Payment/${cartId}`, request);
  }

  /**
   * The publishable key is served by the API so the same deployment never ships a
   * key that disagrees with the secret key the server is charging against.
   */
  getStripe(): Promise<Stripe | null> {
    if (!this.stripePromise) {
      this.stripePromise = firstValueFrom(
        this.http.get<{ publishableKey: string }>(`${this.apiUrl}Payment/config`),
      ).then((config) => loadStripe(config.publishableKey));
    }

    return this.stripePromise;
  }

  async createCardElement(): Promise<StripeCardElement> {
    const stripe = await this.getStripe();
    if (!stripe) throw new Error('Stripe failed to load.');

    this.elements ??= stripe.elements();

    return this.elements.create('card', {
      hidePostalCode: true,
      style: {
        base: {
          fontSize: '16px',
          color: '#1e293b',
          '::placeholder': { color: '#94a3b8' },
        },
        invalid: { color: '#b91c1c' },
      },
    });
  }

  async confirmCardPayment(
    clientSecret: string,
    card: StripeCardElement,
    billing: {
      name: string;
      email?: string;
      line1?: string;
      city?: string;
      state?: string;
      postalCode?: string;
      country?: string;
    },
  ) {
    const stripe = await this.getStripe();
    if (!stripe) throw new Error('Stripe failed to load.');

    const country = billing.country?.trim().toUpperCase();

    return stripe.confirmCardPayment(clientSecret, {
      payment_method: {
        card,
        billing_details: {
          name: billing.name,
          email: billing.email,
          address: {
            line1: billing.line1 || undefined,
            city: billing.city || undefined,
            state: billing.state || undefined,
            postal_code: billing.postalCode || undefined,
            country: country || undefined,
          },
        },
      },
    });
  }

  reset() {
    this.elements = undefined;
  }
}
