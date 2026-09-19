import { afterNextRender, Component, ElementRef, inject, Injector, OnInit, signal, viewChild } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AccountService } from '../../../core/services/account-service';

@Component({
  selector: 'app-forgot-password',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './forgot-password.html',
  styleUrl: './forgot-password.scss',
  host: {
    '(document:keydown.escape)': 'onEscape()',
  },
})
export class ForgotPasswordComponent implements OnInit {
  private fb = inject(FormBuilder);
  private accountService = inject(AccountService);
  private injector = inject(Injector);
  private readonly heading = viewChild<ElementRef<HTMLHeadingElement>>('confirmHeading');
  private readonly submitButton = viewChild<ElementRef<HTMLButtonElement>>('submitButton');

  form!: FormGroup;
  protected readonly showConfirmation = signal(false);
  protected readonly submitting = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly isError = signal(false);

  ngOnInit(): void {
    this.form = this.fb.group({
      email: ['', [Validators.required, Validators.email]],
    });
  }

  onSubmit() {
    if (this.form.invalid || this.submitting()) return;

    this.submitting.set(true);
    this.message.set(null);
    this.isError.set(false);

    const email = this.form.value.email as string;
    this.accountService.forgotPassword(email).subscribe({
      next: () => {
        this.submitting.set(false);
        this.showConfirmation.set(true);
        afterNextRender(() => {
          const heading = this.heading();
          heading?.nativeElement.focus();
        }, { injector: this.injector });
      },
      error: (err: { error?: { message?: string } }) => {
        this.submitting.set(false);
        this.isError.set(true);
        this.message.set(err.error?.message || 'Could not send reset email.');
      },
    });
  }

  onEscape() {
    if (this.showConfirmation()) {
      this.closeModal();
    }
  }

  closeModal() {
    this.showConfirmation.set(false);
    afterNextRender(() => {
      const submitButton = this.submitButton();
      submitButton?.nativeElement.focus();
    }, { injector: this.injector });
  }
}
