import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { excerptProductDescription } from '../../../core/utils/product-text';
import { IProductResponse } from '../../models/productResponse';
import { ActivatedRoute, Router, RouterLink } from "@angular/router";
import { StoreProductsService } from '../../../core/services/store-products';
import { CurrencyPipe } from '@angular/common';
import { CartService } from '../../../core/services/cart-service';
import { WishlistService } from '../../../core/services/wishlist-service';
import { AccountService } from '../../../core/services/account-service';
import { SiteNavbarComponent } from "../../../core/components/site-navbar/site-navbar";
import { SiteFooterComponent } from "../../../core/components/site-footer/site-footer";

@Component({
  imports: [CurrencyPipe, RouterLink, SiteNavbarComponent, SiteFooterComponent, FormsModule],
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
  private router = inject(Router);

  product = signal<IProductResponse | null>(null);
  loadFailed = signal(false);
  relatedProducts = signal<IProductResponse[]>([]);
  featuredProducts = signal<IProductResponse[]>([]);
  quantity = signal(1);
  selectedImageIndex = signal(0);
  isZooming = signal(false);
  zoomOrigin = signal('center center');
  activeTab = signal<'description' | 'reviews'>('description');
  sidebarSearch = '';
  addingToCart = signal(false);
  wishlistLoading = signal(false);
  inWishlist = signal(false);
  cartMessage = signal<string | null>(null);
  wishlistMessage = signal<string | null>(null);
  wishlistError = signal(false);
  shareMessage = signal<string | null>(null);
  canNativeShare = signal(false);

  galleryImages = computed(() => {
    const product = this.product();
    if (!product) return [];

    const images: { url: string; sku: string; type?: string }[] = [];
    const addImage = (url: string | undefined, sku: string | undefined, type?: string) => {
      const trimmedUrl = url?.trim();
      if (!trimmedUrl || images.some((image) => image.url === trimmedUrl)) {
        return;
      }

      images.push({
        url: trimmedUrl,
        sku: sku?.trim() || product.sku,
        type: type?.trim() || undefined,
      });
    };

    const pictures = product.pictures ?? [];
    const heroPicture = pictures.find((picture) => picture.photoUrl?.trim() === product.bigImage?.trim());
    addImage(product.bigImage, heroPicture?.skuPhoto ?? product.sku, heroPicture?.type);
    for (const picture of pictures) {
      addImage(picture.photoUrl, picture.skuPhoto, picture.type);
    }

    return images;
  });

  selectedImageUrl = computed(() => {
    const images = this.galleryImages();
    if (!images.length) return '';
    const index = Math.min(this.selectedImageIndex(), images.length - 1);
    return images[index].url;
  });

  selectedSku = computed(() => {
    const product = this.product();
    const images = this.galleryImages();
    if (!images.length) {
      return product?.sku ?? '';
    }

    const index = Math.min(this.selectedImageIndex(), images.length - 1);
    return images[index].sku || product?.sku || '';
  });

  selectedType = computed(() => {
    const images = this.galleryImages();
    if (!images.length) return '';
    const index = Math.min(this.selectedImageIndex(), images.length - 1);
    return images[index].type ?? '';
  });

  inStock = computed(() => (this.product()?.stockQuantity ?? 0) > 0);

  wishlistLabel = computed(() => {
    if (this.wishlistLoading()) return 'Updating wishlist…';
    return this.inWishlist() ? 'Saved to wishlist' : 'Add to wishlist';
  });

  shareLinks = computed(() => {
    const product = this.product();
    const url = encodeURIComponent(this.pageUrl());
    const name = encodeURIComponent(product?.nameEn ?? 'this CosmicStore product');
    const image = encodeURIComponent(product?.bigImage ?? '');
    return {
      facebook: `https://www.facebook.com/sharer/sharer.php?u=${url}`,
      twitter: `https://twitter.com/intent/tweet?url=${url}&text=${name}`,
      pinterest: `https://pinterest.com/pin/create/button/?url=${url}&media=${image}&description=${name}`,
      whatsapp: `https://api.whatsapp.com/send?text=${name}%20${url}`,
      email: `mailto:?subject=${name}&body=I found this on CosmicStore:%0A${url}`,
    };
  });

  shortDescription = computed(() => {
    const product = this.product();
    const dedicated = product?.shortDescription?.trim();
    if (dedicated) return dedicated;
    return excerptProductDescription(product?.descriptionEn);
  });

  productId: string | null = null;

  ngOnInit(): void {
    this.canNativeShare.set(typeof navigator !== 'undefined' && typeof navigator.share === 'function');
    this.route.paramMap.subscribe(params => {
      this.productId = params.get('id');
      if (this.productId) {
        this.getProductDetails(this.productId);
      }
    });
  }

  incrementQty() {
    const max = this.product()?.stockQuantity ?? 1;
    this.quantity.update(q => Math.min(q + 1, Math.max(1, max)));
  }

  decrementQty() {
    this.quantity.update(q => Math.max(1, q - 1));
  }

  searchStore(): void {
    const term = this.sidebarSearch.trim();
    void this.router.navigate(['/store'], {
      queryParams: term ? { search: term, page: 1 } : { search: null, page: 1 },
    });
  }

  scrollToTop(): void {
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  addToCart() {
    const product = this.product();
    if (!product || this.addingToCart()) return;

    if (!this.inStock()) {
      this.cartMessage.set('This product is out of stock.');
      return;
    }

    this.addingToCart.set(true);
    this.cartMessage.set(null);

    this.cartService.addItem(product.id, this.quantity(), this.selectedSku()).subscribe({
      next: () => {
        this.cartMessage.set('Added to cart!');
        this.addingToCart.set(false);
      },
      error: (err: { error?: { message?: string } }) => {
        this.cartMessage.set(err.error?.message ?? 'Could not add to cart.');
        this.addingToCart.set(false);
      },
    });
  }

  selectImage(index: number) {
    if (index >= 0 && index < this.galleryImages().length) {
      this.selectedImageIndex.set(index);
      this.endZoom();
    }
  }

  previousImage() {
    const count = this.galleryImages().length;
    if (count <= 1) return;
    this.selectedImageIndex.update((index) => (index - 1 + count) % count);
    this.endZoom();
  }

  nextImage() {
    const count = this.galleryImages().length;
    if (count <= 1) return;
    this.selectedImageIndex.update((index) => (index + 1) % count);
    this.endZoom();
  }

  startZoom(): void {
    this.isZooming.set(true);
  }

  endZoom(): void {
    this.isZooming.set(false);
    this.zoomOrigin.set('center center');
  }

  moveZoom(event: MouseEvent): void {
    const stage = event.currentTarget as HTMLElement;
    const image = stage.querySelector('img');
    const bounds = (image ?? stage).getBoundingClientRect();
    const x = ((event.clientX - bounds.left) / bounds.width) * 100;
    const y = ((event.clientY - bounds.top) / bounds.height) * 100;
    this.zoomOrigin.set(`${Math.min(100, Math.max(0, x))}% ${Math.min(100, Math.max(0, y))}%`);
  }

  getProductDetails(productId: string) {
    this.loadFailed.set(false);
    this.product.set(null);
    this.productService.getProductById(productId).subscribe({
      next: (productDetails) => {
        this.product.set(productDetails);
        this.selectedImageIndex.set(0);
        this.endZoom();
        this.quantity.set(1);
        this.activeTab.set('description');
        this.addingToCart.set(false);
        this.cartMessage.set(null);
        this.wishlistMessage.set(null);
        this.wishlistError.set(false);
        this.shareMessage.set(null);
        this.loadWishlistState(productId);
        this.loadRelatedProducts(productId);
        this.loadFeaturedProducts(productId);
      },
      error: (err) => {
        console.error('Error fetching product details', err);
        this.loadFailed.set(true);
      }
    });
  }

  loadRelatedProducts(productId: string) {
    this.productService.getRelatedProducts(productId).subscribe({
      next: (items) => this.relatedProducts.set(items),
    });
  }

  loadFeaturedProducts(productId: string) {
    this.productService.getHighlightedProducts('Featured').subscribe({
      next: (items) => this.featuredProducts.set(items.filter((item) => item.id !== productId).slice(0, 3)),
    });
  }

  loadWishlistState(productId: string) {
    if (!this.canUseWishlist()) {
      this.inWishlist.set(false);
      return;
    }

    this.wishlistService.isInWishlist(productId).subscribe({
      next: (isSaved) => this.inWishlist.set(isSaved),
      error: () => this.inWishlist.set(false),
    });
  }

  toggleWishlist() {
    const product = this.product();
    if (!product) return;

    if (!this.canUseWishlist()) {
      void this.router.navigate(['/login'], {
        queryParams: { returnUrl: this.router.url },
      });
      return;
    }

    this.wishlistLoading.set(true);
    this.wishlistMessage.set(null);
    this.wishlistError.set(false);

    if (this.inWishlist()) {
      this.wishlistService.removeItem(product.id).subscribe({
        next: () => {
          this.inWishlist.set(false);
          this.wishlistLoading.set(false);
          this.wishlistError.set(false);
          this.wishlistMessage.set('Removed from wishlist.');
        },
        error: () => {
          this.wishlistLoading.set(false);
          this.wishlistError.set(true);
          this.wishlistMessage.set('Could not update wishlist.');
        },
      });
      return;
    }

    this.wishlistService.addItem(product.id).subscribe({
      next: () => {
        this.inWishlist.set(true);
        this.wishlistLoading.set(false);
        this.wishlistError.set(false);
        this.wishlistMessage.set('Saved to wishlist.');
      },
      error: (err: { error?: { message?: string } }) => {
        this.wishlistLoading.set(false);
        this.wishlistError.set(true);
        this.wishlistMessage.set(err.error?.message || 'Could not update wishlist.');
      },
    });
  }

  copyShareLink() {
    const url = this.pageUrl();
    if (typeof navigator !== 'undefined' && navigator.clipboard?.writeText) {
      void navigator.clipboard.writeText(url).then(
        () => this.shareMessage.set('Product link copied.'),
        () => this.shareMessage.set(url),
      );
      return;
    }

    this.shareMessage.set(url);
  }

  shareNative() {
    const product = this.product();
    if (!product || typeof navigator === 'undefined' || !navigator.share) return;

    void navigator.share({
      title: product.nameEn,
      text: `Check out ${product.nameEn} on CosmicStore`,
      url: this.pageUrl(),
    }).catch(() => undefined);
  }

  private canUseWishlist(): boolean {
    const user = this.accountService.currentUserValue;
    return !!user && user.isGuest !== true;
  }

  private pageUrl(): string {
    if (typeof window === 'undefined') return '';
    return window.location.href.split('#')[0];
  }
}
