import { Component, inject, OnInit, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { OrderService } from '../../../core/services/order-service';
import { IOrder } from '../../models/order';
import { SiteNavbarComponent } from "../../../core/components/site-navbar/site-navbar";

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
  loading = true;

  ngOnInit(): void {
    const orderId = this.route.snapshot.paramMap.get('id');
    if (!orderId) {
      this.loading = false;
      return;
    }

    this.orderService.getOrder(orderId).subscribe({
      next: (order) => {
        this.order.set(order);
        this.loading = false;
      },
      error: () => {
        this.loading = false;
      },
    });
  }
}
