import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { WalletHistoryEntry } from '../../models/wallet.models';
import { SessionService } from '../../../../core/session/session.service';
import { UserAvatarComponent } from '../../../../core/components/user-avatar/user-avatar.component';
import { AuthService } from '../../../auth/data-access/auth.service';
import { SessionUser } from '../../../auth/models/auth.models';
import { WalletService } from '../../data-access/wallet.service';
import { Notification, NotificationService } from '../../../../core/notifications/notification.service';
import { NotificationItemComponent } from '../../../dashboard/components/notification-item/notification-item.component';
import { PaymentService } from '../../../payment/data-access/payment.service';
import { WithdrawalRequest } from '../../../payment/models/payment.models';

type ActivityFilter = 'all' | 'income' | 'expenses';
type DateFilter = '30d' | 'all';

@Component({
  selector: 'app-wallet-history-page',
  standalone: true,
  imports: [RouterLink, CommonModule, ReactiveFormsModule, UserAvatarComponent, NotificationItemComponent],
  templateUrl: './wallet-history-page.component.html'
})
export class WalletHistoryPageComponent implements OnInit {
  user: SessionUser | null = null;
  entries: WalletHistoryEntry[] = [];
  filteredEntries: WalletHistoryEntry[] = [];
  isLoading = false;
  errorMessage = '';
  readonly unreadNotificationCount$ = this.notificationService.getUnreadCount();
  readonly notifications$: Observable<Notification[]> = this.notificationService.getNotifications();
  showNotificationPopover = false;

  balance = 0;
  balanceCurrency = 'USD';
  isLoadingBalance = false;

  isWithdrawSubmitting = false;
  withdrawErrorMessage = '';
  withdrawSuccessMessage = '';
  amountValidationError = '';

  isWithdrawalsLoading = false;
  withdrawalsErrorMessage = '';
  withdrawals: WithdrawalRequest[] = [];

  readonly withdrawForm = this.fb.nonNullable.group({
    amount: [1, [Validators.required, Validators.min(1)]],
    notes: ['']
  });

  page = 1;
  pageSize = 10;
  totalCount = 0;
  pageToken: string | null = null;
  nextPageToken: string | null = null;
  pageTokens: Array<string | null> = [null];

  activityFilter: ActivityFilter = 'all';
  dateFilter: DateFilter = '30d';
  searchTerm = '';

  // Download functionality
  isDownloading = false;
  downloadError = '';

  constructor(
    private readonly walletService: WalletService,
    private readonly paymentService: PaymentService,
    private readonly fb: FormBuilder,
    private readonly sessionService: SessionService,
    private readonly authService: AuthService,
    private readonly router: Router,
    private readonly notificationService: NotificationService
  ) {}

