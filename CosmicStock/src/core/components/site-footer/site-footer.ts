import { AsyncPipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AccountService } from '../../services/account-service';

@Component({
  selector: 'app-site-footer',
  imports: [RouterLink, AsyncPipe],
  templateUrl: './site-footer.html',
  styleUrl: './site-footer.scss',
})
export class SiteFooterComponent {
  protected accountService = inject(AccountService);
}
