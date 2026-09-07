import { Component, inject, OnInit } from '@angular/core';
import { AbstractControl, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { AccountService } from '../../../core/services/account-service';
import { Router, RouterLink } from '@angular/router';

@Component({
  imports: [ReactiveFormsModule, RouterLink],
  selector: 'app-register-component',
  styleUrl: './register-component.scss',
  templateUrl: './register-component.html',
})
export class RegisterComponent implements OnInit {

  registerForm!: FormGroup;
  private accountService = inject(AccountService);
  private router = inject(Router);
  private fb = inject(FormBuilder);
  errors: string[] = [];
  ngOnInit(): void {
  this.registerForm = this.fb.group({
    userName: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]],
    confirmPassword: ['', [Validators.required]]
  }, { validators: this.passwordMatchValidator });

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
    if (this.registerForm.invalid) return;
    console.log('Form submitted:', this.registerForm.value);
    this.errors = [];

    this.accountService.register(this.registerForm.value).subscribe({
      next: (user) => {
        // Since a newly registered user defaults to a regular customer role, route them to the shop
        this.router.navigateByUrl('/store');
      },
      error: (err) => {
        // ASP.NET Identity typically returns an array of error objects: [{code: '...', description: '...'}]
        if (Array.isArray(err.error)) {
          this.errors = err.error.map((e: { description: string }) => e.description);
        } else if (err.error?.message) {
          this.errors = [err.error.message];
        } else {
          this.errors = ['An unexpected error occurred during registration.'];
        }
      }
    });
  }



}
