import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { IProductResponse } from '../../models/productResponse';
import { ActivatedRoute, RouterLink } from "@angular/router";
import { StoreProductsService } from '../../../core/services/store-products';
import { CurrencyPipe } from '@angular/common';
import { CartService } from '../../../core/services/cart-service';
import { WishlistService } from '../../../core/services/wishlist-service';
import { AccountService } from '../../../core/services/account-service';
import { SiteNavbarComponent } from "../../../core/components/site-navbar/site-navbar";

@Component({
  imports: [CurrencyPipe, RouterLink, SiteNavbarComponent],
  selector: 'app-products-card-component',
  styleUrl: './products-card-component.scss',
  templateUrl: './products-card-component.html',
})
export class ProductDetailComponent implements OnInit {
  private productService = inject(StoreProductsService);
  private cartService = inject(CartService);
  private wishlistService = inject(WishlistService);
  private accountService = inject(AccountService);
  private route = inject(ActivatedRoute);

  product = signal<IProductResponse | null>(null);
  relatedProducts = signal<IProductResponse[]>([]);
  quantity = signal(1);
  selectedImageIndex = signal(0);
  addingToCart = false;
  wishlistLoading = false;
  inWishlist = signal(false);
  cartMessage: string | null = null;
  wishlistMessage: string | null = null;

  galleryImages = computed(() => {
    const product = this.product();
    if (!product) return [];

    const urls: string[] = [];
    const addUrl = (url: string | undefined) => {
      const trimmed = url?.trim();
      if (trimmed && !urls.includes(trimmed)) {
        urls.push(trimmed);
      }
    };

    addUrl(product.bigImage);
    for (const picture of product.pictures ?? []) {
      addUrl(picture.photoUrl);
    }

    return urls;
  });

  selectedImageUrl = computed(() => {
    const images = this.galleryImages();
    if (!images.length) return '';
    const index = Math.min(this.selectedImageIndex(), images.length - 1);
    return images[index];
  });

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

  selectImage(index: number) {
    if (index >= 0 && index < this.galleryImages().length) {
      this.selectedImageIndex.set(index);
    }
  }

  previousImage() {
    const count = this.galleryImages().length;
    if (count <= 1) return;
    this.selectedImageIndex.update((index) => (index - 1 + count) % count);
  }

  nextImage() {
    const count = this.galleryImages().length;
    if (count <= 1) return;
    this.selectedImageIndex.update((index) => (index + 1) % count);
  }

  getProductDetails(productId: string) {
    this.productService.getProductById(productId).subscribe({
      next: (productDetails) => {
        this.product.set(productDetails);
        this.selectedImageIndex.set(0);
        this.loadWishlistState(productId);
        this.loadRelatedProducts(productId);
      },
      error: (err) => {
        console.error('Error fetching product details', err);
      }
    });
  }

  loadRelatedProducts(productId: string) {
    this.productService.getRelatedProducts(productId).subscribe({
      next: (items) => this.relatedProducts.set(items),
    });
  }

  loadWishlistState(productId: string) {
    if (!this.accountService.currentUserValue) {
      this.inWishlist.set(false);
      return;
    }

    this.wishlistService.isInWishlist(productId).subscribe({
      next: (isSaved) => this.inWishlist.set(isSaved),
    });
  }

  toggleWishlist() {
    const product = this.product();
    if (!product) return;

    if (!this.accountService.currentUserValue) {
      this.wishlistMessage = 'Sign in to save items to your wishlist.';
      return;
    }

    this.wishlistLoading = true;
    this.wishlistMessage = null;

    if (this.inWishlist()) {
      this.wishlistService.removeItem(product.id).subscribe({
        next: () => {
          this.inWishlist.set(false);
          this.wishlistLoading = false;
          this.wishlistMessage = 'Removed from wishlist.';
        },
        error: () => {
          this.wishlistLoading = false;
          this.wishlistMessage = 'Could not update wishlist.';
        },
      });
      return;
    }

    this.wishlistService.addItem(product.id).subscribe({
      next: () => {
        this.inWishlist.set(true);
        this.wishlistLoading = false;
        this.wishlistMessage = 'Saved to wishlist.';
      },
      error: () => {
        this.wishlistLoading = false;
        this.wishlistMessage = 'Could not update wishlist.';
      },
    });
  }
}
