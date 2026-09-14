import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { IPagination } from '../models/pagination';
import { sunParams } from '../models/paramOptions';
import { StoreProductsService } from '../../core/services/store-products';
import { ICategorySummary, IProductResponse } from '../models/productResponse';
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

  sunParams = new sunParams();
  searchInput = '';
  minPriceInput: number | null = null;
  maxPriceInput: number | null = null;
  p?: IPagination;
  protected readonly loading = signal(false);

  protected readonly products = signal<IProductResponse[]>([]);
  protected readonly categories = signal<ICategorySummary[]>([]);
  protected readonly heroImages = computed(() =>
    this.products()
      .map((product) => product.bigImage?.trim())
      .filter((url): url is string => !!url)
      .slice(0, 2),
  );

  ngOnInit(): void {
    this.productService.getCategories().subscribe({
      next: (categories) => this.categories.set(categories),
    });

    this.route.queryParamMap.subscribe((params) => {
      this.sunParams.search = params.get('search') ?? '';
      this.sunParams.category = params.get('category') ?? '';
      this.sunParams.sort = params.get('sort') ?? '';
      this.sunParams.pageNumber = Number(params.get('page') ?? 1);
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

  selectCategory(category: string | null) {
    this.updateQuery({
      category,
      page: '1',
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
      page: this.sunParams.pageNumber > 1 ? this.sunParams.pageNumber.toString() : null,
      ...changes,
    };

    this.router.navigate([], {
      relativeTo: this.route,
      queryParams,
      queryParamsHandling: 'merge',
    });
  }
}
