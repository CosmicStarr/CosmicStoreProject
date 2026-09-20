import { Component, inject, OnInit, signal } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { WishlistService } from '../../../core/services/wishlist-service';
import { CartService } from '../../../core/services/cart-service';
import { IWishlistItem } from '../../models/UserInfo';
import { SiteNavbarComponent } from '../../../core/components/site-navbar/site-navbar';
import { SiteFooterComponent } from '../../../core/components/site-footer/site-footer';

@Component({
  selector: 'app-wishlist',
  imports: [CurrencyPipe, RouterLink, SiteNavbarComponent, SiteFooterComponent],
  templateUrl: './wishlist.html',
  styleUrl: './wishlist.scss',
})
export class WishlistComponent implements OnInit {
  private wishlistService = inject(WishlistService);
  private cartService = inject(CartService);
  protected items = signal<IWishlistItem[]>([]);
  protected readonly loading = signal(true);
  protected readonly message = signal<string | null>(null);
  protected readonly isError = signal(false);
  protected readonly addingProductId = signal<string | null>(null);

  ngOnInit(): void {
    this.loadWishlist();
  }

  loadWishlist() {
    this.loading.set(true);
    this.wishlistService.getWishlist().subscribe({
      next: (items) => {
        this.items.set(Array.isArray(items) ? items : []);
        this.loading.set(false);
      },
      error: (err: { error?: { message?: string } }) => {
        this.loading.set(false);
        this.isError.set(true);
        this.message.set(err.error?.message || 'Wishlist could not be loaded.');
      },
    });
  }

  addToCart(item: IWishlistItem) {
    if (this.addingProductId()) return;

    this.addingProductId.set(item.productId);
    this.message.set(null);
    this.isError.set(false);

    this.cartService.addItem(item.productId, 1, item.sku).subscribe({
      next: () => {
        this.addingProductId.set(null);
        this.isError.set(false);
        this.message.set(`${item.nameEn} added to cart.`);
      },
      error: (err: { error?: { message?: string } }) => {
        this.addingProductId.set(null);
        this.isError.set(true);
        this.message.set(err.error?.message || 'Could not add to cart.');
      },
    });
  }

  removeItem(productId: string) {
    this.wishlistService.removeItem(productId).subscribe({
      next: () => {
        this.items.update((items) => items.filter((item) => item.productId !== productId));
        this.isError.set(false);
        this.message.set('Removed from wishlist.');
      },
      error: () => {
        this.isError.set(true);
        this.message.set('Could not remove this item.');
      },
    });
  }
}
