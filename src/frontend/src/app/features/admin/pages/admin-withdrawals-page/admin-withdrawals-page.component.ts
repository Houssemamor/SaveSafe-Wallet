import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../auth/data-access/auth.service';
import { SessionService } from '../../../../core/session/session.service';
import { SessionUser } from '../../../auth/models/auth.models';
import { AdminService } from '../../data-access/admin.service';
import { AdminUser, AdminWithdrawalRequest, AdminWithdrawalStatus } from '../../models/admin.models';

type WithdrawalFilter = 'All' | 'Pending' | 'Approved' | 'Rejected';

@Component({
  selector: 'app-admin-withdrawals-page',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './admin-withdrawals-page.component.html'
})
export class AdminWithdrawalsPageComponent implements OnInit {
  user: SessionUser | null = null;

  isLoading = false;
  errorMessage = '';
  successMessage = '';

  usersById = new Map<string, AdminUser>();
  withdrawals: AdminWithdrawalRequest[] = [];

  activeFilter: WithdrawalFilter = 'All';

  rowActionLoading = new Set<string>();

  rejectModalOpen = false;
  rejectReason = '';
  rejectTarget: AdminWithdrawalRequest | null = null;

  constructor(
    private readonly adminService: AdminService,
    private readonly sessionService: SessionService,
    private readonly authService: AuthService,
    private readonly router: Router
  ) {}

  ngOnInit(): void {
    this.user = this.sessionService.currentUser;
    this.loadData();
  }

  get filteredWithdrawals(): AdminWithdrawalRequest[] {
    if (this.activeFilter === 'All') {
      return this.withdrawals;
    }

    return this.withdrawals.filter((item) => item.status === this.activeFilter);
  }

  setFilter(filter: WithdrawalFilter): void {
    this.activeFilter = filter;
    this.loadWithdrawals();
  }

  getUserLabel(withdrawal: AdminWithdrawalRequest): string {
    const user = this.usersById.get(withdrawal.userId);
    if (!user) {
      return withdrawal.userId;
    }

    return `${user.name} (${user.email})`;
  }

  isRowLoading(id: string): boolean {
    return this.rowActionLoading.has(id);
  }

  canProcess(withdrawal: AdminWithdrawalRequest): boolean {
    return withdrawal.status === 'Pending';
  }

  approve(withdrawal: AdminWithdrawalRequest): void {
    if (!this.canProcess(withdrawal) || this.isRowLoading(withdrawal.id)) {
      return;
    }

    this.errorMessage = '';
    this.successMessage = '';
    this.rowActionLoading.add(withdrawal.id);

    this.adminService.approveWithdrawalRequest(withdrawal.id).subscribe({
      next: (updated) => {
        this.rowActionLoading.delete(withdrawal.id);
        this.patchRow(updated);
        this.successMessage = 'Demande approuvee avec succes.';
      },
      error: (error: HttpErrorResponse) => {
        this.rowActionLoading.delete(withdrawal.id);
        this.errorMessage = error.error?.message || 'Impossible d approuver la demande.';
      }
    });
  }

  openRejectModal(withdrawal: AdminWithdrawalRequest): void {
    if (!this.canProcess(withdrawal) || this.isRowLoading(withdrawal.id)) {
      return;
    }

    this.rejectTarget = withdrawal;
    this.rejectReason = '';
    this.rejectModalOpen = true;
  }

  closeRejectModal(): void {
    this.rejectModalOpen = false;
    this.rejectReason = '';
    this.rejectTarget = null;
  }

  confirmReject(): void {
    if (!this.rejectTarget) {
      return;
    }

    const target = this.rejectTarget;
    this.rowActionLoading.add(target.id);
    this.errorMessage = '';
    this.successMessage = '';

    this.adminService.rejectWithdrawalRequest(target.id, this.rejectReason).subscribe({
      next: (updated) => {
        this.rowActionLoading.delete(target.id);
        this.patchRow(updated);
        this.successMessage = 'Demande rejetee et montant rembourse.';
        this.closeRejectModal();
      },
      error: (error: HttpErrorResponse) => {
        this.rowActionLoading.delete(target.id);
        this.errorMessage = error.error?.message || 'Impossible de rejeter la demande.';
      }
    });
  }

  statusClass(status: AdminWithdrawalStatus): string {
    const normalized = status.toLowerCase();

    if (normalized === 'approved') {
      return 'bg-emerald-100 text-emerald-700 border-emerald-200';
    }

    if (normalized === 'rejected' || normalized === 'failed') {
      return 'bg-rose-100 text-rose-700 border-rose-200';
    }

    return 'bg-amber-100 text-amber-800 border-amber-200';
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

  private loadData(): void {
    this.isLoading = true;
    this.errorMessage = '';

    this.adminService.getUsers(500).subscribe({
      next: (users) => {
        this.usersById = new Map(users.map((user) => [user.userId, user]));
        this.loadWithdrawals();
      },
      error: () => {
        this.usersById = new Map();
        this.loadWithdrawals();
      }
    });
  }

  private loadWithdrawals(): void {
    const backendFilter = this.activeFilter === 'All' ? undefined : this.activeFilter;

    this.adminService.getWithdrawalRequests(backendFilter).subscribe({
      next: (items) => {
        this.withdrawals = items;
        this.isLoading = false;
      },
      error: (error: HttpErrorResponse) => {
        this.withdrawals = [];
        this.isLoading = false;
        this.errorMessage = error.error?.message || 'Impossible de charger les demandes de retrait.';
      }
    });
  }

  private patchRow(updated: AdminWithdrawalRequest): void {
    this.withdrawals = this.withdrawals.map((item) => item.id === updated.id ? updated : item);
  }
}
