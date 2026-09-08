import { Component, inject, OnInit } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AccountService } from '../../../core/services/account-service';

@Component({
  selector: 'app-confirm-email',
  imports: [RouterLink],
  templateUrl: './confirm-email.html',
  styleUrl: './confirm-email.scss',
})
export class ConfirmEmailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private accountService = inject(AccountService);

  loading = true;
  message: string | null = null;
  isError = false;

  ngOnInit(): void {
    const userId = this.route.snapshot.queryParamMap.get('userId');
    const token = this.route.snapshot.queryParamMap.get('token');

    if (!userId || !token) {
      this.loading = false;
      this.isError = true;
      this.message = 'Invalid confirmation link.';
      return;
    }

    this.accountService.confirmEmail(userId, token).subscribe({
      next: (response) => {
        this.loading = false;
        this.message = response.message;
      },
      error: (err) => {
        this.loading = false;
        this.isError = true;
        this.message = err.error?.message || 'Email confirmation failed.';
      },
    });
  }
}
