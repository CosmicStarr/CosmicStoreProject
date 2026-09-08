import { Component, inject, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AccountService } from '../../../core/services/account-service';

@Component({
  selector: 'app-forgot-password',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './forgot-password.html',
  styleUrl: './forgot-password.scss',
})
export class ForgotPasswordComponent implements OnInit {
  private fb = inject(FormBuilder);
  private accountService = inject(AccountService);

  form!: FormGroup;
  message: string | null = null;
  isError = false;
  submitted = false;

  ngOnInit(): void {
    this.form = this.fb.group({
      email: ['', [Validators.required, Validators.email]],
    });
  }

  onSubmit() {
    if (this.form.invalid) return;

    this.submitted = true;
    this.message = null;
    this.isError = false;

    this.accountService.forgotPassword(this.form.value.email).subscribe({
      next: (response) => {
        this.submitted = false;
        this.message = response.message;
      },
      error: (err) => {
        this.submitted = false;
        this.isError = true;
        this.message = err.error?.message || 'Could not send reset email.';
      },
    });
  }
}
