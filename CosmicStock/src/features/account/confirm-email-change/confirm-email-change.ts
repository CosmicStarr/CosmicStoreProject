import { afterNextRender, Component, ElementRef, inject, Injector, OnInit, signal, viewChild } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AccountService } from '../../../core/services/account-service';

@Component({
  selector: 'app-confirm-email-change',
  imports: [RouterLink],
  templateUrl: './confirm-email-change.html',
  styleUrl: './confirm-email-change.scss',
})
export class ConfirmEmailChangeComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private accountService = inject(AccountService);
  private injector = inject(Injector);
  private readonly heading = viewChild<ElementRef<HTMLHeadingElement>>('confirmHeading');

  protected readonly loading = signal(true);
  protected readonly message = signal<string | null>(null);
  protected readonly isError = signal(false);

  ngOnInit(): void {
    const userId = this.route.snapshot.queryParamMap.get('userId');
    const email = this.route.snapshot.queryParamMap.get('email');
    const token = (this.route.snapshot.queryParamMap.get('token') ?? '').replace(/ /g, '+');

    if (!userId || !email || !token) {
      this.finish(true, 'This verification link is missing required information.');
      return;
    }

    this.accountService.confirmEmailChange(userId, token, email).subscribe({
      next: (response) => {
        this.accountService.clearSession();
        this.finish(false, response.message);
      },
      error: (err: { error?: { message?: string } }) => {
        this.finish(true, err.error?.message || 'This verification link is invalid or has expired.');
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
