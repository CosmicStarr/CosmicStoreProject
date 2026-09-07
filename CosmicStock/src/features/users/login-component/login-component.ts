import { Component, inject, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AccountService } from '../../../core/services/account-service';

@Component({
  imports: [ReactiveFormsModule, RouterLink],
  selector: 'app-login-component',
  styleUrl: './login-component.scss',
  templateUrl: './login-component.html',
})
export class LoginComponent implements OnInit {

loginForm!: FormGroup;
error: string | null = null;

private router = inject(Router);
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
        const role = this.getRoleFromToken(user.token);

        if (role === 'Admin') {
          this.router.navigateByUrl('/admin/dashboard');
        } else {
          this.router.navigateByUrl('/store');
        }
      },
      error: (err) => {
        this.error = err.error?.message || 'Invalid email or password';
      }
    });
  }

  private getRoleFromToken(token: string): string {
    try {
      const payloadBase64 = token.split('.')[1];
      const decodedPayload = JSON.parse(atob(payloadBase64));
      
      return decodedPayload['role'] || decodedPayload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] || '';
    } catch (e) {
      return '';
    }
  }


  
}
