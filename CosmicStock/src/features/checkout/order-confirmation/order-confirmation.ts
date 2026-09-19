import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { IOrder } from '../../models/order';
import { SiteNavbarComponent } from '../../../core/components/site-navbar/site-navbar';
import { SiteFooterComponent } from '../../../core/components/site-footer/site-footer';

@Component({
  selector: 'app-order-confirmation',
  imports: [CurrencyPipe, DatePipe, RouterLink, SiteNavbarComponent, SiteFooterComponent],
  templateUrl: './order-confirmation.html',
  styleUrl: './order-confirmation.scss',
})
export class OrderConfirmationComponent {
  protected readonly order = signal<IOrder | null>(null);

  constructor() {
    const nav = inject(Router).getCurrentNavigation();
    const fromNav = nav?.extras.state as { order?: IOrder } | undefined;
    const fromHistory = history.state as { order?: IOrder } | undefined;
    this.order.set(fromNav?.order ?? fromHistory?.order ?? null);
  }
}
