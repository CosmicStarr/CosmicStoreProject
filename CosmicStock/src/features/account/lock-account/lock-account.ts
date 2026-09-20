import { afterNextRender, Component, ElementRef, inject, Injector, OnInit, signal, viewChild } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AccountService } from '../../../core/services/account-service';

@Component({
  selector: 'app-lock-account',
  imports: [RouterLink],
  templateUrl: './lock-account.html',
  styleUrl: './lock-account.scss',
})
export class LockAccountComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private accountService = inject(AccountService);
  private injector = inject(Injector);
  private readonly heading = viewChild<ElementRef<HTMLHeadingElement>>('confirmHeading');

  protected readonly loading = signal(true);
  protected readonly message = signal<string | null>(null);
  protected readonly isError = signal(false);

  ngOnInit(): void {
    const userId = this.route.snapshot.queryParamMap.get('userId');
    const token = (this.route.snapshot.queryParamMap.get('token') ?? '').replace(/ /g, '+');

    if (!userId || !token) {
      this.finish(true, 'This lock link is missing required information.');
      return;
    }

    this.accountService.lockAccount(userId, token).subscribe({
      next: (response) => this.finish(false, response.message),
      error: (err: { error?: { message?: string } }) => {
        this.finish(true, err.error?.message || 'This lock link is invalid or has expired.');
      },
    });
  }

  private finish(error: boolean, text: string) {
    this.loading.set(false);
    this.isError.set(error);
    this.message.set(text);
    afterNextRender(() => {
      const heading = this.heading();
      heading?.nativeElement.focus();
    }, { injector: this.injector });
  }
}
