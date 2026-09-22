import { AsyncPipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NavigationEnd, Router, RouterLink, RouterLinkActive } from '@angular/router';
import { filter } from 'rxjs';
import { BrandLogoComponent } from '../brand-logo/brand-logo';
import { OverflowRowComponent } from '../overflow-row/overflow-row';
import { AccountService } from '../../services/account-service';
import { CartService } from '../../services/cart-service';
import { StoreProductsService } from '../../services/store-products';
import { ICategorySummary } from '../../../features/models/productResponse';
import { isAdminUser } from '../../utils/auth-utils';

@Component({
  selector: 'app-site-navbar',
  imports: [RouterLink, RouterLinkActive, AsyncPipe, FormsModule, OverflowRowComponent, BrandLogoComponent],
  templateUrl: './site-navbar.html',
  styleUrl: './site-navbar.scss',
  host: {
    '(document:click)': 'onDocumentClick($event)',
  },
})
export class SiteNavbarComponent implements OnInit {
  protected userService = inject(AccountService);
  protected cartService = inject(CartService);
  private accountService = inject(AccountService);
  private storeProducts = inject(StoreProductsService);
  private router = inject(Router);

  protected visible = signal(true);
  protected isAdmin = signal(false);
  protected readonly categories = signal<ICategorySummary[]>([]);
  activeDropdown: string | null = null;
  isMobileMenuOpen = false;

  searchTerm = '';

  ngOnInit(): void {
    this.cartService.loadCart();
    this.isAdmin.set(isAdminUser());
    this.updateVisibility(this.router.url);
    this.storeProducts.getCategories().subscribe({
      next: (categories) => this.categories.set(categories),
    });

    this.accountService.currentUser$.subscribe(() => {
      this.isAdmin.set(isAdminUser());
    });

    this.router.events
      .pipe(filter((event) => event instanceof NavigationEnd))
      .subscribe((event) => {
        const url = (event as NavigationEnd).urlAfterRedirects;
        this.updateVisibility(url);
      });
  }

  onDocumentClick(event: MouseEvent) {
    const targetElement = event.target as HTMLElement;

    if (!targetElement.closest('.dropdown-container')) {
      this.activeDropdown = null;
    }

    if (!targetElement.closest('.top-bar')) {
      this.isMobileMenuOpen = false;
    }
  }

  toggleDropdown(menuName: string) {
    this.activeDropdown = this.activeDropdown === menuName ? null : menuName;
  }

  closeDropdowns(event: Event) {
    event.stopPropagation();
    this.activeDropdown = null;
  }

  logout() {
    this.accountService.logout();
    this.isAdmin.set(false);
    this.activeDropdown = null;
  }

  submitSearch(event: Event) {
    event.preventDefault();
    const term = this.searchTerm.trim();
    this.router.navigate(['/store'], {
      queryParams: term ? { search: term, page: 1 } : { search: null, page: null },
      queryParamsHandling: 'merge',
    });
  }

  private updateVisibility(url: string) {
    this.visible.set(!url.startsWith('/admin'));
  }
}
