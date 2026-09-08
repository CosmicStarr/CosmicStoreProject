import { Component, inject, OnInit, signal } from '@angular/core';
import { sunParams } from '../../models/paramOptions';
import { StoreProductsService } from '../../../core/services/store-products';
import { IProductResponse } from '../../models/productResponse';
import { AdminProductService } from '../../../core/services/admin-product';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../env/environment';
import { IFlatProduct } from '../../models/flattenProduct';
import { SiteNavbarComponent } from "../../../core/components/site-navbar/site-navbar";

@Component({
  imports: [RouterLink, SiteNavbarComponent],
  selector: 'app-home-component',
  styleUrl: './home-component.scss',
  templateUrl: './home-component.html',
})
export class HomeComponent implements OnInit {
  private httpClient = inject(HttpClient);
  protected readonly title = ('CosmicStock');
  protected readonly description = ('CosmicStock is a web application that allows users to browse and purchase products from the CosmicStoreAPI. The application is built using Angular and TypeScript, and it communicates with the CosmicStoreAPI to retrieve product data. Users can view product details, add products to their cart, and complete purchases through the application.');
  protected readonly baseUrl = environment.baseUrl;
  protected readonly apiData = signal<IFlatProduct[]>([]);

  private route = inject(ActivatedRoute);
  productService = inject(StoreProductsService);
  adminService = inject(AdminProductService);
  sunParams = new sunParams();
  highlightType: string = 'NewArrival';

  productId!: string;
  protected readonly products = signal<IProductResponse[]>([]);

  ngOnInit(): void {
    this.productId = this.route.snapshot.paramMap.get('id') || '';
    this.getAllProducts(this.highlightType);
    this.getProduct();
  }

  getProduct() {
    this.adminService.getProductById(this.productId).subscribe({
      error: (err) => console.error('Failed to load product metadata', err)
    });
  }

  getAllProducts(highlightType: string) {
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
      },
      error: (err) => {
        console.error('Error fetching products', err);
      }
    });
  }
}
