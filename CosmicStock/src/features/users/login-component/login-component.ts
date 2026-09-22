import { Component, inject, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AccountService } from '../../../core/services/account-service';
import { BrandLogoComponent } from '../../../core/components/brand-logo/brand-logo';
import { getRoleFromToken } from '../../../core/utils/auth-utils';

@Component({
  imports: [ReactiveFormsModule, RouterLink, BrandLogoComponent],
  selector: 'app-login-component',
  styleUrl: './login-component.scss',
  templateUrl: './login-component.html',
})
export class LoginComponent implements OnInit {

loginForm!: FormGroup;
error: string | null = null;

private router = inject(Router);
private route = inject(ActivatedRoute);
private accountService = inject(AccountService);

private fb = inject(FormBuilder);

ngOnInit(): void {
  this.loginForm = this.fb.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(6)]]
  });
}


onSubmit() {
    if (this.loginForm.invalid) return;

    this.accountService.login(this.loginForm.value).subscribe({
      next: (user) => {
        const returnUrl = this.safeReturnUrl();
        if (returnUrl) {
          void this.router.navigateByUrl(returnUrl);
          return;
        }

        const role = getRoleFromToken(user.token);

        if (role === 'Admin') {
          this.router.navigateByUrl('/admin/dashboard');
        } else {
          this.router.navigateByUrl('/store');
        }
      },
      error: (err) => {
        const payload = err?.error as { code?: string; message?: string } | undefined;
        if (payload?.code === 'accountNotFound') {
          const email = String(this.loginForm.get('email')?.value ?? '').trim();
          this.router.navigate(['/register'], {
            queryParams: {
              email,
              reason: 'no-account',
            },
          });
          return;
        }

        this.error = payload?.message || 'Invalid email or password';
      }
    });
  }

  private safeReturnUrl(): string | null {
    const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
    if (!returnUrl || !returnUrl.startsWith('/') || returnUrl.startsWith('//')) {
      return null;
    }
    return returnUrl;
  }
}
