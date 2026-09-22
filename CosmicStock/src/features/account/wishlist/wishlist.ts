import { Component, inject, OnInit, signal } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { WishlistService } from '../../../core/services/wishlist-service';
import { CartService } from '../../../core/services/cart-service';
import { AddressService } from '../../../core/services/address-service';
import { IUserAddress, IWishlist, IWishlistItem } from '../../models/UserInfo';
import { SiteNavbarComponent } from '../../../core/components/site-navbar/site-navbar';
import { SiteFooterComponent } from '../../../core/components/site-footer/site-footer';

@Component({
  selector: 'app-wishlist',
  imports: [CurrencyPipe, RouterLink, ReactiveFormsModule, SiteNavbarComponent, SiteFooterComponent],
  templateUrl: './wishlist.html',
  styleUrl: './wishlist.scss',
})
export class WishlistComponent implements OnInit {
  private wishlistService = inject(WishlistService);
  private cartService = inject(CartService);
  private addressService = inject(AddressService);
  private fb = inject(FormBuilder);

  protected wishlist = signal<IWishlist | null>(null);
  protected addresses = signal<IUserAddress[]>([]);
  protected readonly loading = signal(true);
  protected readonly message = signal<string | null>(null);
  protected readonly isError = signal(false);
  protected readonly addingProductId = signal<string | null>(null);
  protected readonly savingSettings = signal(false);
  protected readonly copied = signal(false);

  protected readonly settingsForm = this.fb.nonNullable.group({
    name: ['My Wishlist', [Validators.required, Validators.maxLength(80)]],
    shippingAddressId: [null as number | null],
    isGiftRegistry: [false],
  });

  ngOnInit(): void {
    this.loadWishlist();
    this.addressService.getAddresses().subscribe({
      next: (addresses) => this.addresses.set(addresses),
      error: () => this.addresses.set([]),
    });
  }

  protected items(): IWishlistItem[] {
    return this.wishlist()?.items ?? [];
  }

  loadWishlist() {
    this.loading.set(true);
    this.wishlistService.getWishlist().subscribe({
      next: (list) => {
        this.wishlist.set(list);
        this.settingsForm.patchValue({
          name: list.name,
          shippingAddressId: list.shippingAddressId ?? null,
          isGiftRegistry: list.isGiftRegistry,
        });
        this.loading.set(false);
      },
      error: (err: { error?: { message?: string } }) => {
        this.loading.set(false);
        this.isError.set(true);
        this.message.set(err.error?.message || 'Wishlist could not be loaded.');
      },
    });
  }

  saveSettings() {
    if (this.settingsForm.invalid || this.savingSettings()) return;

    this.savingSettings.set(true);
    this.message.set(null);
    this.wishlistService.updateWishlist(this.settingsForm.getRawValue()).subscribe({
      next: (list) => {
        this.wishlist.set(list);
        this.savingSettings.set(false);
        this.isError.set(false);
        this.message.set(list.isGiftRegistry
          ? 'Gift registry saved. Share the private link — buyers will not see your street address.'
          : 'Wishlist settings saved.');
      },
      error: (err: { error?: { message?: string } }) => {
        this.savingSettings.set(false);
        this.isError.set(true);
        this.message.set(err.error?.message || 'Could not save registry settings.');
      },
    });
  }

  async copyShareLink() {
    const url = this.wishlist()?.shareUrl;
    if (!url) return;

    try {
      await navigator.clipboard.writeText(url);
      this.copied.set(true);
      this.isError.set(false);
      this.message.set('Registry link copied.');
    } catch {
      this.isError.set(true);
      this.message.set('Could not copy the link. Select it and copy manually.');
    }
  }

  addToCart(item: IWishlistItem) {
    if (this.addingProductId()) return;

    this.addingProductId.set(item.productId);
    this.message.set(null);
    this.isError.set(false);

    this.cartService.addItem(item.productId, 1, item.sku).subscribe({
      next: () => {
        this.addingProductId.set(null);
        this.isError.set(false);
        this.message.set(`${item.nameEn} added to cart.`);
      },
      error: (err: { error?: { message?: string } }) => {
        this.addingProductId.set(null);
        this.isError.set(true);
        this.message.set(err.error?.message || 'Could not add to cart.');
      },
    });
  }

  removeItem(productId: string) {
    this.wishlistService.removeItem(productId).subscribe({
      next: () => {
        this.wishlist.update((list) =>
          list ? { ...list, items: list.items.filter((item) => item.productId !== productId) } : list,
        );
        this.isError.set(false);
        this.message.set('Removed from wishlist.');
      },
      error: () => {
        this.isError.set(true);
        this.message.set('Could not remove this item.');
      },
    });
  }
}
