import { Component, inject, OnInit, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { OrderService } from '../../../core/services/order-service';
import { IOrder } from '../../models/order';
import { SiteNavbarComponent } from '../../../core/components/site-navbar/site-navbar';

@Component({
  selector: 'app-order-detail-component',
  imports: [CurrencyPipe, DatePipe, RouterLink, SiteNavbarComponent],
  templateUrl: './order-detail-component.html',
  styleUrl: './order-detail-component.scss',
})
export class OrderDetailComponent implements OnInit {
  private orderService = inject(OrderService);
  private route = inject(ActivatedRoute);

  protected order = signal<IOrder | null>(null);
  protected loading = signal(true);
  protected cancelling = signal(false);
  protected message = signal<string | null>(null);
  protected messageIsError = signal(false);

  ngOnInit(): void {
    this.load();
  }

  canCancel() {
    const current = this.order();
    if (!current) return false;
    return !['Shipped', 'Delivered', 'Cancelled', 'Refunded'].includes(current.status);
  }

  requestCancellation() {
    const current = this.order();
    if (!current) return;

    const confirmed = window.confirm(
      'Cancel this order? If it has not shipped yet, we will stop fulfillment and refund your payment.',
    );
    if (!confirmed) return;

    this.cancelling.set(true);
    this.message.set(null);
    this.orderService.requestCancellation(current.orderId).subscribe({
      next: (updated) => {
        this.order.set(updated);
        this.cancelling.set(false);
        this.messageIsError.set(false);
        this.message.set('Your order was cancelled. Any refund will return to the original payment method.');
      },
      error: (err) => {
        this.cancelling.set(false);
        this.messageIsError.set(true);
        this.message.set(
          err.error?.message || 'This order could not be cancelled. Please try again or contact support.',
        );
        this.load();
      },
    });
  }

  private load() {
    const orderId = this.route.snapshot.paramMap.get('id');
    if (!orderId) {
      this.loading.set(false);
      return;
    }

    this.loading.set(true);
    this.orderService.getOrder(orderId).subscribe({
      next: (order) => {
        this.order.set(order);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      },
    });
  }
}
