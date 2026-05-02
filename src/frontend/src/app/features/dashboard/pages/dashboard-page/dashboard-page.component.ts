import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { SessionService } from '../../../../core/session/session.service';
import { AuthService } from '../../../auth/data-access/auth.service';
import { SessionUser } from '../../../auth/models/auth.models';
import { WalletService } from '../../../wallet-history/data-access/wallet.service';
import { WalletBalanceResponse } from '../../../wallet-history/models/wallet.models';

@Component({
  selector: 'app-dashboard-page',
  standalone: true,
  imports: [RouterLink, CommonModule, ReactiveFormsModule],
  templateUrl: './dashboard-page.component.html'
})
export class DashboardPageComponent implements OnInit {
  user: SessionUser | null = null;
  balance: WalletBalanceResponse | null = null;
  balanceError = '';

  // Transfer form
  isTransferring = false;
  transferError = '';
  transferSuccess = '';
  showTransferModal = false;

  readonly transferForm = this.fb.nonNullable.group({
    recipientEmail: ['', [Validators.required, Validators.email]],
    amount: ['', [Validators.required, Validators.min(0.01)]],
    description: ['']
  });

  // Download functionality
  isDownloading = false;
  downloadError = '';

  get liquidityIntegerPart(): string {
    const amount = this.balance?.balance ?? 0;
    return Math.trunc(amount).toLocaleString('en-US');
  }

  get liquidityDecimalPart(): string {
    const amount = this.balance?.balance ?? 0;
    const decimals = Math.round((amount - Math.trunc(amount)) * 100);
    return decimals.toString().padStart(2, '0');
  }

  constructor(
    private readonly sessionService: SessionService,
    private readonly walletService: WalletService,
    private readonly authService: AuthService,
    private readonly router: Router,
    private readonly fb: FormBuilder
  ) {}

  ngOnInit(): void {
    this.user = this.sessionService.currentUser;
    this.loadBalance();
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

  openTransferModal(): void {
    this.showTransferModal = true;
    this.transferError = '';
    this.transferSuccess = '';
    this.transferForm.reset();
  }

  closeTransferModal(): void {
    this.showTransferModal = false;
  }

  onTransfer(): void {
    if (this.transferForm.invalid || this.isTransferring) {
      this.transferForm.markAllAsTouched();
      return;
    }

    this.isTransferring = true;
    this.transferError = '';
    this.transferSuccess = '';

    const { recipientEmail, amount, description } = this.transferForm.getRawValue();
    const amountAsNumber = parseFloat(amount);

    this.walletService.transferFunds(recipientEmail, amountAsNumber, description).subscribe({
      next: (response) => {
        this.isTransferring = false;
        if (response.success) {
          this.transferSuccess = `Transfer of $${amountAsNumber.toFixed(2)} to ${recipientEmail} completed successfully!`;
          this.loadBalance(); // Refresh balance
          setTimeout(() => this.closeTransferModal(), 2000);
        } else {
          this.transferError = response.errorMessage || 'Transfer failed. Please try again.';
        }
      },
      error: () => {
        this.isTransferring = false;
        this.transferError = 'Transfer failed. Please check your connection and try again.';
      }
    });
  }

  onDownloadActivity(): void {
    if (this.isDownloading) return;

    this.isDownloading = true;
    this.downloadError = '';

    // Download last 30 days of activity
    const endDate = new Date();
    const startDate = new Date();
    startDate.setDate(startDate.getDate() - 30);

    this.walletService.exportHistory(
      startDate.toISOString().split('T')[0],
      endDate.toISOString().split('T')[0]
    ).subscribe({
      next: (blob: Blob) => {
        this.isDownloading = false;
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `wallet-activity-${new Date().toISOString().split('T')[0]}.csv`;
        document.body.appendChild(a);
        a.click();
        window.URL.revokeObjectURL(url);
        document.body.removeChild(a);
      },
      error: () => {
        this.isDownloading = false;
        this.downloadError = 'Failed to download activity. Please try again.';
      }
    });
  }

  private loadBalance(): void {
    this.walletService.getBalance().subscribe({
      next: (balance) => {
        this.balance = balance;
        this.balanceError = '';
      },
      error: () => {
        this.balanceError = 'Unable to load wallet balance.';
      }
    });
  }
}