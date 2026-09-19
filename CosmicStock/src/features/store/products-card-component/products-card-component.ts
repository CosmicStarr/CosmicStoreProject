import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
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
  addingToCart = false;
  wishlistLoading = false;
  inWishlist = signal(false);
  cartMessage: string | null = null;
  wishlistMessage: string | null = null;

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

  shortDescription = computed(() => {
    const text = this.product()?.descriptionEn?.trim();
    if (!text) return 'No description is available for this product yet.';
    return text.length > 220 ? `${text.slice(0, 220).trim()}…` : text;
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
    if (!product) return;

    if (!this.inStock()) {
      this.cartMessage = 'This product is out of stock.';
      return;
    }

    this.addingToCart = true;
    this.cartMessage = null;

    this.cartService.addItem(product.id, this.quantity(), this.selectedSku()).subscribe({
      next: () => {
        this.cartMessage = 'Added to cart!';
        this.addingToCart = false;
      },
      error: (err: { error?: { message?: string } }) => {
        this.cartMessage = err.error?.message ?? 'Could not add to cart.';
        this.addingToCart = false;
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
