import { Component, inject, OnInit, signal } from '@angular/core';
import { AdminUsersService, IAdminUser } from '../../../core/services/admin-users';

type PendingAction =
  | { type: 'revoke' | 'lock' | 'unlock' | 'delete' | 'reset'; user: IAdminUser }
  | null;

@Component({
  selector: 'app-admin-users',
  templateUrl: './admin-users.html',
  styleUrl: './admin-users.scss',
  host: { class: 'admin-users-page' },
})
export class AdminUsersComponent implements OnInit {
  private adminUsers = inject(AdminUsersService);

  protected users = signal<IAdminUser[]>([]);
  protected loading = signal(true);
  protected busyId = signal<string | null>(null);
  protected message = signal<string | null>(null);
  protected pending = signal<PendingAction>(null);

  ngOnInit(): void {
    this.load();
  }

  load() {
    this.loading.set(true);
    this.adminUsers.getUsers().subscribe({
      next: (users) => {
        this.users.set(Array.isArray(users) ? users : []);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.message.set(err.error?.message || 'Users could not be loaded.');
      },
    });
  }

  isAdmin(user: IAdminUser): boolean {
    return (user.roles ?? []).some((role) => role.toLowerCase() === 'admin');
  }

  rolesLabel(user: IAdminUser): string {
    const roles = user.roles ?? [];
    return roles.length ? roles.join(', ') : 'Customer';
  }

  askRevoke(user: IAdminUser): void {
    this.pending.set({ type: 'revoke', user });
  }

  askLock(user: IAdminUser): void {
    this.pending.set({ type: 'lock', user });
  }

  askUnlock(user: IAdminUser): void {
    this.pending.set({ type: 'unlock', user });
  }

  askDelete(user: IAdminUser): void {
    this.pending.set({ type: 'delete', user });
  }

  askReset(user: IAdminUser): void {
    this.pending.set({ type: 'reset', user });
  }

  cancelPending(): void {
    this.pending.set(null);
  }

  confirmPending(): void {
    const action = this.pending();
    if (!action) {
      return;
    }

    const { type, user } = action;
    this.pending.set(null);
    this.busyId.set(user.id);
    this.message.set(null);

    if (type === 'delete') {
      this.adminUsers.deleteUser(user.id).subscribe({
        next: () => {
          this.busyId.set(null);
          this.users.update((list) => list.filter((item) => item.id !== user.id));
          this.message.set(`Deleted ${user.email}.`);
        },
        error: (err: { error?: { message?: string } }) => {
          this.busyId.set(null);
          this.message.set(err.error?.message || 'Action failed.');
        },
      });
      return;
    }

    if (type === 'reset') {
      this.adminUsers.sendResetPassword(user.id).subscribe({
        next: (result) => {
          this.busyId.set(null);
          this.message.set(result.message || `Reset email sent to ${user.email}.`);
        },
        error: (err: { error?: { message?: string } }) => {
          this.busyId.set(null);
          this.message.set(err.error?.message || 'Action failed.');
        },
      });
      return;
    }

    if (type === 'unlock') {
      this.adminUsers.unlockUser(user.id).subscribe({
        next: (result) => {
          this.busyId.set(null);
          this.replaceUser(result.user);
          this.message.set(result.message || `Unlocked ${user.email}.`);
        },
        error: (err: { error?: { message?: string; user?: IAdminUser } }) => {
          this.busyId.set(null);
          if (err.error?.user) {
            this.replaceUser(err.error.user);
          }
          this.message.set(err.error?.message || 'Action failed.');
        },
      });
      return;
    }

    const request =
      type === 'revoke'
        ? this.adminUsers.revokeAdmin(user.id)
        : this.adminUsers.lockUser(user.id);

    request.subscribe({
      next: (updated) => {
        this.busyId.set(null);
        this.replaceUser(updated);
        this.message.set(
          type === 'revoke'
            ? `Removed Admin from ${user.email}.`
            : `Locked ${user.email}.`,
        );
      },
      error: (err: { error?: { message?: string } }) => {
        this.busyId.set(null);
        this.message.set(err.error?.message || 'Action failed.');
      },
    });
  }

  grantAdmin(user: IAdminUser): void {
    this.runUserAction(user.id, this.adminUsers.grantAdmin(user.id), `Granted Admin to ${user.email}.`);
  }

  lockedLabel(user: IAdminUser): string {
    if (!user.isLocked) {
      return 'No';
    }
    return user.isPermanentlyLocked ? 'Permanent' : 'Temporary';
  }

  pendingTitle(): string {
    const action = this.pending();
    if (!action) {
      return '';
    }
    switch (action.type) {
      case 'revoke':
        return `Remove Admin from ${action.user.email}?`;
      case 'lock':
        return `Permanently lock ${action.user.email}?`;
      case 'unlock':
        return `Unlock ${action.user.email}?`;
      case 'delete':
        return `Delete ${action.user.email}?`;
      case 'reset':
        return `Send password reset to ${action.user.email}?`;
    }
  }

  pendingText(): string {
    const action = this.pending();
    if (!action) {
      return '';
    }
    switch (action.type) {
      case 'revoke':
        return 'They will lose admin access immediately.';
      case 'lock':
        return 'This creates a security lock that does not expire. Only an admin can unlock it.';
      case 'unlock':
        return action.user.isPermanentlyLocked
          ? `Before unlocking, contact the user at their registered email (${action.user.email}) to verify their identity. Unlock clears the lock and immediately emails a mandatory password reset to that same address—not any pending email-change address.`
          : 'This clears a temporary failed-password lockout and emails a password-reset link to the registered address.';
      case 'delete':
        return 'This permanently deletes the account. This cannot be undone.';
      case 'reset':
        return 'They will receive the same password-reset email used on the account page.';
    }
  }

  pendingConfirmLabel(): string {
    const action = this.pending();
    if (!action) {
      return 'Confirm';
    }
    switch (action.type) {
      case 'revoke':
        return 'Remove Admin';
      case 'lock':
        return 'Lock account';
      case 'unlock':
        return 'Unlock and email reset';
      case 'delete':
        return 'Delete user';
      case 'reset':
        return 'Send reset email';
    }
  }

  private runUserAction(id: string, request: import('rxjs').Observable<IAdminUser>, success: string): void {
    this.busyId.set(id);
    this.message.set(null);
    request.subscribe({
      next: (user) => {
        this.busyId.set(null);
        this.replaceUser(user);
        this.message.set(success);
      },
      error: (err) => {
        this.busyId.set(null);
        this.message.set(err.error?.message || 'Action failed.');
      },
    });
  }

  private replaceUser(updated: IAdminUser): void {
    this.users.update((list) => list.map((user) => (user.id === updated.id ? updated : user)));
  }
}
