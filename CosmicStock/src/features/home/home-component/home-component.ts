import { Component, HostListener, inject, OnInit, signal } from '@angular/core';
import { sunParams } from '../../models/paramOptions';
import { StoreProductsService } from '../../../core/services/store-products';
import { IProductResponse } from '../../models/productResponse';
import { AdminProductService } from '../../../core/services/admin-product';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../env/environment';
import { IFlatProduct } from '../../models/flattenProduct';
import { AccountService } from '../../../core/services/account-service';
import { CartService } from '../../../core/services/cart-service';
import { AsyncPipe } from '@angular/common';


@Component({
  imports: [RouterLink, AsyncPipe],
  selector: 'app-home-component',
  styleUrl: './home-component.scss',
  templateUrl: './home-component.html',
})
export class HomeComponent implements OnInit {
  private httpClient = inject(HttpClient);
  protected readonly title =('CosmicStock');
  protected readonly description =('CosmicStock is a web application that allows users to browse and purchase products from the CosmicStoreAPI. The application is built using Angular and TypeScript, and it communicates with the CosmicStoreAPI to retrieve product data. Users can view product details, add products to their cart, and complete purchases through the application.');
  protected readonly baseUrl =environment.baseUrl;
  protected readonly apiData = signal<IFlatProduct[]>([]);  


  private route = inject(ActivatedRoute);
  productService = inject(StoreProductsService)
  adminService = inject(AdminProductService)
  userService = inject(AccountService);
  protected cartService = inject(CartService);
  sunParams = new sunParams()
  highlightType: string = 'NewArrival';
  
  productId!: string;
  protected readonly products = signal<IProductResponse[]>([]);
  private accountService = inject(AccountService);
  activeDropdown: string | null = null;
  isMobileMenuOpen: boolean = false;
  ngOnInit(): void {
    this.productId = this.route.snapshot.paramMap.get('id') || '';
    this.cartService.loadCart();
    this.getAllProducts(this.highlightType);
    this.getProduct();
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent) {
    const targetElement = event.target as HTMLElement;
    
    // If the clicked element is NOT inside a dropdown container, close any open dropdowns
    if (!targetElement.closest('.dropdown-container')) {
      this.activeDropdown = null;
    }

    if (!targetElement.closest('.top-bar')) {
      this.isMobileMenuOpen = false;
    }
  }

  getProduct(){
      this.adminService.getProductById(this.productId).subscribe({
      error: (err) => console.error('Failed to load product metadata', err)
    });
  }
  
  getAllProducts(highlightType: string) {
    console.log(highlightType)
    this.productService.getHighlightedProducts(highlightType).subscribe({
      next: (response: any) => {
        
        console.log('1. Raw Response:', response);
        console.log('2. Is this an array?', Array.isArray(response));

        // Defensive check: Only set the signal if it's an actual array
        if (Array.isArray(response)) {
          this.products.set(response);
        } 
        // Sometimes ASP.NET wraps arrays in a "value" or "result" property
        else if (response && Array.isArray(response.result)) {
          console.warn('Data was hiding inside response.result!');
          this.products.set(response.result);
        }
        else if (response && Array.isArray(response.value)) {
          console.warn('Data was hiding inside response.value!');
          this.products.set(response.value);
        }
        // If it's completely unrecognized, don't crash the HTML, just set it empty
        else {
          console.error('CRITICAL: API did not return an array. It returned:', response);
          this.products.set([]); 
        }
        
      },
      error: (err) => {
        console.error('Error fetching products', err);
      }
    });
  }


  toggleDropdown(menuName: string) {
    this.activeDropdown = this.activeDropdown === menuName ? null : menuName;
  }

  toggleMobileMenu() {
    this.isMobileMenuOpen = !this.isMobileMenuOpen;
  }

  closeDropdowns(event: Event) {
    event.stopPropagation();
    this.activeDropdown = null;
  }

  selectItem(item: string) {
    // Handle currency selection logic here
    this.activeDropdown = null;
  }

  selectLang(lang: string) {
    // Handle language selection logic here
    this.activeDropdown = null;
  }

  logout() {
    // Handle logout logic
    this.accountService.logout();
    this.activeDropdown = null;
  }

}
