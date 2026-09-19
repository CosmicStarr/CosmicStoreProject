import { Component, inject, OnInit } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { CartService } from '../../services/cart-service';

@Component({
  selector: 'app-cart-panel',
  imports: [CurrencyPipe, RouterLink],
  templateUrl: './cart-panel.html',
  styleUrl: './cart-panel.scss',
  host: {
    '(document:keydown.escape)': 'onEscape()',
  },
})
export class CartPanelComponent implements OnInit {
  protected cartService = inject(CartService);

  ngOnInit(): void {
    this.cartService.loadCart();
  }

  onEscape() {
    if (this.cartService.panelOpen()) {
      this.cartService.closePanel();
    }
  }

  removeItem(sku: string) {
    this.cartService.removeItem(sku);
  }
}
