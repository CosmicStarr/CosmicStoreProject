import { afterNextRender, Component, inject } from '@angular/core';
import { ViewportScroller } from '@angular/common';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { SiteNavbarComponent } from '../../core/components/site-navbar/site-navbar';
import { SiteFooterComponent } from '../../core/components/site-footer/site-footer';

@Component({
  selector: 'app-legal-shell',
  imports: [RouterLink, RouterLinkActive, SiteNavbarComponent, SiteFooterComponent],
  templateUrl: './legal-shell.html',
  styleUrl: './legal-page.scss',
})
export class LegalShellComponent {
  constructor() {
    const viewport = inject(ViewportScroller);
    afterNextRender(() => viewport.scrollToPosition([0, 0]));
  }
}
