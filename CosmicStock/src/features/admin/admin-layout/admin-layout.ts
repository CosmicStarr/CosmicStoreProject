import { Component } from '@angular/core';
import { RouterOutlet, RouterLink } from '@angular/router';

@Component({
  selector: 'app-admin-layout',
  standalone: true,
  imports: [], // Mandatory for routerLink and router-outlet to work
  templateUrl: '../admin-layout/admin-layout.html',
  styleUrls: ['../admin-layout/admin-layout.scss']
})
export class AdminLayoutComponent {
  
}
