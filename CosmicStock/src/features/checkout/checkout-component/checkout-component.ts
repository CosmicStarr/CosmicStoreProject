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
import { IUserAddress } from '../../models/UserInfo';
import { IShippingOption } from '../../models/order';

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
  private fb = inject(FormBuilder);
  private router = inject(Router);

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
  error: string | null = null;
  submitting = false;

  ngOnInit(): void {
    this.cartService.loadCart();

    this.checkoutForm = this.fb.group({
      fullName: ['', Validators.required],
      streetAddress: ['', Validators.required],
      city: ['', Validators.required],
      provinceOrState: ['', Validators.required],
      countryCode: ['US', Validators.required],
    });

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

    // Re-quote whenever the destination changes, since CJ prices freight per country.
    this.checkoutForm.valueChanges.pipe(debounceTime(600)).subscribe(() => this.loadShippingOptions());
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
      countryCode: address.countryCode,
    });
  }

  selectShipping(option: IShippingOption) {
    this.selectedShipping.set(option);
  }

  loadShippingOptions() {
    const cart = this.cartService.cart();
    const countryCode = this.checkoutForm.get('countryCode')?.value;

    if (!cart?.shoppingCartItems.length || !countryCode) return;

    this.shippingLoading.set(true);

    this.shippingService
      .getQuote({
        countryCode,
        provinceOrState: this.checkoutForm.get('provinceOrState')?.value,
        city: this.checkoutForm.get('city')?.value,
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
    if (this.checkoutForm.invalid || !this.card) return;

    const cart = this.cartService.cart();
    const cartId = this.cartService.getCartId();

    if (!cart?.shoppingCartItems.length || !cartId) {
      this.error = 'Your cart is empty.';
      return;
    }

    this.submitting = true;
    this.error = null;

    const shipping = this.selectedShipping();

    try {
      // 1. Ask the server for a PaymentIntent. It prices the basket itself, so the
      //    amount charged never depends on anything this page calculated.
      this.statusMessage.set('Preparing payment...');
      const intent = await this.paymentService
        .createOrUpdateIntent(cartId, {
          logisticName: shipping?.logisticName,
          shippingCost: shipping?.freightCost ?? 0,
        })
        .toPromise();

      if (!intent) throw new Error('Could not start the payment.');

      // 2. Collect and confirm the card with Stripe directly; card data never touches our API.
      this.statusMessage.set('Confirming your card...');
      const result = await this.paymentService.confirmCardPayment(
        intent.clientSecret,
        this.card,
        this.checkoutForm.value.fullName,
      );

      if (result.error) {
        this.error = result.error.message ?? 'Your card was declined.';
        this.submitting = false;
        this.statusMessage.set(null);
        return;
      }

      // 3. Only now place the order. The server re-verifies the intent with Stripe.
      this.statusMessage.set('Placing your order...');
      const order = await this.orderService
        .checkout({
          ...this.checkoutForm.value,
          stripePaymentMethodId: result.paymentIntent!.id,
          logisticName: shipping?.logisticName,
          shippingCost: shipping?.freightCost ?? 0,
          items: cart.shoppingCartItems.map((item) => ({
            sku: item.sku,
            amount: item.amount,
            name: item.name,
            price: item.price,
          })),
        })
        .toPromise();

      this.cartService.clearCart();
      this.router.navigate(['/orders', order!.orderId]);
    } catch (err: unknown) {
      const httpError = err as { error?: { message?: string } };
      this.error = httpError.error?.message || 'Checkout failed. Please try again.';
      this.submitting = false;
      this.statusMessage.set(null);
    }
  }
}
