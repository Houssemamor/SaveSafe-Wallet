import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { SessionUser } from '../../models/auth.models';
import { WalletHistoryEntry } from '../../models/wallet.models';
import { AuthService } from '../../services/auth.service';
import { SessionService } from '../../services/session.service';
import { WalletService } from '../../services/wallet.service';

type ActivityFilter = 'all' | 'income' | 'expenses';
type DateFilter = '30d' | 'all';

@Component({
  selector: 'app-wallet-history-page',
  standalone: true,
  imports: [RouterLink, CommonModule],
  templateUrl: './wallet-history-page.component.html'
})
export class WalletHistoryPageComponent implements OnInit {
  user: SessionUser | null = null;
  entries: WalletHistoryEntry[] = [];
  filteredEntries: WalletHistoryEntry[] = [];
  isLoading = false;
  errorMessage = '';

  page = 1;
  pageSize = 10;
  totalCount = 0;

  activityFilter: ActivityFilter = 'all';
  dateFilter: DateFilter = '30d';
  searchTerm = '';

  constructor(
    private readonly walletService: WalletService,
    private readonly sessionService: SessionService,
    private readonly authService: AuthService,
    private readonly router: Router
  ) {}

  ngOnInit(): void {
    this.user = this.sessionService.currentUser;
    this.loadHistory();
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

  previousPage(): void {
    if (this.page <= 1 || this.isLoading) {
      return;
    }

    this.page -= 1;
    this.loadHistory();
  }

  nextPage(): void {
    if (this.page >= this.totalPages || this.isLoading) {
      return;
    }

    this.page += 1;
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

  trackByEntry(_: number, entry: WalletHistoryEntry): string {
    return entry.id;
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

  private loadHistory(): void {
    this.isLoading = true;
    this.errorMessage = '';

    this.walletService.getHistory(this.page, this.pageSize).subscribe({
      next: (response) => {
        this.entries = response.entries;
        this.totalCount = response.totalCount;
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

      return (entry.description ?? '').toLowerCase().includes(normalizedSearch);
    });
  }
}