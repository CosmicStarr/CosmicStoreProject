import { Component, inject, OnInit } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { CartService } from '../../../core/services/cart-service';

@Component({
  selector: 'app-cart-component',
  imports: [CurrencyPipe, RouterLink],
  templateUrl: './cart-component.html',
  styleUrl: './cart-component.scss',
})
export class CartComponent implements OnInit {
  protected cartService = inject(CartService);
  protected itemCount = this.cartService.itemCount;

  ngOnInit(): void {
    this.cartService.loadCart();
  }

  removeItem(sku: string) {
    this.cartService.removeItem(sku);
  }
}
