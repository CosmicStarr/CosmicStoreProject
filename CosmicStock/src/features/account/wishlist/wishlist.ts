import { Component, inject, OnInit, signal } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { WishlistService } from '../../../core/services/wishlist-service';
import { IWishlistItem } from '../../models/UserInfo';

@Component({
  selector: 'app-wishlist',
  imports: [CurrencyPipe, RouterLink],
  templateUrl: './wishlist.html',
  styleUrl: './wishlist.scss',
})
export class WishlistComponent implements OnInit {
  private wishlistService = inject(WishlistService);
  protected items = signal<IWishlistItem[]>([]);
  loading = true;

  ngOnInit(): void {
    this.loadWishlist();
  }

  loadWishlist() {
    this.wishlistService.getWishlist().subscribe({
      next: (items) => {
        this.items.set(items);
        this.loading = false;
      },
      error: () => {
        this.loading = false;
      },
    });
  }

  removeItem(productId: string) {
    this.wishlistService.removeItem(productId).subscribe({
      next: () => this.loadWishlist(),
    });
  }
}
