import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { IPagination } from '../models/pagination';
import { sunParams } from '../models/paramOptions';
import { StoreProductsService } from '../../core/services/store-products';
import { AccountService } from '../../core/services/account-service';
import { OrderService } from '../../core/services/order-service';
import { IProductResponse } from '../models/productResponse';
import { SiteNavbarComponent } from '../../core/components/site-navbar/site-navbar';
import { SiteFooterComponent } from '../../core/components/site-footer/site-footer';


@Component({
  imports: [RouterLink, SiteNavbarComponent, CurrencyPipe, FormsModule, SiteFooterComponent],
  selector: 'app-products-component',
  styleUrl: './products-component.scss',
  templateUrl: './products-component.html',
})
export class ProductsComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private productService = inject(StoreProductsService);
  private accountService = inject(AccountService);
  private orderService = inject(OrderService);

  sunParams = new sunParams();
  searchInput = '';
  minPriceInput: number | null = null;
  maxPriceInput: number | null = null;
  p?: IPagination;
  protected readonly loading = signal(false);
  protected readonly pageSizeOptions = [12, 16, 20, 24];

  protected readonly products = signal<IProductResponse[]>([]);
  protected readonly pastPurchases = signal<IProductResponse[]>([]);
  protected readonly selectedCategory = signal('');
  protected readonly heroImages = computed(() =>
    this.products()
      .map((product) => product.bigImage?.trim())
      .filter((url): url is string => !!url)
      .slice(0, 2),
  );

  ngOnInit(): void {
    this.loadPastPurchases();
    this.route.queryParamMap.subscribe((params) => {
      this.sunParams.search = params.get('search') ?? '';
      this.sunParams.category = params.get('category') ?? '';
      this.selectedCategory.set(this.sunParams.category);
      this.sunParams.sort = params.get('sort') ?? '';
      this.sunParams.pageNumber = Number(params.get('page') ?? 1);
      this.sunParams.pageSize = this.parsePageSize(params.get('pageSize'));
      this.searchInput = this.sunParams.search;
      this.minPriceInput = params.get('minPrice') ? Number(params.get('minPrice')) : null;
      this.maxPriceInput = params.get('maxPrice') ? Number(params.get('maxPrice')) : null;
      this.sunParams.minPrice = this.minPriceInput ?? undefined;
      this.sunParams.maxPrice = this.maxPriceInput ?? undefined;
      this.loadProducts();
    });
  }

  loadProducts() {
    this.loading.set(true);

    this.productService.getJoinedProducts(this.sunParams).subscribe({
      next: (response) => {
        this.products.set(response?.result ?? []);
        this.p = response?.Pagination;
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Error fetching products', err);
        this.loading.set(false);
      },
    });
  }

  applyFilters() {
    this.updateQuery({
      search: this.searchInput.trim() || null,
      minPrice: this.minPriceInput?.toString() ?? null,
      maxPrice: this.maxPriceInput?.toString() ?? null,
      page: '1',
    });
  }

  onSortSelect(event: Event) {
    const sort = (event.target as HTMLSelectElement).value;
    this.updateQuery({ sort: sort || null, page: '1' });
  }

  onPageSizeSelect(event: Event) {
    const pageSize = this.parsePageSize((event.target as HTMLSelectElement).value);
    this.updateQuery({
      pageSize: pageSize === 12 ? null : pageSize.toString(),
      page: '1',
    });
  }

  goToPage(page: number) {
    if (!this.p?.TotalPages || page < 1 || page > this.p.TotalPages) return;
    this.updateQuery({ page: page.toString() });
  }

  showingFrom(): number {
    if (!this.p?.TotalItems) return 0;
    return (this.sunParams.pageNumber - 1) * this.sunParams.pageSize + 1;
  }

  showingTo(): number {
    if (!this.p?.TotalItems) return 0;
    return Math.min(this.sunParams.pageNumber * this.sunParams.pageSize, this.p.TotalItems);
  }

  private updateQuery(changes: Record<string, string | null>) {
    const queryParams: Record<string, string | null> = {
      search: this.sunParams.search || null,
      category: this.sunParams.category || null,
      sort: this.sunParams.sort || null,
      minPrice: this.sunParams.minPrice?.toString() ?? null,
      maxPrice: this.sunParams.maxPrice?.toString() ?? null,
      pageSize: this.sunParams.pageSize === 12 ? null : this.sunParams.pageSize.toString(),
      page: this.sunParams.pageNumber > 1 ? this.sunParams.pageNumber.toString() : null,
      ...changes,
    };

    this.router.navigate([], {
      relativeTo: this.route,
      queryParams,
      queryParamsHandling: 'merge',
    });
  }

  private parsePageSize(value: string | null): number {
    const size = Number(value);
    return this.pageSizeOptions.includes(size) ? size : 12;
  }

  private loadPastPurchases() {
    const user = this.accountService.currentUserValue;
    if (!user?.token || user.isGuest) {
      this.pastPurchases.set([]);
      return;
    }

    this.orderService.getRecentPurchases().subscribe({
      next: (products) => this.pastPurchases.set(Array.isArray(products) ? products.slice(0, 3) : []),
      error: () => this.pastPurchases.set([]),
    });
  }
}
