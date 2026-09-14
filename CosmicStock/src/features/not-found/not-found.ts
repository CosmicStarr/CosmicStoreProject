import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-not-found',
  imports: [RouterLink],
  template: `
    <main class="missing-page">
      <h1>Page not found</h1>
      <p>That address is not part of CosmicStore.</p>
      <a routerLink="/" class="home-link">Back to home</a>
    </main>
  `,
  styles: `
    .missing-page {
      max-width: 40rem;
      margin: 6rem auto;
      padding: 0 1.5rem;
      text-align: center;
    }

    h1 {
      margin-bottom: 0.75rem;
    }

    p {
      margin-bottom: 1.5rem;
      color: #64748b;
    }

    .home-link {
      display: inline-block;
      background: #ff9100;
      color: #fff;
      padding: 0.75rem 1.5rem;
      border-radius: 999px;
      font-weight: 700;
      text-decoration: none;
    }
  `,
})
export class NotFoundComponent {}
