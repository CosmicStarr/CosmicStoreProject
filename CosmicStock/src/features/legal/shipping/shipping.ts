import { Component } from '@angular/core';
import { LEGAL_TERMS_VERSION } from '../../../core/legal/legal-terms';
import { LegalShellComponent } from '../legal-shell';
import { ShippingPolicyContentComponent } from '../shipping-policy-content';

@Component({
  selector: 'app-shipping',
  imports: [LegalShellComponent, ShippingPolicyContentComponent],
  templateUrl: './shipping.html',
  styleUrl: '../legal-page.scss',
})
export class ShippingComponent {
  protected readonly LEGAL_TERMS_VERSION = LEGAL_TERMS_VERSION;
}
