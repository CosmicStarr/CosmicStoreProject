import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { StoreProductsService } from '../../../core/services/store-products';
import { ICategorySummary, IProductResponse } from '../../models/productResponse';
import { SiteNavbarComponent } from '../../../core/components/site-navbar/site-navbar';
import { SiteFooterComponent } from '../../../core/components/site-footer/site-footer';
import {
  SHIPPING_PROCESSING_BUSINESS_DAYS,
  SHIPPING_TRANSIT_BUSINESS_DAYS_MAX,
  SHIPPING_TRANSIT_BUSINESS_DAYS_MIN,
} from '../../../core/legal/shipping-policy';
import { REFUND_QUALITY_DAYS_AFTER_DELIVERY } from '../../../core/legal/refund-policy';

@Component({
  imports: [RouterLink, SiteNavbarComponent, SiteFooterComponent, CurrencyPipe],
  selector: 'app-home-component',
  styleUrl: './home-component.scss',
  templateUrl: './home-component.html',
})
export class HomeComponent implements OnInit {
  private productService = inject(StoreProductsService);
  highlightType = 'Featured';
  protected readonly products = signal<IProductResponse[]>([]);
  protected readonly categories = signal<ICategorySummary[]>([]);
  protected readonly loading = signal(false);

  protected readonly heroProduct = computed(() => this.products()[0] ?? null);
  protected readonly campaignCategories = computed(() => this.categories().slice(0, 2));
  protected readonly overflowProducts = computed(() => this.products().slice(2, 6));
  protected readonly processingDays = SHIPPING_PROCESSING_BUSINESS_DAYS;
  protected readonly transitMin = SHIPPING_TRANSIT_BUSINESS_DAYS_MIN;
  protected readonly transitMax = SHIPPING_TRANSIT_BUSINESS_DAYS_MAX;
  protected readonly qualityDays = REFUND_QUALITY_DAYS_AFTER_DELIVERY;

  ngOnInit(): void {
    this.productService.getCategories().subscribe({
      next: (categories) => this.categories.set(categories),
    });
    this.getAllProducts(this.highlightType);
  }

  getAllProducts(highlightType: string) {
    this.highlightType = highlightType;
    this.loading.set(true);
    this.productService.getHighlightedProducts(highlightType).subscribe({
      next: (response: unknown) => {
        if (Array.isArray(response)) {
          this.products.set(response);
        } else if (response && typeof response === 'object' && Array.isArray((response as { result?: unknown[] }).result)) {
          this.products.set((response as { result: IProductResponse[] }).result);
        } else if (response && typeof response === 'object' && Array.isArray((response as { value?: unknown[] }).value)) {
          this.products.set((response as { value: IProductResponse[] }).value);
        } else {
          this.products.set([]);
        }
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Error fetching products', err);
        this.products.set([]);
        this.loading.set(false);
      }
    });
  }

  highlightLabel(): string {
    if (this.highlightType === 'Featured') return 'featured';
    if (this.highlightType === 'TopSelling') return 'top selling';
    return 'new arrival';
  }

  scrollToTop(): void {
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }
}
