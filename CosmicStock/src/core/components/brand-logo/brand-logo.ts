import { NgOptimizedImage } from '@angular/common';
import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-brand-logo',
  imports: [NgOptimizedImage, RouterLink],
  template: `
    @if (linkHome()) {
      <a routerLink="/" class="brand-logo" [class.compact]="compact()" [attr.aria-label]="ariaLabel()">
        <img
          ngSrc="/images/cosmicstore-logo.png"
          width="160"
          height="160"
          alt=""
          class="brand-logo-mark"
          [priority]="priority()" />
        @if (showWordmark()) {
          <span class="brand-logo-text">{{ wordmark() }}</span>
        }
      </a>
    } @else {
      <span class="brand-logo" [class.compact]="compact()" aria-hidden="true">
        <img
          ngSrc="/images/cosmicstore-logo.png"
          width="160"
          height="160"
          alt=""
          class="brand-logo-mark" />
        @if (showWordmark()) {
          <span class="brand-logo-text">{{ wordmark() }}</span>
        }
      </span>
    }
  `,
  styles: `
    :host {
      display: inline-flex;
    }

    .brand-logo {
      display: inline-flex;
      align-items: center;
      gap: 10px;
      text-decoration: none;
      color: inherit;
    }

    .brand-logo-mark {
      width: 76px;
      height: 76px;
      object-fit: contain;
      flex-shrink: 0;
    }

    .brand-logo.compact .brand-logo-mark {
      width: 40px;
      height: 40px;
    }

    .brand-logo-text {
      font-size: 1.55rem;
      font-weight: 800;
      color: #0b1f3a;
      letter-spacing: -0.03em;
      line-height: 1;
    }

    .brand-logo.compact .brand-logo-text {
      font-size: 1.1rem;
    }
  `,
})
export class BrandLogoComponent {
  readonly linkHome = input(true);
  readonly showWordmark = input(true);
  readonly wordmark = input('CosmicStore');
  readonly compact = input(false);
  readonly priority = input(false);
  readonly ariaLabel = input('CosmicStore home');
}
