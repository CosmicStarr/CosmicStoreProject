import {
  AfterViewInit,
  Component,
  computed,
  ElementRef,
  inject,
  OnDestroy,
  OnInit,
  signal,
  viewChild,
} from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { debounceTime } from 'rxjs';
import { StripeCardElement } from '@stripe/stripe-js';
import { CartService } from '../../../core/services/cart-service';
import { OrderService } from '../../../core/services/order-service';
import { AddressService } from '../../../core/services/address-service';
import { ShippingService } from '../../../core/services/shipping-service';
import { PaymentService } from '../../../core/services/payment-service';
import { AccountService } from '../../../core/services/account-service';
import { LEGAL_TERMS_VERSION } from '../../../core/legal/legal-terms';
import {
  SHIPPING_PROCESSING_BUSINESS_DAYS,
  SHIPPING_TRANSIT_BUSINESS_DAYS_MAX,
  SHIPPING_TRANSIT_BUSINESS_DAYS_MIN,
} from '../../../core/legal/shipping-policy';
import { IUserAddress } from '../../models/UserInfo';
import { ICheckoutRequest, IShippingOption } from '../../models/order';

@Component({
  selector: 'app-checkout-component',
  imports: [ReactiveFormsModule, CurrencyPipe, RouterLink],
  templateUrl: './checkout-component.html',
  styleUrl: './checkout-component.scss',
})
export class CheckoutComponent implements OnInit, AfterViewInit, OnDestroy {
  protected cartService = inject(CartService);
  private orderService = inject(OrderService);
  private addressService = inject(AddressService);
  private shippingService = inject(ShippingService);
  private paymentService = inject(PaymentService);
  private accountService = inject(AccountService);
  private fb = inject(FormBuilder);
  private router = inject(Router);

  protected readonly processingDays = SHIPPING_PROCESSING_BUSINESS_DAYS;
  protected readonly transitMin = SHIPPING_TRANSIT_BUSINESS_DAYS_MIN;
  protected readonly transitMax = SHIPPING_TRANSIT_BUSINESS_DAYS_MAX;

  private cardHost = viewChild.required<ElementRef<HTMLDivElement>>('cardElement');
  private card?: StripeCardElement;

  checkoutForm!: FormGroup;
  protected savedAddresses = signal<IUserAddress[]>([]);
  protected shippingOptions = signal<IShippingOption[]>([]);
  protected selectedShipping = signal<IShippingOption | null>(null);
  protected shippingLoading = signal(false);
  protected cardReady = signal(false);
  protected cardError = signal<string | null>(null);
  protected statusMessage = signal<string | null>(null);
  protected orderTotal = computed(
    () => this.cartService.subtotal() + (this.selectedShipping()?.freightCost ?? 0),
  );
  protected registryWishlistId = computed(() => {
    const cart = this.cartService.cart();
    if (cart?.wishlistId && cart.wishlistId > 0) {
      return cart.wishlistId;
    }

    const items = cart?.shoppingCartItems ?? [];
    if (!items.length) {
      return null;
    }

    const ids = [
      ...new Set(
        items
          .map((item) => item.wishlistId)
          .filter((id): id is number => typeof id === 'number' && id > 0),
      ),
    ];
    if (ids.length === 1 && items.every((item) => item.wishlistId === ids[0])) {
      return ids[0];
    }

    return null;
  });
  protected maskedShippingLabel = computed(
    () => this.cartService.cart()?.maskedShippingLabel ?? "Ship to the recipient's Registry Address",
  );
  error: string | null = null;
  submitting = false;

