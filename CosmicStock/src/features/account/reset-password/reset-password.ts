import { afterNextRender, Component, ElementRef, inject, Injector, OnInit, signal, viewChild } from '@angular/core';
import { AbstractControl, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AccountService } from '../../../core/services/account-service';

const passwordPattern = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,20}$/;

@Component({
  selector: 'app-reset-password',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './reset-password.html',
  styleUrl: './reset-password.scss',
})
export class ResetPasswordComponent implements OnInit {
  private fb = inject(FormBuilder);
  private route = inject(ActivatedRoute);
  private accountService = inject(AccountService);
  private injector = inject(Injector);
  private readonly heading = viewChild<ElementRef<HTMLHeadingElement>>('confirmHeading');

  form!: FormGroup;
  email = '';
  token = '';

  protected readonly verifying = signal(true);
  protected readonly verified = signal(false);
  protected readonly updated = signal(false);
  protected readonly submitting = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly isError = signal(false);

  ngOnInit(): void {
    this.email = this.route.snapshot.queryParamMap.get('email') ?? '';
    this.token = (this.route.snapshot.queryParamMap.get('token') ?? '').replace(/ /g, '+');

    this.form = this.fb.group({
      newPassword: ['', [Validators.required, Validators.minLength(8), Validators.maxLength(20), Validators.pattern(passwordPattern)]],
      confirmPassword: ['', [Validators.required]],
    }, { validators: this.passwordMatchValidator });

    if (!this.email || !this.token) {
      this.verifying.set(false);
      this.isError.set(true);
      this.message.set('This reset link is missing required information.');
      this.focusHeading();
      return;
    }

    this.accountService.verifyResetPassword(this.email, this.token).subscribe({
      next: (response) => {
        this.verifying.set(false);
        this.verified.set(true);
        this.message.set(response.message);
      },
      error: (err: { error?: { message?: string; errors?: string[] } }) => {
        this.verifying.set(false);
        this.verified.set(false);
        this.isError.set(true);
        this.message.set(this.readError(err) || 'This reset link is invalid or has expired.');
        this.focusHeading();
      },
    });
  }

  passwordMatchValidator(control: AbstractControl) {
    const password = control.get('newPassword')?.value;
    const confirmPassword = control.get('confirmPassword')?.value;
    if (!password || !confirmPassword) {
      return null;
    }
    return password === confirmPassword ? null : { mismatch: true };
  }

  onSubmit() {
    if (this.form.invalid || this.submitting() || !this.email || !this.token) return;

    this.submitting.set(true);
    this.message.set(null);
    this.isError.set(false);

    this.accountService.resetPassword({
      email: this.email,
      token: this.token,
      newPassword: this.form.value.newPassword,
      confirmPassword: this.form.value.confirmPassword,
    }).subscribe({
      next: (response) => {
        this.submitting.set(false);
        this.updated.set(true);
        this.message.set(response.message);
        this.focusHeading();
      },
      error: (err: { error?: { message?: string; errors?: string[] } }) => {
        this.submitting.set(false);
        this.isError.set(true);
        this.message.set(this.readError(err) || 'Password reset failed.');
      },
    });
  }

  private readError(err: { error?: { message?: string; errors?: string[] } }) {
    const details = err.error?.errors?.filter((item): item is string => typeof item === 'string');
    if (details?.[0]) {
      return details[0];
    }

    const message = err.error?.message;
    if (message && message !== 'You made a bad request!') {
      return message;
    }

    return null;
  }

  private focusHeading() {
    afterNextRender(() => {
      const heading = this.heading();
      heading?.nativeElement.focus();
    }, { injector: this.injector });
  }
}
