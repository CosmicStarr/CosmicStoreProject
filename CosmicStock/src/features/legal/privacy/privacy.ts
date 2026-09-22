import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LEGAL_TERMS_VERSION } from '../../../core/legal/legal-terms';
import { LegalShellComponent } from '../legal-shell';

@Component({
  selector: 'app-privacy',
  imports: [RouterLink, LegalShellComponent],
  templateUrl: './privacy.html',
  styleUrl: '../legal-page.scss',
})
export class PrivacyComponent {
  protected readonly LEGAL_TERMS_VERSION = LEGAL_TERMS_VERSION;
}
