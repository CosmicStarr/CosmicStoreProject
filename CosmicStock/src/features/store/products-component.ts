import { Component, inject, OnInit, signal } from '@angular/core';
import { IPagination } from '../models/pagination';
import { sunParams } from '../models/paramOptions';
import { StoreProductsService } from '../../core/services/store-products';
import { IProductResponse } from '../models/productResponse';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { SiteNavbarComponent } from "../../core/components/site-navbar/site-navbar";


@Component({
  imports: [RouterLink, SiteNavbarComponent],
  selector: 'app-products-component',
  styleUrl: './products-component.scss',
  templateUrl: './products-component.html',
})
export class ProductsComponent implements OnInit {
  private route = inject(ActivatedRoute);
  productService = inject(StoreProductsService)
  sunParams = new sunParams()
  p?:IPagination;
  productId!: string;
  protected readonly products = signal<IProductResponse[]>([]);
  ngOnInit(): void {
    this.productId = this.route.snapshot.paramMap.get('id') || '';
    this.getAllProducts()
  }

  

getAllProducts() {
    this.productService.getJoinedProducts(this.sunParams).subscribe({
      next: (response) => {
          this.products.set(response?.result ?? []);
        this.p = response?.Pagination; // Assigning data to the 'p' property defined on line 15
      },
      error: (err) => {
        console.error('Error fetching products', err);
      }
    });
  }

}
