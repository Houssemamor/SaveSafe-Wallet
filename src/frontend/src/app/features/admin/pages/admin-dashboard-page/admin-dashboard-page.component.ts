import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { SessionService } from '../../../../core/session/session.service';
import { AuthService } from '../../../auth/data-access/auth.service';
import { SessionUser } from '../../../auth/models/auth.models';
import { AdminService } from '../../data-access/admin.service';
import {
  AdminFailedLoginByIp,
  AdminLoginEvent,
  AdminSecuritySummary,
  AdminUser
} from '../../models/admin.models';

@Component({
  selector: 'app-admin-dashboard-page',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './admin-dashboard-page.component.html'
})
export class AdminDashboardPageComponent implements OnInit {
  user: SessionUser | null = null;
  summary: AdminSecuritySummary | null = null;
  failedLogins: AdminFailedLoginByIp[] = [];
  loginEvents: AdminLoginEvent[] = [];
  users: AdminUser[] = [];

  isLoading = true;
  isRefreshing = false;
  errorMessage = '';

  constructor(
    private readonly adminService: AdminService,
    private readonly sessionService: SessionService,
    private readonly authService: AuthService,
    private readonly router: Router
  ) {}

  ngOnInit(): void {
    this.user = this.sessionService.currentUser;
    this.loadDashboard();
  }

  logout(): void {
    this.authService.logout().subscribe({
      next: () => this.router.navigate(['/login']),
      error: () => {
        this.authService.clearLocalSession();
        this.router.navigate(['/login']);
      }
    });
  }

  formatDate(value: string | null): string {
    if (!value) {
      return 'N/A';
    }

    return new Date(value).toLocaleString();
  }

  private loadDashboard(): void {
    this.isLoading = true;
    this.errorMessage = '';

    forkJoin({
      summary: this.adminService.getSecuritySummary(),
      failedLogins: this.adminService.getFailedLogins(10),
      loginEvents: this.adminService.getLoginEvents(25),
      users: this.adminService.getUsers(20)
    }).subscribe({
      next: ({ summary, failedLogins, loginEvents, users }) => {
        this.summary = summary;
        this.failedLogins = failedLogins;
        this.loginEvents = loginEvents;
        this.users = users;
        this.isLoading = false;
      },
      error: () => {
        this.errorMessage = 'Unable to load admin dashboard data.';
        this.isLoading = false;
      }
    });
  }

  refreshSummary(): void {
    if (this.isRefreshing) {
      return;
    }

    this.isRefreshing = true;
    this.adminService.refreshSecuritySummary().subscribe({
      next: (summary) => {
        this.summary = summary;
        this.isRefreshing = false;
      },
      error: () => {
        this.errorMessage = 'Unable to refresh security summary.';
        this.isRefreshing = false;
      }
    });
  }

  suspendUser(userId: string): void {
    if (!confirm('Are you sure you want to suspend this user?')) {
      return;
    }

    this.adminService.suspendUser(userId).subscribe({
      next: () => {
        this.loadDashboard(); // Reload to show updated status
      },
      error: () => {
        this.errorMessage = 'Unable to suspend user.';
      }
    });
  }

  activateUser(userId: string): void {
    if (!confirm('Are you sure you want to activate this user?')) {
      return;
    }

    this.adminService.activateUser(userId).subscribe({
      next: () => {
        this.loadDashboard(); // Reload to show updated status
      },
      error: () => {
        this.errorMessage = 'Unable to activate user.';
      }
    });
  }

  deleteUser(userId: string): void {
    if (!confirm('Are you sure you want to delete this user? This action cannot be undone.')) {
      return;
    }

    this.adminService.deleteUser(userId).subscribe({
      next: () => {
        this.loadDashboard(); // Reload to show updated user list
      },
      error: () => {
        this.errorMessage = 'Unable to delete user.';
      }
    });
  }

  canSuspendUser(user: AdminUser): boolean {
    return user.accountStatus === 'Active';
  }

  canActivateUser(user: AdminUser): boolean {
    return user.accountStatus === 'Suspended';
  }

  canDeleteUser(user: AdminUser): boolean {
    return user.accountStatus !== 'Deleted';
  }
}
