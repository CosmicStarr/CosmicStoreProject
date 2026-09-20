import { afterNextRender, Component, ElementRef, inject, Injector, OnInit, signal, viewChild } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AccountService } from '../../../core/services/account-service';
import { AddressService } from '../../../core/services/address-service';
import { OrderService } from '../../../core/services/order-service';
import { IUserAddress } from '../../models/UserInfo';
import { IOrder } from '../../models/order';

@Component({
  selector: 'app-profile',
  imports: [ReactiveFormsModule, RouterLink, CurrencyPipe, DatePipe],
  templateUrl: './profile.html',
  styleUrl: './profile.scss',
})
export class ProfileComponent implements OnInit {
  private fb = inject(FormBuilder);
  private accountService = inject(AccountService);
  private addressService = inject(AddressService);
  private orderService = inject(OrderService);
  private router = inject(Router);
  private injector = inject(Injector);
  private readonly reauthPassword = viewChild<ElementRef<HTMLInputElement>>('reauthPassword');
  private readonly newEmailInput = viewChild<ElementRef<HTMLInputElement>>('newEmailInput');

  profileForm!: FormGroup;
  addressForm!: FormGroup;
  protected addresses = signal<IUserAddress[]>([]);
  protected orders = signal<IOrder[]>([]);
  protected ordersLoading = signal(true);
  protected ordersMessage = signal<string | null>(null);
  editingAddressId: number | null = null;
  profileMessage: string | null = null;
  passwordMessage: string | null = null;
  addressMessage: string | null = null;
  isError = false;

  passwordForm!: FormGroup;
  reauthForm!: FormGroup;
  emailChangeForm!: FormGroup;
  protected readonly showReauth = signal(false);
  protected readonly emailFormVisible = signal(false);
  protected readonly reauthSubmitting = signal(false);
  protected readonly emailChangeSubmitting = signal(false);
  protected readonly pendingEmail = signal<string | null>(null);
  protected readonly emailChangeMessage = signal<string | null>(null);
  protected readonly emailChangeError = signal(false);
  private reauthToken: string | null = null;

  ngOnInit(): void {
    const user = this.accountService.currentUserValue;

    this.profileForm = this.fb.group({
      userName: [user?.userName ?? '', Validators.required],
      email: [{ value: user?.email ?? '', disabled: true }],
    });

    this.passwordForm = this.fb.group({
      currentPassword: ['', Validators.required],
      newPassword: ['', [Validators.required, Validators.minLength(8)]],
      confirmPassword: ['', Validators.required],
    });

    this.reauthForm = this.fb.group({
      currentPassword: ['', Validators.required],
    });

    this.emailChangeForm = this.fb.group({
      newEmail: ['', [Validators.required, Validators.email]],
    });

    this.pendingEmail.set(user?.pendingEmail ?? null);
    this.resetAddressForm();
    this.loadAddresses();
    this.loadOrders();
    this.accountService.loadCurrentUser().subscribe({
      next: (current) => {
        this.pendingEmail.set(current.pendingEmail ?? null);
        this.profileForm.patchValue({
          userName: current.userName ?? '',
          email: current.email ?? '',
        });
      },
      error: () => undefined,
    });
  }

  loadAddresses() {
    this.addressService.getAddresses().subscribe({
      next: (addresses) => this.addresses.set(addresses),
    });
  }

  loadOrders() {
    this.ordersLoading.set(true);
    this.orderService.getOrders().subscribe({
      next: (orders) => {
        this.orders.set(Array.isArray(orders) ? orders : []);
        this.ordersLoading.set(false);
      },
      error: (err) => {
        this.ordersLoading.set(false);
        this.ordersMessage.set(err.error?.message || 'Orders could not be loaded.');
      },
    });
  }

  openOrder(order: IOrder) {
    void this.router.navigate(['/orders', order.orderId]);
  }

  itemCount(order: IOrder) {
    return order.items?.reduce((total, item) => total + item.quantity, 0) ?? 0;
  }

  saveProfile() {
    if (this.profileForm.invalid) return;

    this.accountService.updateProfile(this.profileForm.value.userName).subscribe({
      next: () => {
        this.isError = false;
        this.profileMessage = 'Profile updated.';
      },
      error: (err) => {
        this.isError = true;
        this.profileMessage = err.error?.message || 'Could not update profile.';
      },
    });
  }

