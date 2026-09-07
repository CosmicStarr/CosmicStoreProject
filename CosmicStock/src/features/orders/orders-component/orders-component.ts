import { Component, inject, OnInit, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { OrderService } from '../../../core/services/order-service';
import { IOrder } from '../../models/order';

@Component({
  selector: 'app-orders-component',
  imports: [CurrencyPipe, DatePipe, RouterLink],
  templateUrl: './orders-component.html',
  styleUrl: './orders-component.scss',
})
export class OrdersComponent implements OnInit {
  private orderService = inject(OrderService);
  protected orders = signal<IOrder[]>([]);
  loading = true;

  ngOnInit(): void {
    this.orderService.getOrders().subscribe({
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