  ngOnInit(): void {
    this.user = this.sessionService.currentUser;
    this.loadBalance();
    this.loadHistory();
    this.loadWithdrawals();
  }

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.totalCount / this.pageSize));
  }

  get startItem(): number {
    if (this.totalCount === 0) {
      return 0;
    }

    return (this.page - 1) * this.pageSize + 1;
  }

  get endItem(): number {
    return Math.min(this.page * this.pageSize, this.totalCount);
  }

  get netCashflow(): number {
    return this.filteredEntries.reduce((sum, entry) => sum + entry.amount, 0);
  }

  get positiveTransactions(): number {
    return this.filteredEntries.filter((entry) => this.isCredit(entry)).length;
  }

  setActivityFilter(filter: ActivityFilter): void {
    this.activityFilter = filter;
    this.applyFilters();
  }

  setDateFilter(filter: DateFilter): void {
    this.dateFilter = filter;
    this.applyFilters();
  }

  onSearch(term: string): void {
    this.searchTerm = term;
    this.applyFilters();
  }

  onSearchSubmit(term: string): void {
    this.onSearch(term);
  }

  previousPage(): void {
    if (this.page <= 1 || this.isLoading) {
      return;
    }

    this.page -= 1;
    this.pageToken = this.pageTokens[this.page - 1] ?? null;
    this.loadHistory();
  }

  nextPage(): void {
    if (!this.nextPageToken || this.page >= this.totalPages || this.isLoading) {
      return;
    }

    this.page += 1;
    this.pageToken = this.nextPageToken;
    this.loadHistory();
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

  /**
   * Toggle notifications popover visibility
   */
  toggleNotificationsPopover(): void {
    this.showNotificationPopover = !this.showNotificationPopover;
  }

  /**
   * Close notifications popover
   */
  closeNotificationsPopover(): void {
    this.showNotificationPopover = false;
  }

  /**
   * Handle notification item click
   * Marks notification as read and closes popover
   */
  onNotificationClick(notification: Notification): void {
    if (!notification.isRead) {
      this.notificationService.markAsRead(notification.id).subscribe();
    }
    this.showNotificationPopover = false;
  }

  /**
   * Track by function for notification ngFor optimization
   */
  trackByNotificationId(_: number, notification: Notification): string {
    return notification.id;
  }

  /**
   * Handle notifications button click
   * Shows notifications panel or navigates to notifications page
   */
  onNotificationsClick(): void {
    this.toggleNotificationsPopover();
  }

  /**
   * Handle settings button click
   * Navigates to settings page or opens settings modal
   */
  onSettingsClick(): void {
    // Navigate to profile page
    this.router.navigate(['/profile']);
  }

  trackByEntry(_: number, entry: WalletHistoryEntry): string {
    return entry.id;
  }

  trackByWithdrawal(_: number, withdrawal: WithdrawalRequest): string {
    return withdrawal.id;
  }

  get maxWithdrawAmount(): number {
    return this.balance > 0 ? this.balance : 1;
  }

  onWithdrawSubmit(): void {
    this.withdrawSuccessMessage = '';
    this.withdrawErrorMessage = '';
    this.amountValidationError = '';

    if (this.withdrawForm.invalid || this.isWithdrawSubmitting) {
      this.withdrawForm.markAllAsTouched();
      return;
    }

    const amount = Number(this.withdrawForm.getRawValue().amount);
    if (amount <= 0) {
      this.amountValidationError = 'Le montant doit etre superieur a 0.';
      return;
    }

    if (amount > this.balance) {
      this.amountValidationError = 'Le montant depasse le solde disponible.';
      return;
    }

    this.isWithdrawSubmitting = true;

    const notes = this.withdrawForm.getRawValue().notes?.trim();
    this.paymentService.createWithdrawRequest(amount, notes).subscribe({
      next: (response) => {
        this.isWithdrawSubmitting = false;

        if (!response.success) {
          this.withdrawErrorMessage = response.errorMessage || 'Impossible de soumettre la demande de retrait.';
          return;
        }

        this.withdrawSuccessMessage = 'Demande de retrait envoyee, en cours de traitement.';
        this.withdrawForm.reset({ amount: 1, notes: '' });

        if (typeof response.newBalance === 'number') {
          this.balance = response.newBalance;
        } else {
          this.loadBalance();
        }

        this.loadWithdrawals();
        this.loadHistory();
      },
      error: (error: HttpErrorResponse) => {
        this.isWithdrawSubmitting = false;
        const backendMessage = error.error?.errorMessage ?? error.error?.message;
        this.withdrawErrorMessage = backendMessage || 'Impossible de soumettre la demande de retrait.';
      }
    });
  }

  getWithdrawalStatusClass(status: string): string {
    const normalized = status.toLowerCase();

    if (normalized === 'approved') {
      return 'bg-secondary-container text-on-secondary-container';
    }

    if (normalized === 'rejected' || normalized === 'failed') {
      return 'bg-rose-100 text-rose-700';
    }

    return 'bg-amber-100 text-amber-800';
  }

  isCredit(entry: WalletHistoryEntry): boolean {
    return entry.type.toLowerCase() === 'credit';
  }

  formatAmount(entry: WalletHistoryEntry): string {
    const amount = Math.abs(entry.amount).toLocaleString('en-US', {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    });

    return `${this.isCredit(entry) ? '+' : '-'}$${amount}`;
  }

  /**
   * Return a user-friendly description. Masks emails partially for privacy and
   * preserves original text when no email is present.
   */
  formatDescription(entry: WalletHistoryEntry): string {
    if (!entry.description) return 'Wallet operation';

    // Simple email regex
    const emailRegex = /([a-zA-Z0-9._%+-]+)@([a-zA-Z0-9.-]+\.[a-zA-Z]{2,})/g;

    return entry.description.replace(emailRegex, (_match, local, domain) => {
      const keep = Math.ceil(local.length / 2);
      const visible = local.slice(0, keep);
      const masked = '*'.repeat(Math.max(3, local.length - keep));
      return `${visible}${masked}@${domain}`;
    });
  }

  onDownloadHistory(): void {
    if (this.isDownloading) return;

    this.isDownloading = true;
    this.downloadError = '';

    // Calculate date range based on current filter
    const endDate = new Date();
    const startDate = new Date();

    if (this.dateFilter === '30d') {
      startDate.setDate(startDate.getDate() - 30);
    } else {
      // For 'all' filter, use a reasonable range (e.g., last year)
      startDate.setFullYear(startDate.getFullYear() - 1);
    }

    this.walletService.exportHistory(
      startDate.toISOString().split('T')[0],
      endDate.toISOString().split('T')[0]
    ).subscribe({
      next: (blob: Blob) => {
        this.isDownloading = false;
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `wallet-history-${new Date().toISOString().split('T')[0]}.csv`;
        document.body.appendChild(a);
        a.click();
        window.URL.revokeObjectURL(url);
        document.body.removeChild(a);
      },
      error: () => {
        this.isDownloading = false;
        this.downloadError = 'Failed to download history. Please try again.';
      }
    });
  }

  private loadBalance(): void {
    this.isLoadingBalance = true;

    this.walletService.getBalance().subscribe({
      next: (balance) => {
        this.balance = balance.balance;
        this.balanceCurrency = balance.currency || 'USD';
        this.isLoadingBalance = false;
      },
      error: () => {
        this.isLoadingBalance = false;
      }
    });
  }

  private loadWithdrawals(): void {
    this.isWithdrawalsLoading = true;
    this.withdrawalsErrorMessage = '';

    this.paymentService.getWithdrawals().subscribe({
      next: (withdrawals) => {
        this.withdrawals = withdrawals;
        this.isWithdrawalsLoading = false;
      },
      error: () => {
        this.withdrawals = [];
        this.withdrawalsErrorMessage = 'Impossible de charger l historique des retraits.';
        this.isWithdrawalsLoading = false;
      }
    });
  }

  private loadHistory(): void {
    this.isLoading = true;
    this.errorMessage = '';

    this.walletService.getHistory(this.pageSize, this.pageToken).subscribe({
      next: (response) => {
        this.entries = response.entries;
        this.totalCount = response.totalCount;
        this.nextPageToken = response.nextPageToken;
        this.pageTokens[this.page] = response.nextPageToken;
        this.applyFilters();
        this.isLoading = false;
      },
      error: () => {
        this.entries = [];
        this.filteredEntries = [];
        this.isLoading = false;
        this.errorMessage = 'Unable to load wallet history.';
      }
    });
  }

  private applyFilters(): void {
    const normalizedSearch = this.searchTerm.trim().toLowerCase();

    this.filteredEntries = this.entries.filter((entry) => {
      if (this.activityFilter === 'income' && !this.isCredit(entry)) {
        return false;
      }

      if (this.activityFilter === 'expenses' && this.isCredit(entry)) {
        return false;
      }

      if (this.dateFilter === '30d') {
        const entryDate = new Date(entry.createdAt);
        const daysDiff = (Date.now() - entryDate.getTime()) / (1000 * 60 * 60 * 24);
        if (daysDiff > 30) {
          return false;
        }
      }

      if (!normalizedSearch) {
        return true;
      }

      return this.getSearchableEntryText(entry).includes(normalizedSearch);
    });
  }

  private getSearchableEntryText(entry: WalletHistoryEntry): string {
    const parts = [
      entry.description,
      entry.type,
      entry.amount.toFixed(2),
      new Date(entry.createdAt).toLocaleDateString('en-US', {
        year: 'numeric',
        month: 'short',
        day: 'numeric'
      })
    ];

    return parts
      .filter((value): value is string => typeof value === 'string' && value.length > 0)
      .join(' ')
      .toLowerCase();
  }
}