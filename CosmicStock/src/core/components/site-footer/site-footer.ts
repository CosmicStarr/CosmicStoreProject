import { AsyncPipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AccountService } from '../../services/account-service';
import { BrandLogoComponent } from '../brand-logo/brand-logo';

@Component({
  selector: 'app-site-footer',
  imports: [RouterLink, AsyncPipe, BrandLogoComponent],
  templateUrl: './site-footer.html',
  styleUrl: './site-footer.scss',
})
export class SiteFooterComponent {
  protected accountService = inject(AccountService);
}
