import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { IOrder } from '../../models/order';
import { SiteNavbarComponent } from '../../../core/components/site-navbar/site-navbar';
import { SiteFooterComponent } from '../../../core/components/site-footer/site-footer';
import {
  SHIPPING_PROCESSING_BUSINESS_DAYS,
  SHIPPING_TRANSIT_BUSINESS_DAYS_MAX,
  SHIPPING_TRANSIT_BUSINESS_DAYS_MIN,
} from '../../../core/legal/shipping-policy';

@Component({
  selector: 'app-order-confirmation',
  imports: [CurrencyPipe, DatePipe, RouterLink, SiteNavbarComponent, SiteFooterComponent],
  templateUrl: './order-confirmation.html',
  styleUrl: './order-confirmation.scss',
})
export class OrderConfirmationComponent {
  protected readonly order = signal<IOrder | null>(null);
  protected readonly processingDays = SHIPPING_PROCESSING_BUSINESS_DAYS;
  protected readonly transitMin = SHIPPING_TRANSIT_BUSINESS_DAYS_MIN;
  protected readonly transitMax = SHIPPING_TRANSIT_BUSINESS_DAYS_MAX;

  constructor() {
    const nav = inject(Router).getCurrentNavigation();
    const fromNav = nav?.extras.state as { order?: IOrder } | undefined;
    const fromHistory = history.state as { order?: IOrder } | undefined;
    this.order.set(fromNav?.order ?? fromHistory?.order ?? null);
  }
}
