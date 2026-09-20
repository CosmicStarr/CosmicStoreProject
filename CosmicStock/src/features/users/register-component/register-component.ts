import { afterNextRender, Component, ElementRef, inject, Injector, OnInit, signal, viewChild } from '@angular/core';
import { AbstractControl, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AccountService } from '../../../core/services/account-service';

@Component({
  imports: [ReactiveFormsModule, RouterLink],
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
      confirmPassword: ['', [Validators.required]]
    }, { validators: this.passwordMatchValidator });

    const email = this.route.snapshot.queryParamMap.get('email')?.trim();
    if (email) {
      this.registerForm.patchValue({ email });
    }
    this.fromMissingAccount.set(this.route.snapshot.queryParamMap.get('reason') === 'no-account');
  }

  passwordMatchValidator(control: AbstractControl) {
    const password = control.get('password');
    const confirmPassword = control.get('confirmPassword');

    if (password && confirmPassword && password.value !== confirmPassword.value) {
      confirmPassword.setErrors({ mismatch: true });
      return { mismatch: true };
    }
    return null;
  }

  onSubmit() {
    if (this.registerForm.invalid || this.submitting()) return;
    this.errors = [];
    this.submitting.set(true);

    this.accountService.register(this.registerForm.value).subscribe({
      next: (user) => {
        this.submitting.set(false);
        this.registeredEmail.set(user.email || this.registerForm.get('email')?.value);
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
}
