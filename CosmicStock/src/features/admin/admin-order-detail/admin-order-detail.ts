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
  protected returnTracking = signal('');

  ngOnInit(): void {
    const orderId = this.route.snapshot.paramMap.get('orderId');
    if (!orderId) {
      this.loading.set(false);
      this.message.set('Order not found.');
      return;
    }

    this.adminOrders.getOrder(orderId).subscribe({
      next: (order) => {
        this.applyOrder(order);
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
    return item.status !== 'Cancelled' && item.status !== 'Refunded' && item.status !== 'RefundRequested';
  }

  itemHasRefundRequest(item: IOrderItem) {
    return item.status === 'RefundRequested' || !!item.refundRequestedAt || !!item.returnTrackingNumber;
  }

  requestedRefundItems() {
    const current = this.order();
    if (!current) return [];
    return (current.items ?? []).filter((item) => this.itemHasRefundRequest(item));
  }

  itemStatusLabel(item: IOrderItem) {
    return item.status === 'RefundRequested' ? 'Refund requested' : (item.status || 'Ordered');
  }

  useReturnTracking(item: IOrderItem) {
    const tracking = item.returnTrackingNumber?.trim();
    if (tracking) {
      this.returnTracking.set(tracking);
    }
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
        this.applyOrder(updated);
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
    if (current.requiresReturnReceipt && !current.returnReceived) {
      this.message.set(
        'This customer does not qualify for a refund until CJ confirms the returned merchandise was received.',
      );
      return;
    }
    const confirmed = window.confirm(
      `Refund $${current.total.toFixed(2)} to ${current.customerEmail}? Stripe will return the remaining payment.`,
    );
    if (!confirmed) return;
    this.runOrderAction(() => this.adminCj.refundOrder(current.orderId), 'Refund issued through Stripe.');
  }

  openReturnDispute() {
    const current = this.order();
    if (!current) return;
    const tracking = this.returnTracking().trim();
    if (!tracking) {
      this.message.set('Enter the customer return tracking number first.');
      return;
    }
    this.runOrderAction(
      () => this.adminCj.openReturnDispute(current.orderId, tracking),
      'CJ return dispute opened. Check receipt after CJ receives the merchandise.',
    );
  }

  refreshReturnDispute() {
    const current = this.order();
    if (!current) return;
    this.runOrderAction(() => this.adminCj.refreshReturnDispute(current.orderId), this.receiptMessage);
  }

  onReturnTrackingInput(event: Event) {
    this.returnTracking.set((event.target as HTMLInputElement).value);
  }

  canCancelOrder() {
    const current = this.order();
    return !!current && !['Shipped', 'Delivered', 'Cancelled', 'Refunded'].includes(current.status);
  }

  canRefundOrder() {
    const current = this.order();
    if (!current) return false;
    if (current.paymentStatus !== 'PaymentRecevied' && current.paymentStatus !== 'Paid') {
      return false;
    }
    return !current.requiresReturnReceipt || !!current.returnReceived;
  }

  waitingForReturn() {
    const current = this.order();
    if (!current) return false;
    if (current.paymentStatus !== 'PaymentRecevied' && current.paymentStatus !== 'Paid') {
      return false;
    }
    return !!current.requiresReturnReceipt && !current.returnReceived;
  }

  showReturnPanel() {
    const current = this.order();
    if (!current) return false;
    return (
      !!current.requiresReturnReceipt ||
      !!current.hasRefundRequest ||
      !!current.cjShipmentOrderId ||
      !!current.returnTrackingNumber ||
      !!current.cjDisputeId ||
      this.requestedRefundItems().length > 0
    );
  }

  private receiptMessage = (updated: IOrder) =>
    updated.returnReceived
      ? 'CJ confirmed the return was received. This customer now qualifies for a refund.'
      : updated.cjDisputeStatus
        ? `CJ dispute is ${updated.cjDisputeStatus}. The customer does not qualify for a refund yet.`
        : 'No CJ return receipt yet. Open a dispute after the customer ships the merchandise back.';

  private applyOrder(order: IOrder) {
    this.order.set(order);
    const stored = order.returnTrackingNumber?.trim();
    const fromItem = (order.items ?? [])
      .filter((item) => !!item.returnTrackingNumber?.trim())
      .sort((left, right) => {
        const leftDate = left.refundRequestedAt ? Date.parse(left.refundRequestedAt) : 0;
        const rightDate = right.refundRequestedAt ? Date.parse(right.refundRequestedAt) : 0;
        return rightDate - leftDate;
      })[0]?.returnTrackingNumber?.trim();
    if (stored) {
      this.returnTracking.set(stored);
    } else if (fromItem) {
      this.returnTracking.set(fromItem);
    }
  }

  private runOrderAction(
    request: () => import('rxjs').Observable<IOrder>,
    successMessage: string | ((updated: IOrder) => string),
  ) {
    this.busyOrder.set(true);
    this.message.set(null);
    request().subscribe({
      next: (updated) => {
        this.applyOrder(updated);
        this.busyOrder.set(false);
        this.message.set(typeof successMessage === 'function' ? successMessage(updated) : successMessage);
      },
      error: (err) => {
        this.busyOrder.set(false);
        this.message.set(err.error?.message || 'That action could not be completed.');
      },
    });
  }
}
