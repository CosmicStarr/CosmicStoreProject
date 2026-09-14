import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import { IFlatProduct } from '../../models/flattenProduct';
import { sunParams } from '../../models/paramOptions';
import { AdminProductService } from '../../../core/services/admin-product';
import { IPagination } from '../../models/pagination';

const PAGE_SIZE_OPTIONS = [12, 24, 48];

@Component({
  imports: [RouterLink],
  selector: 'app-dashboard',
  styleUrl: './dashboard.scss',
  templateUrl: './dashboard.html',
})
export class DashboardComponent implements OnInit {
  private adminService = inject(AdminProductService);
  private searchChanges = new Subject<string>();

  protected sunParams = new sunParams();
  protected readonly pageSizeOptions = PAGE_SIZE_OPTIONS;

  protected readonly apiData = signal<IFlatProduct[]>([]);
  protected readonly categories = signal<string[]>([]);
  protected readonly pagination = signal<IPagination | undefined>(undefined);
  protected readonly loading = signal(false);
  protected readonly loadError = signal<string | null>(null);

  protected readonly totalPages = computed(() => this.pagination()?.TotalPages ?? 1);
  protected readonly totalItems = computed(() => this.pagination()?.TotalItems ?? 0);

  /** Human-readable "Showing 25-48 of 222" range for the current page. */
  protected readonly rangeLabel = computed(() => {
    const total = this.totalItems();
    if (total === 0) return 'No products';

    const size = this.sunParams.pageSize;
    const start = (this.sunParams.pageNumber - 1) * size + 1;
    const end = Math.min(start + this.apiData().length - 1, total);
    return `Showing ${start}-${end} of ${total}`;
  });

  publishing = false;
  message: string | null = null;
  isError = false;

  ngOnInit() {
    this.sunParams.pageSize = 24;

    this.searchChanges.pipe(debounceTime(400), distinctUntilChanged()).subscribe((term) => {
      this.sunParams.search = term;
      this.sunParams.pageNumber = 1;
      this.getProductsToEdit();
    });

    this.adminService.getCategories().subscribe({
      next: (categories) => this.categories.set(categories),
      error: () => this.categories.set([]),
    });

    this.getProductsToEdit();
  }

  getProductsToEdit() {
    this.loading.set(true);
    this.loadError.set(null);

    this.adminService.getAllProducts(this.sunParams).subscribe({
      next: (response) => {
        this.apiData.set(Array.isArray(response.result) ? response.result : []);
        this.pagination.set(response.Pagination);
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Error get products', err);
        this.apiData.set([]);
        this.pagination.set(undefined);
        this.loading.set(false);
        this.loadError.set(
          err.status === 401 || err.status === 403
            ? 'Your admin session has expired. Please sign in again.'
            : 'Could not load products. Please try again.'
        );
      },
    });
  }

  onSearch(term: string) {
    this.searchChanges.next(term);
  }

  onCategoryChange(category: string) {
    this.sunParams.category = category;
    this.sunParams.pageNumber = 1;
    this.getProductsToEdit();
  }

  onSortChange(sort: string) {
    this.sunParams.sort = sort;
    this.sunParams.pageNumber = 1;
    this.getProductsToEdit();
  }

  onPageSizeChange(size: string) {
    this.sunParams.pageSize = Number(size);
    this.sunParams.pageNumber = 1;
    this.getProductsToEdit();
  }

  goToPage(page: number) {
    if (page < 1 || page > this.totalPages() || page === this.sunParams.pageNumber) return;

    this.sunParams.pageNumber = page;
    this.getProductsToEdit();
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  publishOne(productId: string) {
    this.publishing = true;
    this.message = null;

    this.adminService.publishProduct(productId).subscribe({
      next: () => {
        this.publishing = false;
        this.message = 'Product published to storefront.';
        this.isError = false;
      },
      error: () => {
        this.publishing = false;
        this.message = 'Failed to publish product.';
        this.isError = true;
      },
    });
  }

  publishAll() {
    const ids = this.apiData().map((p) => p.id);
    if (!ids.length) return;

    this.publishing = true;
    this.message = null;

    this.adminService.publishBulk(ids).subscribe({
      next: (result) => {
        this.publishing = false;
        this.message = `Published ${result.length} product(s) to storefront.`;
        this.isError = false;
      },
      error: () => {
        this.publishing = false;
        this.message = 'Bulk publish failed.';
        this.isError = true;
      },
    });
  }
}
