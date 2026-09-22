import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LEGAL_TERMS_VERSION } from '../../../core/legal/legal-terms';
import { LegalShellComponent } from '../legal-shell';
import { RefundPolicyContentComponent } from '../refund-policy-content';
import { ShippingPolicyContentComponent } from '../shipping-policy-content';

@Component({
  selector: 'app-terms',
  imports: [RouterLink, LegalShellComponent, RefundPolicyContentComponent, ShippingPolicyContentComponent],
  templateUrl: './terms.html',
  styleUrl: '../legal-page.scss',
})
export class TermsComponent {
  protected readonly LEGAL_TERMS_VERSION = LEGAL_TERMS_VERSION;
}
