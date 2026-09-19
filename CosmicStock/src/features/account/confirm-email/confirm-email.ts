import { afterNextRender, Component, ElementRef, inject, Injector, OnInit, signal, viewChild } from '@angular/core';
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
  private injector = inject(Injector);
  private readonly heading = viewChild<ElementRef<HTMLHeadingElement>>('confirmHeading');

  protected readonly loading = signal(true);
  protected readonly message = signal<string | null>(null);
  protected readonly isError = signal(false);

  ngOnInit(): void {
    const userId = this.route.snapshot.queryParamMap.get('userId');
    const token = (this.route.snapshot.queryParamMap.get('token') ?? '').replace(/ /g, '+');

    if (!userId || !token) {
      this.finish(true, 'This confirmation link is missing required information.');
      return;
    }

    this.accountService.confirmEmail(userId, token).subscribe({
      next: (response) => {
        if (this.accountService.currentUserValue) {
          this.accountService.loadCurrentUser().subscribe();
        }
        this.finish(false, response.message);
      },
      error: (err: { error?: { message?: string } }) => {
        this.finish(true, err.error?.message || 'Email confirmation failed. The link may have expired.');
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
