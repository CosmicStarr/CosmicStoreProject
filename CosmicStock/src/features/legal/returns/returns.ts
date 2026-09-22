import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LEGAL_TERMS_VERSION } from '../../../core/legal/legal-terms';
import { LegalShellComponent } from '../legal-shell';
import { RefundPolicyContentComponent } from '../refund-policy-content';

@Component({
  selector: 'app-returns',
  imports: [RouterLink, LegalShellComponent, RefundPolicyContentComponent],
  templateUrl: './returns.html',
  styleUrl: '../legal-page.scss',
})
export class ReturnsComponent {
  protected readonly LEGAL_TERMS_VERSION = LEGAL_TERMS_VERSION;
}
