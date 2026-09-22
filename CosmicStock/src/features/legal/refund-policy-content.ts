import { Component } from '@angular/core';
import {
  REFUND_LOST_DAYS_AFTER_WAREHOUSE,
  REFUND_QUALITY_DAYS_AFTER_DELIVERY,
} from '../../core/legal/refund-policy';

@Component({
  selector: 'app-refund-policy-content',
  templateUrl: './refund-policy-content.html',
  styleUrl: './legal-page.scss',
})
export class RefundPolicyContentComponent {
  protected readonly qualityDays = REFUND_QUALITY_DAYS_AFTER_DELIVERY;
  protected readonly lostDays = REFUND_LOST_DAYS_AFTER_WAREHOUSE;
}
