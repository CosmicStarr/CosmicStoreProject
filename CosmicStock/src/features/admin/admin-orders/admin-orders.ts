import { Component, inject, OnInit, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { AdminOrdersService } from '../../../core/services/admin-orders';
import { IOrder } from '../../models/order';

@Component({
  selector: 'app-admin-orders',
  imports: [CurrencyPipe, DatePipe],
  templateUrl: './admin-orders.html',
  styleUrl: './admin-orders.scss',
})
export class AdminOrdersComponent implements OnInit {
  private adminOrders = inject(AdminOrdersService);
  protected orders = signal<IOrder[]>([]);
  loading = true;

  ngOnInit(): void {
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
}
