import { Component, inject, OnInit, signal } from '@angular/core';
import { AdminUsersService, IAdminUser } from '../../../core/services/admin-users';

@Component({
  selector: 'app-admin-users',
  templateUrl: './admin-users.html',
  styleUrl: './admin-users.scss',
})
export class AdminUsersComponent implements OnInit {
  private adminUsers = inject(AdminUsersService);
  protected users = signal<IAdminUser[]>([]);
  loading = true;

  ngOnInit(): void {
    this.adminUsers.getUsers().subscribe({
      next: (users) => {
        this.users.set(users);
        this.loading = false;
      },
      error: () => {
        this.loading = false;
      },
    });
  }
}
