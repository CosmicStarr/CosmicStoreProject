import { Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AccountService } from '../../../core/services/account-service';
import { AddressService } from '../../../core/services/address-service';
import { IUserAddress } from '../../models/UserInfo';

@Component({
  selector: 'app-profile',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './profile.html',
  styleUrl: './profile.scss',
})
export class ProfileComponent implements OnInit {
  private fb = inject(FormBuilder);
  private accountService = inject(AccountService);
  private addressService = inject(AddressService);

  profileForm!: FormGroup;
  addressForm!: FormGroup;
  protected addresses = signal<IUserAddress[]>([]);
  editingAddressId: number | null = null;
  profileMessage: string | null = null;
  passwordMessage: string | null = null;
  addressMessage: string | null = null;
  isError = false;

  passwordForm!: FormGroup;

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

    this.resetAddressForm();
    this.loadAddresses();
  }

  loadAddresses() {
    this.addressService.getAddresses().subscribe({
      next: (addresses) => this.addresses.set(addresses),
    });
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
