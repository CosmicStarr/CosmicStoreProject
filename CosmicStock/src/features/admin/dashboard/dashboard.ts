import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { IFlatProduct } from '../../models/flattenProduct';
import { sunParams } from '../../models/paramOptions';
import { AdminProductService } from '../../../core/services/admin-product';
import { IPagination } from '../../models/pagination';

@Component({
  imports: [RouterLink],
  selector: 'app-dashboard',
  styleUrl: './dashboard.scss',
  templateUrl: './dashboard.html',
})
export class DashboardComponent implements OnInit {
  private sun = new sunParams();
  private adminService = inject(AdminProductService);

  p?: IPagination;
  protected readonly apiData = signal<IFlatProduct[]>([]);
  publishing = false;
  message: string | null = null;
  isError = false;

  ngOnInit() {
    this.getProductsToEdit();
  }

  getProductsToEdit() {
    this.adminService.getAllProducts(this.sun).subscribe({
      next: (response) => {
        this.apiData.set(response.result ?? []);
        this.p = response.Pagination;
      },
      error: (err) => console.error('Error get products', err),
    });
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