  ngOnInit(): void {
    this.cartService.loadCart();
    this.accountService.ensureGuestCheckoutIfNeeded();

    this.checkoutForm = this.fb.group({
      email: [this.accountService.currentUserValue?.email ?? '', [Validators.required, Validators.email]],
      fullName: ['', Validators.required],
      streetAddress: ['', Validators.required],
      city: ['', Validators.required],
      provinceOrState: ['', Validators.required],
      zipCode: ['', [Validators.required, Validators.maxLength(20)]],
      countryCode: ['US', Validators.required],
      billingPostalCode: [''],
      acceptedTerms: [false, Validators.requiredTrue],
      acceptedTermsAt: [''],
    });

    const useSavedAddresses = !!this.accountService.currentUserValue && !this.accountService.isGuestCheckout();
    if (this.registryWishlistId() || !useSavedAddresses) {
      this.loadShippingOptions();
    } else {
      this.addressService.getAddresses().subscribe({
        next: (addresses) => {
          this.savedAddresses.set(addresses);
          const defaultAddress = addresses.find((address) => address.isDefault) ?? addresses[0];
          if (defaultAddress) {
            this.applyAddress(defaultAddress);
          }
          this.loadShippingOptions();
        },
        error: () => this.loadShippingOptions(),
      });
    }

    // Re-quote whenever the destination changes, since CJ prices freight per country.
    this.checkoutForm.valueChanges.pipe(debounceTime(600)).subscribe(() => this.loadShippingOptions());
    this.checkoutForm.get('acceptedTerms')?.valueChanges.subscribe((checked) => {
      this.checkoutForm.patchValue(
        { acceptedTermsAt: checked ? new Date().toISOString() : '' },
        { emitEvent: false },
      );
    });
    setTimeout(() => this.loadShippingOptions(), 400);
  }

  async ngAfterViewInit(): Promise<void> {
    try {
      this.card = await this.paymentService.createCardElement();
      this.card.mount(this.cardHost().nativeElement);
      this.card.on('change', (event) => {
        this.cardError.set(event.error?.message ?? null);
        this.cardReady.set(event.complete);
      });
    } catch {
      this.cardError.set('Card payment is unavailable right now.');
    }
  }

  ngOnDestroy(): void {
    this.card?.destroy();
    this.paymentService.reset();
  }

  applyAddress(address: IUserAddress) {
    this.checkoutForm.patchValue({
      fullName: address.fullName,
      streetAddress: address.streetAddress,
      city: address.city,
      provinceOrState: address.provinceOrState,
      zipCode: address.zipCode,
      countryCode: address.countryCode,
    });
  }

  selectShipping(option: IShippingOption) {
    this.selectedShipping.set(option);
  }

  loadShippingOptions() {
    const cart = this.cartService.cart();
    const wishlistId = this.registryWishlistId();
    const countryCode = this.checkoutForm.get('countryCode')?.value;
    this.applyRegistryMode();

    if (!cart?.shoppingCartItems.length) return;
    if (!wishlistId && !countryCode) return;

    this.shippingLoading.set(true);

    this.shippingService
      .getQuote({
        countryCode: countryCode || 'US',
        provinceOrState: this.checkoutForm.get('provinceOrState')?.value,
        city: this.checkoutForm.get('city')?.value,
        wishlistId: wishlistId ?? undefined,
        items: cart.shoppingCartItems.map((item) => ({ sku: item.sku, amount: item.amount })),
      })
      .subscribe({
        next: (options) => {
          this.shippingOptions.set(options);
          const current = this.selectedShipping();
          const stillAvailable = options.find((o) => o.logisticName === current?.logisticName);
          this.selectedShipping.set(stillAvailable ?? options[0] ?? null);
          this.shippingLoading.set(false);
        },
        error: () => {
          this.shippingOptions.set([]);
          this.shippingLoading.set(false);
        },
      });
  }

