import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { Router } from '@angular/router';
import { Observable } from 'rxjs';
import { AdminOrdersService } from '../../../core/services/admin-orders';
import { AdminCjService } from '../../../core/services/admin-cj-service';
import { IOrder } from '../../models/order';

@Component({
  selector: 'app-admin-orders',
  imports: [CurrencyPipe, DatePipe],
  templateUrl: './admin-orders.html',
  styleUrl: './admin-orders.scss',
  host: { class: 'admin-orders-page' },
})
export class AdminOrdersComponent implements OnInit {
  private adminOrders = inject(AdminOrdersService);
  private adminCj = inject(AdminCjService);
  private router = inject(Router);

  protected orders = signal<IOrder[]>([]);
  protected busyOrderId = signal<string | null>(null);
  protected message = signal<string | null>(null);
  protected loading = signal(true);
  protected ftcAttentionCount = computed(
    () => this.orders().filter((o) => o.needsFtcShipAttention).length,
  );

  ngOnInit(): void {
    this.load();
  }

  load() {
    this.loading.set(true);
    this.adminOrders.getOrders().subscribe({
      next: (orders) => {
        const list = Array.isArray(orders) ? orders : [];
        // Surface FTC-attention orders first so ops can act before day 30.
        list.sort((a, b) => Number(!!b.needsFtcShipAttention) - Number(!!a.needsFtcShipAttention));
        this.orders.set(list);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.message.set(err.error?.message || 'Orders could not be loaded.');
      },
    });
  }

  syncAll() {
    this.busyOrderId.set('all');
    this.message.set(null);

    this.adminCj.syncPendingOrders().subscribe({
      next: (result) => {
        this.message.set(
          `Processed ${result.updatedOrders} order(s). Paid orders are sent to CJ when the wallet can cover them.`,
        );
        this.busyOrderId.set(null);
        this.load();
      },
      error: (err) => {
        this.message.set(err.error?.message || 'Sync failed.');
        this.busyOrderId.set(null);
      },
    });
  }

  syncOrder(order: IOrder) {
    this.runOrderAction(order, () => this.adminCj.syncOrder(order.orderId));
  }

  cancelOrder(order: IOrder) {
    this.runOrderAction(order, () => this.adminCj.cancelOrder(order.orderId));
  }

  refundOrder(order: IOrder) {
    const amount = order.total.toFixed(2);
    const confirmed = window.confirm(
      `Refund $${amount} to ${order.customerEmail}? Stripe will return the payment to the original card.`,
    );
    if (!confirmed) {
      return;
    }

    this.runOrderAction(
      order,
      () => this.adminCj.refundOrder(order.orderId),
      'Refund issued through Stripe.',
    );
  }

  openOrder(order: IOrder) {
    void this.router.navigate(['/admin/orders', order.orderId]);
  }

  canCancel(order: IOrder) {
    return !['Shipped', 'Delivered', 'Cancelled', 'Refunded'].includes(order.status);
  }

  canRefund(order: IOrder) {
    if (order.paymentStatus !== 'PaymentRecevied' && order.paymentStatus !== 'Paid') {
      return false;
    }
    return !order.requiresReturnReceipt || !!order.returnReceived;
  }

  awaitingReturn(order: IOrder) {
    if (order.paymentStatus !== 'PaymentRecevied' && order.paymentStatus !== 'Paid') {
      return false;
    }
    return !!order.requiresReturnReceipt && !order.returnReceived;
  }

  refundRequestLabel(order: IOrder) {
    const count = order.items?.filter((item) =>
      item.status === 'RefundRequested' || !!item.refundRequestedAt || !!item.returnTrackingNumber,
    ).length ?? 0;
    if (count > 1) {
      return `${count} items requested`;
    }
    return 'Requested';
  }

  private runOrderAction(
    order: IOrder,
    request: () => Observable<IOrder>,
    successMessage?: string,
  ) {
    this.busyOrderId.set(order.orderId);
    this.message.set(null);

    request().subscribe({
      next: (updated) => {
        this.orders.update((orders) =>
          orders.map((o) => (o.orderId === updated.orderId ? updated : o)),
        );
        this.busyOrderId.set(null);
        if (successMessage) {
          this.message.set(successMessage);
        }
      },
      error: (err) => {
        this.message.set(err.error?.message || 'That action could not be completed.');
        this.busyOrderId.set(null);
      },
    });
  }
}
