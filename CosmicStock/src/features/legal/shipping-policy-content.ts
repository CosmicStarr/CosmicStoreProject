import { Component } from '@angular/core';
import {
  SHIPPING_PROCESSING_BUSINESS_DAYS,
  SHIPPING_TRANSIT_BUSINESS_DAYS_MAX,
  SHIPPING_TRANSIT_BUSINESS_DAYS_MIN,
} from '../../core/legal/shipping-policy';

@Component({
  selector: 'app-shipping-policy-content',
  templateUrl: './shipping-policy-content.html',
  styleUrl: './legal-page.scss',
})
export class ShippingPolicyContentComponent {
  protected readonly processingDays = SHIPPING_PROCESSING_BUSINESS_DAYS;
  protected readonly transitMin = SHIPPING_TRANSIT_BUSINESS_DAYS_MIN;
  protected readonly transitMax = SHIPPING_TRANSIT_BUSINESS_DAYS_MAX;
}
