import { afterNextRender, Component, ElementRef, inject, Injector, OnInit, signal, viewChild } from '@angular/core';
import { AbstractControl, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AccountService } from '../../../core/services/account-service';
import { BrandLogoComponent } from '../../../core/components/brand-logo/brand-logo';
import { LEGAL_TERMS_VERSION } from '../../../core/legal/legal-terms';

@Component({
  imports: [ReactiveFormsModule, RouterLink, BrandLogoComponent],
  selector: 'app-register-component',
  styleUrl: './register-component.scss',
  templateUrl: './register-component.html',
})
export class RegisterComponent implements OnInit {
  private accountService = inject(AccountService);
  private fb = inject(FormBuilder);
  private injector = inject(Injector);
  private route = inject(ActivatedRoute);
  private readonly confirmHeading = viewChild<ElementRef<HTMLHeadingElement>>('confirmHeading');

  registerForm!: FormGroup;
  errors: string[] = [];
  protected readonly registeredEmail = signal<string | null>(null);
  protected readonly confirmationEmailSent = signal(true);
  protected readonly resending = signal(false);
  protected readonly resendMessage = signal<string | null>(null);
  protected readonly submitting = signal(false);
  protected readonly fromMissingAccount = signal(false);

  ngOnInit(): void {
    this.registerForm = this.fb.group({
      userName: ['', Validators.required],
      email: ['', [Validators.required, Validators.email]],
      password: ['', [
        Validators.required,
        Validators.minLength(8),
        Validators.maxLength(20),
        Validators.pattern(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,20}$/),
      ]],
      confirmPassword: ['', [Validators.required]],
      acceptedTerms: [false, Validators.requiredTrue],
      acceptedTermsAt: [''],
    }, { validators: this.passwordMatchValidator });

    const email = this.route.snapshot.queryParamMap.get('email')?.trim();
    if (email) {
      this.registerForm.patchValue({ email });
    }
    this.fromMissingAccount.set(this.route.snapshot.queryParamMap.get('reason') === 'no-account');

    this.registerForm.get('acceptedTerms')?.valueChanges.subscribe((checked) => {
      this.registerForm.patchValue(
        { acceptedTermsAt: checked ? new Date().toISOString() : '' },
        { emitEvent: false },
      );
    });
  }

  passwordMatchValidator(control: AbstractControl) {
    const password = control.get('password');
    const confirmPassword = control.get('confirmPassword');

    if (!password || !confirmPassword) {
      return null;
    }

    if (!password.value || !confirmPassword.value) {
      return null;
    }

    if (password.value !== confirmPassword.value) {
      confirmPassword.setErrors({ ...(confirmPassword.errors ?? {}), mismatch: true });
      return { mismatch: true };
    }

    if (confirmPassword.hasError('mismatch')) {
      const rest = { ...(confirmPassword.errors ?? {}) };
      delete rest['mismatch'];
      confirmPassword.setErrors(Object.keys(rest).length ? rest : null);
    }

    return null;
  }

  /** Shown after the user leaves the password field with a value that fails length/pattern rules. */
  showPasswordPatternError(): boolean {
    const control = this.registerForm?.get('password');
    if (!control || !(control.touched || control.dirty)) {
      return false;
    }

    return control.hasError('pattern')
      || control.hasError('minlength')
      || control.hasError('maxlength');
  }

  showPasswordMismatchError(): boolean {
    const confirm = this.registerForm?.get('confirmPassword');
    if (!confirm || !(confirm.touched || confirm.dirty)) {
      return false;
    }

    return !!this.registerForm?.hasError('mismatch') || confirm.hasError('mismatch');
  }

  onSubmit() {
    if (this.registerForm.invalid || this.submitting()) {
      this.registerForm.markAllAsTouched();
      return;
    }
    this.errors = [];
    this.submitting.set(true);

    const { userName, email, password, confirmPassword, acceptedTermsAt } = this.registerForm.getRawValue();
    this.accountService.register({
      userName,
      email,
      password,
      confirmPassword,
      acceptedTermsVersion: LEGAL_TERMS_VERSION,
      acceptedTermsAt: acceptedTermsAt || new Date().toISOString(),
    }).subscribe({
      next: (user) => {
        this.submitting.set(false);
        this.registeredEmail.set(user.email || this.registerForm.get('email')?.value);
        this.confirmationEmailSent.set(user.confirmationEmailSent !== false);
        afterNextRender(() => {
          const heading = this.confirmHeading();
          heading?.nativeElement.focus();
        }, { injector: this.injector });
      },
      error: (err) => {
        this.submitting.set(false);
        this.errors = this.readRegisterErrors(err);
      }
    });
  }

  private readRegisterErrors(err: { error?: unknown }): string[] {
    const body = err.error;
    if (Array.isArray(body)) {
      return body.map((item) => {
        if (typeof item === 'string') return item;
        if (item && typeof item === 'object' && 'description' in item) {
          return String((item as { description: unknown }).description);
        }
        return 'Registration failed.';
      });
    }

    if (body && typeof body === 'object') {
      const payload = body as {
        errors?: unknown;
        message?: string;
        registerErrors?: { description?: string }[];
      };

      if (Array.isArray(payload.errors) && payload.errors.length) {
        return payload.errors.filter((item): item is string => typeof item === 'string');
      }

      if (Array.isArray(payload.registerErrors) && payload.registerErrors.length) {
        return payload.registerErrors
          .map((item) => item.description)
          .filter((item): item is string => !!item);
      }

      if (payload.message && payload.message !== 'You made a bad request!') {
        return [payload.message];
      }
    }

    return ['An unexpected error occurred during registration.'];
  }

  resendConfirmation() {
    const email = this.registeredEmail();
    if (!email || this.resending()) {
      return;
    }

    this.resending.set(true);
    this.resendMessage.set(null);
    this.accountService.resendConfirmation(email).subscribe({
      next: (response) => {
        this.resending.set(false);
        this.confirmationEmailSent.set(true);
        this.resendMessage.set(response.message);
      },
      error: () => {
        this.resending.set(false);
        this.resendMessage.set('The confirmation email could not be sent. Check Graph mail sign-in and try again.');
      },
    });
  }
}
