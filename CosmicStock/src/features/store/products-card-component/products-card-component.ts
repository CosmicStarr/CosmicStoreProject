import { Component, inject, OnInit, signal } from '@angular/core';
import { IProductResponse } from '../../models/productResponse';
import { ActivatedRoute, RouterLink } from "@angular/router";
import { StoreProductsService } from '../../../core/services/store-products';
import { CurrencyPipe } from '@angular/common';
import { CartService } from '../../../core/services/cart-service';

@Component({
  imports: [CurrencyPipe, RouterLink],
  selector: 'app-products-card-component',
  styleUrl: './products-card-component.scss',
  templateUrl: './products-card-component.html',
})
export class ProductDetailComponent implements OnInit {
  private productService = inject(StoreProductsService);
  private cartService = inject(CartService);
  private route = inject(ActivatedRoute);

  product = signal<IProductResponse | null>(null);
  quantity = signal(1);
  addingToCart = false;
  cartMessage: string | null = null;

  productId: string | null = null;

  ngOnInit(): void {
    this.route.paramMap.subscribe(params => {
      this.productId = params.get('id');
      if (this.productId) {
        this.getProductDetails(this.productId);
      }
    });
  }

  incrementQty() {
    this.quantity.update(q => q + 1);
  }

  decrementQty() {
    this.quantity.update(q => Math.max(1, q - 1));
  }

  addToCart() {
    const product = this.product();
    if (!product) return;

    this.addingToCart = true;
    this.cartMessage = null;

    this.cartService.addItem(product.id, this.quantity()).subscribe({
      next: () => {
        this.cartMessage = 'Added to cart!';
        this.addingToCart = false;
      },
      error: () => {
        this.cartMessage = 'Could not add to cart.';
        this.addingToCart = false;
      },
    });
  }

  getProductDetails(productId: string) {
    this.productService.getProductById(productId).subscribe({
      next: (productDetails) => {
        this.product.set(productDetails);
      },
      error: (err) => {
        console.error('Error fetching product details', err);
      }
    });
  }
}
