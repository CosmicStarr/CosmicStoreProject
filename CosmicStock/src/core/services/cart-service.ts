import { computed, inject, Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../env/environment';
import { IAddCartItem, IShoppingCart } from '../../features/models/cart';
import { tap } from 'rxjs';

const CART_ID_KEY = 'cosmicCartId';

@Injectable({ providedIn: 'root' })
export class CartService {
  private http = inject(HttpClient);
  private apiUrl = environment.baseUrl;

  private cartState = signal<IShoppingCart | null>(null);
  private panelOpenState = signal(false);
  private cartWriteVersion = 0;
  readonly cart = this.cartState.asReadonly();
  readonly panelOpen = this.panelOpenState.asReadonly();

  readonly itemCount = computed(() =>
    this.cart()?.shoppingCartItems?.reduce((sum, i) => sum + i.amount, 0) ?? 0
  );

  readonly subtotal = computed(() =>
    this.cart()?.shoppingCartItems?.reduce((sum, i) => sum + i.price * i.amount, 0) ?? 0
  );

  getCartId(): string | null {
    return localStorage.getItem(CART_ID_KEY);
  }

  loadCart() {
    const cartId = this.getCartId();
    const options = cartId ? { params: { cartId } } : {};
    const requestVersion = this.cartWriteVersion;

    this.http.get<IShoppingCart>(`${this.apiUrl}Cart`, options).subscribe({
      next: (cart) => {
        if (requestVersion !== this.cartWriteVersion) {
          return;
        }

        this.applyCart(cart);
      },
    });
  }

  addItem(productId: string, quantity = 1, sku?: string, wishlistId?: number) {
    const payload: IAddCartItem = {
      productId,
      quantity,
      sku,
      cartId: this.getCartId() ?? undefined,
      wishlistId,
    };

    return this.http.post<IShoppingCart>(`${this.apiUrl}Cart/items`, payload).pipe(
      tap((cart) => this.setCart(cart))
    );
  }

  removeItem(sku: string) {
    const cartId = this.getCartId();
    if (!cartId) return;

    this.http.delete<IShoppingCart>(`${this.apiUrl}Cart/items/${encodeURIComponent(sku)}`, { params: { cartId } }).subscribe({
      next: (cart) => this.setCart(cart),
    });
  }

  clearCart() {
    const cartId = this.getCartId();
    if (!cartId) return;

    this.http.delete(`${this.apiUrl}Cart`, { params: { cartId } }).subscribe({
      next: () => this.clearLocalCart(),
    });
  }

  mergeGuestCart(guestCartId: string) {
    return this.http.post<IShoppingCart>(`${this.apiUrl}Cart/merge`, { guestCartId }).pipe(
      tap((cart) => this.setCart(cart))
    );
  }

  openPanel() {
    this.loadCart();
    this.panelOpenState.set(true);
  }

  closePanel() {
    this.panelOpenState.set(false);
  }

  togglePanel() {
    if (this.panelOpenState()) {
      this.closePanel();
    } else {
      this.openPanel();
    }
  }

  private applyCart(cart: IShoppingCart | null) {
    if (!cart?.id) {
      return;
    }

    const current = this.cartState();
    const incomingCount = cart.shoppingCartItems?.length ?? 0;
    const currentCount = current?.shoppingCartItems?.length ?? 0;
    if (currentCount > 0 && incomingCount === 0) {
      return;
    }

    this.replaceCart(cart);
  }

  private setCart(cart: IShoppingCart) {
    this.cartWriteVersion++;
    this.replaceCart(cart);
  }

  private replaceCart(cart: IShoppingCart) {
    localStorage.setItem(CART_ID_KEY, cart.id);
    this.cartState.set(normalizeCart(cart));
  }

  clearLocalCart() {
    this.cartWriteVersion++;
    localStorage.removeItem(CART_ID_KEY);
    this.cartState.set(null);
    this.panelOpenState.set(false);
  }
}

function normalizeCart(cart: IShoppingCart): IShoppingCart {
  return {
    ...cart,
    shoppingCartItems: (cart.shoppingCartItems ?? []).map((item) => ({
      ...item,
      price: item.price,
      amount: item.amount,
    })),
  };
}
