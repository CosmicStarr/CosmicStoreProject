import { Component, inject, OnInit, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AdminOrdersService } from '../../../core/services/admin-orders';
import { AdminCjService } from '../../../core/services/admin-cj-service';
import { IOrder, IOrderItem } from '../../models/order';

@Component({
  selector: 'app-admin-order-detail',
  imports: [CurrencyPipe, DatePipe, RouterLink],
  templateUrl: './admin-order-detail.html',
  styleUrl: './admin-order-detail.scss',
})
export class AdminOrderDetailComponent implements OnInit {
  private adminOrders = inject(AdminOrdersService);
  private adminCj = inject(AdminCjService);
  private route = inject(ActivatedRoute);

  protected order = signal<IOrder | null>(null);
  protected loading = signal(true);
  protected message = signal<string | null>(null);
  protected busyItemId = signal<number | null>(null);
  protected busyOrder = signal(false);

  ngOnInit(): void {
    const orderId = this.route.snapshot.paramMap.get('orderId');
    if (!orderId) {
      this.loading.set(false);
      this.message.set('Order not found.');
      return;
    }

    this.adminOrders.getOrder(orderId).subscribe({
      next: (order) => {
        this.order.set(order);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.message.set(err.error?.message || 'Order could not be loaded.');
      },
    });
  }

  lineTotal(item: IOrderItem) {
    return item.priceAtPurchase * item.quantity;
  }

  canCancelItem(item: IOrderItem) {
    const current = this.order();
    if (!current) return false;
    if (['Shipped', 'Delivered', 'Cancelled', 'Refunded'].includes(current.status)) {
      return false;
    }
    return item.status !== 'Cancelled' && item.status !== 'Refunded';
  }

  cancelItem(item: IOrderItem) {
    const current = this.order();
    if (!current) return;

    const amount = this.lineTotal(item).toFixed(2);
    const label = item.name || item.sku;
    const confirmed = window.confirm(
      `Cancel ${label} and refund $${amount} to ${current.customerEmail}? Other items stay on the order.`,
    );
    if (!confirmed) return;

    this.busyItemId.set(item.id);
    this.message.set(null);
    this.adminOrders.cancelOrderItem(current.orderId, item.id).subscribe({
      next: (updated) => {
        this.order.set(updated);
        this.busyItemId.set(null);
        this.message.set(`Cancelled ${label} and issued a Stripe refund.`);
      },
      error: (err) => {
        this.busyItemId.set(null);
        this.message.set(err.error?.message || 'That item could not be cancelled.');
      },
    });
  }

  syncOrder() {
    const current = this.order();
    if (!current) return;
    this.runOrderAction(() => this.adminCj.syncOrder(current.orderId), 'Order synced.');
  }

  cancelOrder() {
    const current = this.order();
    if (!current) return;
    this.runOrderAction(() => this.adminCj.cancelOrder(current.orderId), 'Order cancelled.');
  }

  refundOrder() {
    const current = this.order();
    if (!current) return;
    const confirmed = window.confirm(
      `Refund $${current.total.toFixed(2)} to ${current.customerEmail}? Stripe will return the remaining payment.`,
    );
    if (!confirmed) return;
    this.runOrderAction(() => this.adminCj.refundOrder(current.orderId), 'Refund issued through Stripe.');
  }

  canCancelOrder() {
    const current = this.order();
    return !!current && !['Shipped', 'Delivered', 'Cancelled', 'Refunded'].includes(current.status);
  }

  canRefundOrder() {
    const current = this.order();
    return !!current && (current.paymentStatus === 'PaymentRecevied' || current.paymentStatus === 'Paid');
  }

  private runOrderAction(request: () => import('rxjs').Observable<IOrder>, successMessage: string) {
    this.busyOrder.set(true);
    this.message.set(null);
    request().subscribe({
      next: (updated) => {
        this.order.set(updated);
        this.busyOrder.set(false);
        this.message.set(successMessage);
      },
      error: (err) => {
        this.busyOrder.set(false);
        this.message.set(err.error?.message || 'That action could not be completed.');
      },
    });
  }
}
