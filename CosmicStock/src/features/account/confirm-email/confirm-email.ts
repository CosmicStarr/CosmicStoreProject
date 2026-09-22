import { DOCUMENT } from '@angular/common';
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
  private document = inject(DOCUMENT);
  private readonly heading = viewChild<ElementRef<HTMLHeadingElement>>('confirmHeading');

  protected readonly loading = signal(true);
  protected readonly message = signal<string | null>(null);
  protected readonly isError = signal(false);
  protected readonly email = signal<string | null>(null);
  protected readonly resending = signal(false);

  ngOnInit(): void {
    const params = this.confirmationParams();
    this.email.set(params.email);

    if ((!params.userId && !params.email) || !params.token) {
      this.finish(true, 'This confirmation link is missing required information.');
      return;
    }

    this.accountService.confirmEmail(params.userId, params.token, params.email ?? undefined).subscribe({
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

  resendConfirmation() {
    const email = this.email()?.trim();
    if (!email || this.resending()) {
      return;
    }

    this.resending.set(true);
    this.accountService.resendConfirmation(email).subscribe({
      next: (response) => {
        this.resending.set(false);
        this.message.set(response.message);
      },
      error: () => {
        this.resending.set(false);
        this.message.set('The confirmation email could not be sent. Try again or contact support.');
      },
    });
  }

  private confirmationParams() {
    const search = this.document.defaultView?.location.search ?? '';
    const fromUrl = new URLSearchParams(search);
    const fromRoute = this.route.snapshot.queryParamMap;

    const userId = (fromUrl.get('userId') ?? fromUrl.get('userid') ?? fromRoute.get('userId') ?? '').trim();
    const email = (fromUrl.get('email') ?? fromRoute.get('email') ?? '').trim();
    const token = (fromUrl.get('token') ?? fromRoute.get('token') ?? '').replace(/ /g, '+').trim();

    return { userId, email, token };
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
