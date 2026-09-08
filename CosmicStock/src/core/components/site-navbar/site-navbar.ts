import { AsyncPipe } from '@angular/common';
import { Component, HostListener, inject, OnInit, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive } from '@angular/router';
import { filter } from 'rxjs';
import { AccountService } from '../../services/account-service';
import { CartService } from '../../services/cart-service';
import { isAdminUser } from '../../utils/auth-utils';

@Component({
  selector: 'app-site-navbar',
  imports: [RouterLink, RouterLinkActive, AsyncPipe],
  templateUrl: './site-navbar.html',
  styleUrl: './site-navbar.scss',
})
export class SiteNavbarComponent implements OnInit {
  protected userService = inject(AccountService);
  protected cartService = inject(CartService);
  private accountService = inject(AccountService);
  private router = inject(Router);

  protected visible = signal(true);
  protected isAdmin = signal(false);
  activeDropdown: string | null = null;
  isMobileMenuOpen = false;

  ngOnInit(): void {
    this.cartService.loadCart();
    this.isAdmin.set(isAdminUser());
    this.updateVisibility(this.router.url);

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

  @HostListener('document:click', ['$event'])
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

  private updateVisibility(url: string) {
    this.visible.set(!url.startsWith('/admin'));
  }
}
