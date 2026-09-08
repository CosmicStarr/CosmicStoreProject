import { Component, inject, OnInit, signal } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { CartService } from '../../../core/services/cart-service';
import { OrderService } from '../../../core/services/order-service';
import { AddressService } from '../../../core/services/address-service';
import { IUserAddress } from '../../models/UserInfo';

@Component({
  selector: 'app-checkout-component',
  imports: [ReactiveFormsModule, CurrencyPipe, RouterLink],
  templateUrl: './checkout-component.html',
  styleUrl: './checkout-component.scss',
})
export class CheckoutComponent implements OnInit {
  protected cartService = inject(CartService);
  private orderService = inject(OrderService);
  private addressService = inject(AddressService);
  private fb = inject(FormBuilder);
  private router = inject(Router);

  checkoutForm!: FormGroup;
  protected savedAddresses = signal<IUserAddress[]>([]);
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
      },
    });
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

  onSubmit() {
    if (this.checkoutForm.invalid) return;

    const cart = this.cartService.cart();
    if (!cart?.shoppingCartItems.length) {
      this.error = 'Your cart is empty.';
      return;
    }

    this.submitting = true;
    this.error = null;

    this.orderService.checkout({
      ...this.checkoutForm.value,
      stripePaymentMethodId: 'dev_payment_placeholder',
      items: cart.shoppingCartItems.map((item) => ({
        sku: item.sku,
        amount: item.amount,
        name: item.name,
        price: item.price,
      })),
    }).subscribe({
      next: (order) => {
        this.cartService.clearCart();
        this.router.navigate(['/orders', order.orderId]);
      },
      error: (err) => {
        this.error = err.error?.message || 'Checkout failed. Please try again.';
        this.submitting = false;
      },
    });
  }
}