  async onSubmit() {
    this.accountService.ensureGuestCheckoutIfNeeded();

    if (this.checkoutForm.invalid || !this.card) return;

    const termsAt = this.acceptedTermsTimestamp();
    if (!termsAt) {
      this.error = 'You must agree to the Terms & Conditions and Privacy Policy.';
      return;
    }

    const cart = this.cartService.cart();
    const cartId = this.cartService.getCartId();

    if (!cart?.shoppingCartItems.length || !cartId) {
      this.error = 'Your cart is empty.';
      return;
    }

    this.submitting = true;
    this.error = null;

    const shipping = this.selectedShipping();
    const wishlistId = this.registryWishlistId();

    try {
      // 1. Ask the server for a PaymentIntent. It prices the basket itself, so the
      //    amount charged never depends on anything this page calculated.
      this.statusMessage.set('Preparing payment...');
      const intent = await this.paymentService
        .createOrUpdateIntent(cartId, {
          logisticName: shipping?.logisticName,
          shippingCost: shipping?.freightCost ?? 0,
          wishlistId: wishlistId ?? undefined,
          acceptedTermsVersion: LEGAL_TERMS_VERSION,
          acceptedTermsAt: termsAt,
        })
        .toPromise();

      if (!intent) throw new Error('Could not start the payment.');

      // 2. Collect and confirm the card with Stripe directly; card data never touches our API.
      this.statusMessage.set('Confirming your card...');
      const billing = wishlistId
        ? {
            name: this.checkoutForm.value.email,
            email: this.checkoutForm.value.email,
            postalCode: this.checkoutForm.value.billingPostalCode,
            country: 'US',
          }
        : {
            name: this.checkoutForm.value.fullName,
            email: this.checkoutForm.value.email,
            line1: this.checkoutForm.value.streetAddress,
            city: this.checkoutForm.value.city,
            state: this.checkoutForm.value.provinceOrState,
            postalCode: this.checkoutForm.value.zipCode,
            country: this.checkoutForm.value.countryCode,
          };
      const result = await this.paymentService.confirmCardPayment(
        intent.clientSecret,
        this.card,
        billing,
      );

      if (result.error) {
        this.error = result.error.message ?? 'Your card was declined.';
        this.submitting = false;
        this.statusMessage.set(null);
        return;
      }

      // 3. Only now place the order. The server re-verifies the intent with Stripe.
      this.statusMessage.set('Placing your order...');
      const lineItems = cart.shoppingCartItems.map((item) => ({
        sku: item.sku,
        amount: item.amount,
        name: item.name,
        price: item.price,
        wishlistId: item.wishlistId,
      }));
      const order = await this.orderService
        .checkout(this.buildCheckoutRequest(result.paymentIntent!.id, shipping, lineItems, wishlistId, termsAt))
        .toPromise();

      this.cartService.clearCart();
      if (this.accountService.isGuestCheckout()) {
        this.accountService.endGuestCheckout();
      }
      this.router.navigate(['/checkout/confirmation'], { state: { order } });
    } catch (err: unknown) {
      const httpError = err as { error?: { message?: string } };
      this.error = httpError.error?.message || 'Checkout failed. Please try again.';
      this.submitting = false;
      this.statusMessage.set(null);
    }
  }

  private applyRegistryMode() {
    if (!this.checkoutForm) {
      return;
    }

    const locked = this.registryWishlistId() != null;
    const shippingFields = ['fullName', 'streetAddress', 'city', 'provinceOrState', 'zipCode', 'countryCode'];
    for (const name of shippingFields) {
      const control = this.checkoutForm.get(name);
      if (!control) continue;
      if (locked) {
        control.clearValidators();
      } else if (name === 'zipCode') {
        control.setValidators([Validators.required, Validators.maxLength(20)]);
      } else {
        control.setValidators(Validators.required);
      }
      control.updateValueAndValidity({ emitEvent: false });
    }

    const billing = this.checkoutForm.get('billingPostalCode');
    if (locked) {
      billing?.setValidators([Validators.required, Validators.maxLength(20)]);
    } else {
      billing?.clearValidators();
    }
    billing?.updateValueAndValidity({ emitEvent: false });
  }

  private acceptedTermsTimestamp(): string | null {
    if (!this.checkoutForm.get('acceptedTerms')?.value) {
      return null;
    }

    const stamped = this.checkoutForm.get('acceptedTermsAt')?.value as string | undefined;
    return stamped || new Date().toISOString();
  }

  private buildCheckoutRequest(
    paymentIntentId: string,
    shipping: IShippingOption | null,
    items: ICheckoutRequest['items'],
    wishlistId: number | null,
    acceptedTermsAt: string,
  ): ICheckoutRequest {
    const agreement = {
      stripePaymentMethodId: paymentIntentId,
      logisticName: shipping?.logisticName,
      shippingCost: shipping?.freightCost ?? 0,
      items,
      acceptedTermsVersion: LEGAL_TERMS_VERSION,
      acceptedTermsAt,
    };

    if (wishlistId) {
      return {
        email: this.checkoutForm.value.email,
        wishlistId,
        ...agreement,
      };
    }

    return {
      email: this.checkoutForm.value.email,
      fullName: this.checkoutForm.value.fullName,
      streetAddress: this.checkoutForm.value.streetAddress,
      city: this.checkoutForm.value.city,
      provinceOrState: this.checkoutForm.value.provinceOrState,
      zipCode: this.checkoutForm.value.zipCode,
      countryCode: this.checkoutForm.value.countryCode,
      ...agreement,
    };
  }
}
