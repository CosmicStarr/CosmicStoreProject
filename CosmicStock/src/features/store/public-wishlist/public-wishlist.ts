import { Component, inject, OnInit, signal } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { WishlistService } from '../../../core/services/wishlist-service';
import { CartService } from '../../../core/services/cart-service';
import { IPublicWishlist, IWishlistItem } from '../../models/UserInfo';
import { SiteNavbarComponent } from '../../../core/components/site-navbar/site-navbar';
import { SiteFooterComponent } from '../../../core/components/site-footer/site-footer';

@Component({
  selector: 'app-public-wishlist',
  imports: [CurrencyPipe, RouterLink, SiteNavbarComponent, SiteFooterComponent],
  templateUrl: './public-wishlist.html',
  styleUrl: './public-wishlist.scss',
})
export class PublicWishlistComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private wishlistService = inject(WishlistService);
  private cartService = inject(CartService);

  protected list = signal<IPublicWishlist | null>(null);
  protected readonly loading = signal(true);
  protected readonly message = signal<string | null>(null);
  protected readonly isError = signal(false);
  protected readonly addingProductId = signal<string | null>(null);

  ngOnInit(): void {
    const publicId = this.route.snapshot.paramMap.get('publicId')?.trim() ?? '';
    if (!publicId) {
      this.loading.set(false);
      this.isError.set(true);
      this.message.set('That gift registry was not found.');
      return;
    }

    this.wishlistService.getPublicWishlist(publicId).subscribe({
      next: (list) => {
        this.list.set(list);
        this.loading.set(false);
      },
      error: (err: { error?: { message?: string } }) => {
        this.loading.set(false);
        this.isError.set(true);
        this.message.set(err.error?.message || 'That gift registry was not found.');
      },
    });
  }

  addToCart(item: IWishlistItem) {
    const wishlistId = this.list()?.id;
    if (!wishlistId || this.addingProductId()) return;

    this.addingProductId.set(item.productId);
    this.message.set(null);
    this.isError.set(false);

    this.cartService.addItem(item.productId, 1, item.sku, wishlistId).subscribe({
      next: () => {
        this.addingProductId.set(null);
        this.isError.set(false);
        this.message.set(`${item.nameEn} added to cart for ${this.list()?.maskedShippingLabel}.`);
      },
      error: (err: { error?: { message?: string } }) => {
        this.addingProductId.set(null);
        this.isError.set(true);
        this.message.set(err.error?.message || 'Could not add to cart.');
      },
    });
  }
}