  changePassword() {
    if (this.passwordForm.invalid) return;

    const values = this.passwordForm.value;
    if (values.newPassword !== values.confirmPassword) {
      this.isError = true;
      this.passwordMessage = 'New passwords do not match.';
      return;
    }

    this.accountService.changePassword(values).subscribe({
      next: (response) => {
        this.isError = false;
        this.passwordMessage = response.message;
        this.passwordForm.reset();
      },
      error: (err) => {
        this.isError = true;
        this.passwordMessage = err.error?.message || 'Could not change password.';
      },
    });
  }

  beginEmailChange() {
    this.showReauth.set(true);
    this.emailFormVisible.set(false);
    this.reauthToken = null;
    this.emailChangeMessage.set(null);
    this.emailChangeError.set(false);
    this.reauthForm.reset();
    this.focusField(this.reauthPassword);
  }

  cancelEmailChange() {
    this.showReauth.set(false);
    this.emailFormVisible.set(false);
    this.reauthToken = null;
    this.reauthForm.reset();
    this.emailChangeForm.reset();
    this.emailChangeMessage.set(null);
    this.emailChangeError.set(false);
  }

  confirmPasswordForEmail() {
    if (this.reauthForm.invalid || this.reauthSubmitting()) return;

    this.reauthSubmitting.set(true);
    this.emailChangeMessage.set(null);
    this.emailChangeError.set(false);

    this.accountService.reauthenticate(this.reauthForm.value.currentPassword).subscribe({
      next: (response) => {
        this.reauthSubmitting.set(false);
        this.reauthToken = response.reauthToken;
        this.showReauth.set(false);
        this.emailFormVisible.set(true);
        this.reauthForm.reset();
        this.focusField(this.newEmailInput);
      },
      error: (err: { error?: { message?: string } }) => {
        this.reauthSubmitting.set(false);
        this.emailChangeError.set(true);
        this.emailChangeMessage.set(err.error?.message || 'Current password is incorrect.');
      },
    });
  }

  requestEmailChange() {
    if (this.emailChangeForm.invalid || this.emailChangeSubmitting() || !this.reauthToken) {
      if (!this.reauthToken) {
        this.beginEmailChange();
      }
      return;
    }

    this.emailChangeSubmitting.set(true);
    this.emailChangeMessage.set(null);
    this.emailChangeError.set(false);

    this.accountService.requestEmailChange(this.reauthToken, this.emailChangeForm.value.newEmail).subscribe({
      next: (response) => {
        this.emailChangeSubmitting.set(false);
        this.reauthToken = null;
        this.emailFormVisible.set(false);
        this.showReauth.set(false);
        this.emailChangeForm.reset();
        this.pendingEmail.set(response.pendingEmail);
        this.emailChangeError.set(false);
        this.emailChangeMessage.set(response.message);
      },
      error: (err: { error?: { message?: string } }) => {
        this.emailChangeSubmitting.set(false);
        this.emailChangeError.set(true);
        this.emailChangeMessage.set(err.error?.message || 'Could not start the email change.');
        if (err.error?.message?.includes('password')) {
          this.reauthToken = null;
          this.emailFormVisible.set(false);
          this.showReauth.set(true);
          this.reauthForm.reset();
        }
      },
    });
  }

  private focusField(field: () => ElementRef<HTMLInputElement> | undefined) {
    afterNextRender(() => field()?.nativeElement.focus(), { injector: this.injector });
  }

  editAddress(address: IUserAddress) {
    this.editingAddressId = address.id;
    this.addressForm.patchValue(address);
  }

  resetAddressForm() {
    this.editingAddressId = null;
    this.addressForm = this.fb.group({
      label: ['', Validators.required],
      fullName: ['', Validators.required],
      streetAddress: ['', Validators.required],
      city: ['', Validators.required],
      provinceOrState: ['', Validators.required],
      zipCode: ['', [Validators.required, Validators.maxLength(20)]],
      countryCode: ['US', Validators.required],
      isDefault: [false],
    });
  }

  saveAddress() {
    if (this.addressForm.invalid) return;

    const payload = this.addressForm.getRawValue();
    const request = this.editingAddressId
      ? this.addressService.updateAddress(this.editingAddressId, payload)
      : this.addressService.createAddress(payload);

    request.subscribe({
      next: () => {
        this.isError = false;
        this.addressMessage = this.editingAddressId ? 'Address updated.' : 'Address saved.';
        this.resetAddressForm();
        this.loadAddresses();
      },
      error: (err) => {
        this.isError = true;
        this.addressMessage = err.error?.message || 'Could not save address.';
      },
    });
  }

  deleteAddress(id: number) {
    this.addressService.deleteAddress(id).subscribe({
      next: () => this.loadAddresses(),
    });
  }
}
