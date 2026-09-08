import { Component, inject, OnInit, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { Observable } from 'rxjs';
import { AdminOrdersService } from '../../../core/services/admin-orders';
import { AdminCjService } from '../../../core/services/admin-cj-service';
import { IOrder } from '../../models/order';

@Component({
  selector: 'app-admin-orders',
  imports: [CurrencyPipe, DatePipe],
  templateUrl: './admin-orders.html',
  styleUrl: './admin-orders.scss',
})
export class AdminOrdersComponent implements OnInit {
  private adminOrders = inject(AdminOrdersService);
  private adminCj = inject(AdminCjService);

  protected orders = signal<IOrder[]>([]);
  protected busyOrderId = signal<string | null>(null);
  protected message = signal<string | null>(null);
  loading = true;

  ngOnInit(): void {
    this.load();
  }

  load() {
    this.loading = true;
    this.adminOrders.getOrders().subscribe({
      next: (orders) => {
        this.orders.set(orders);
        this.loading = false;
      },
      error: () => {
        this.loading = false;
      },
    });
  }

  syncAll() {
    this.busyOrderId.set('all');
    this.message.set(null);

    this.adminCj.syncPendingOrders().subscribe({
      next: (result) => {
        this.message.set(`Updated ${result.updatedOrders} order(s) from CJ.`);
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
    this.runOrderAction(order, () => this.adminCj.refundOrder(order.orderId));
  }

  canCancel(order: IOrder) {
    return !['Shipped', 'Delivered', 'Cancelled', 'Refunded'].includes(order.status);
  }

  canRefund(order: IOrder) {
    return order.status !== 'Refunded';
  }

  private runOrderAction(order: IOrder, request: () => Observable<IOrder>) {
    this.busyOrderId.set(order.orderId);
    this.message.set(null);

    request().subscribe({
      next: (updated) => {
        this.orders.update((orders) =>
          orders.map((o) => (o.orderId === updated.orderId ? updated : o)),
        );
        this.busyOrderId.set(null);
      },
      error: (err) => {
        this.message.set(err.error?.message || 'That action could not be completed.');
        this.busyOrderId.set(null);
      },
    });
  }
}
