import { Component, inject, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AccountService } from '../../../core/services/account-service';

@Component({
  selector: 'app-reset-password',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './reset-password.html',
  styleUrl: './reset-password.scss',
})
export class ResetPasswordComponent implements OnInit {
  private fb = inject(FormBuilder);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private accountService = inject(AccountService);

  form!: FormGroup;
  message: string | null = null;
  isError = false;
  submitting = false;
  email = '';
  token = '';

  ngOnInit(): void {
    this.email = this.route.snapshot.queryParamMap.get('email') ?? '';
    this.token = this.route.snapshot.queryParamMap.get('token') ?? '';

    this.form = this.fb.group({
      newPassword: ['', [Validators.required, Validators.minLength(8)]],
      confirmPassword: ['', [Validators.required]],
    });
  }

  onSubmit() {
    if (this.form.invalid || !this.email || !this.token) return;

    if (this.form.value.newPassword !== this.form.value.confirmPassword) {
      this.isError = true;
      this.message = 'Passwords do not match.';
      return;
    }

    this.submitting = true;
    this.message = null;
    this.isError = false;

    this.accountService.resetPassword({
      email: this.email,
      token: this.token,
      newPassword: this.form.value.newPassword,
      confirmPassword: this.form.value.confirmPassword,
    }).subscribe({
      next: (response) => {
        this.message = response.message;
        this.router.navigate(['/login']);
      },
      error: (err) => {
        this.submitting = false;
        this.isError = true;
        this.message = err.error?.message || 'Password reset failed.';
      },
    });
  }
}
