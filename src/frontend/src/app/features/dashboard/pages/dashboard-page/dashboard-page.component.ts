import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Observable } from 'rxjs';
import QRCode from 'qrcode';
import { SessionService } from '../../../../core/session/session.service';
import { UserAvatarComponent } from '../../../../core/components/user-avatar/user-avatar.component';
import { AuthService } from '../../../auth/data-access/auth.service';
import { SessionUser } from '../../../auth/models/auth.models';
import {
  WalletService,
  Wallet,
  CreateWalletRequest,
  ReceiveWalletQrResponse,
  ResolveWalletQrResponse,
  WalletTransferResponse
} from '../../../wallet-history/data-access/wallet.service';
import { WalletBalanceResponse, WalletHistoryEntry } from '../../../wallet-history/models/wallet.models';
import { SecurityService, SecurityLevel } from '../../../../core/security/security.service';
import { Notification, NotificationService } from '../../../../core/notifications/notification.service';
import { NotificationItemComponent } from '../../components/notification-item/notification-item.component';

/**
 * Dashboard statistics interface for real-time data display
 */
interface DashboardStats {
  pendingTransfers: number;
  monthlyInflow: number;
  securityLevel: SecurityLevel;
}

/**
 * Balance distribution item for wallet allocation display
 */
interface BalanceDistributionItem {
  walletId: string;
  walletName: string;
  walletType: string;
  balance: number;
  percentage: number;
  color: string;        // CSS class for legend dot
  colorValue: string;   // Actual hex/rgb for gradient
}

interface QrTransferRecipient {
  walletId: string;
  walletName: string;
  currency: string;
}

@Component({
  selector: 'app-dashboard-page',
  standalone: true,
  imports: [RouterLink, CommonModule, ReactiveFormsModule, UserAvatarComponent, NotificationItemComponent],
  templateUrl: './dashboard-page.component.html'
})
export class DashboardPageComponent implements OnInit {
  user: SessionUser | null = null;
  balance: WalletBalanceResponse | null = null;
  balanceError = '';
  readonly unreadNotificationCount$ = this.notificationService.getUnreadCount();
  readonly notifications$: Observable<Notification[]> = this.notificationService.getNotifications();
  showNotificationPopover = false;

  // Recent transactions
  recentTransactions: WalletHistoryEntry[] = [];
  recentTransactionsError = '';
  isLoadingRecentTransactions = false;

  // Dashboard statistics
  stats: DashboardStats | null = null;
  statsError = '';
  isLoadingStats = false;

  // Balance distribution
  balanceDistribution: BalanceDistributionItem[] = [];
  distributionError = '';
  isLoadingDistribution = false;

  // Search functionality
  searchQuery = '';
  isSearching = false;

  // Transfer form
  isTransferring = false;
  transferError = '';
  transferSuccess = '';
  showTransferModal = false;

  // Transfer between wallets form
  isTransferringBetweenWallets = false;
  walletTransferError = '';
  walletTransferSuccess = '';
  showWalletTransferModal = false;

  // Transfer all functionality
  isTransferringAll = false;
  transferAllError = '';
  transferAllSuccess = '';

  // QR receive workflow
  showReceiveQrModal = false;
  isLoadingReceiveQr = false;
  receiveQrError = '';
  receiveQrToken = '';
  receiveQrImageDataUrl = '';
  receiveQrWalletId = '';
  receiveQrWalletName = '';
  receiveQrCurrency = '';
  receiveQrExpiresAt = '';

  // QR transfer workflow
  showQrTransferModal = false;
  isResolvingQrTransfer = false;
  isSubmittingQrTransfer = false;
  qrTransferError = '';
  qrTransferSuccess = '';
  qrTransferTokenInput = '';
  qrTransferScannerError = '';
  qrTransferScannerStatus = '';
  qrTransferResolvedRecipient: QrTransferRecipient | null = null;
  private qrTransferScannerStream: MediaStream | null = null;
  private qrTransferScannerTimer: number | null = null;

  // Wallet management
  wallets: Wallet[] = [];
  isLoadingWallets = false;
  walletsError = '';
  selectedWallet: Wallet | null = null;

  // Wallet creation
  isCreatingWallet = false;
  walletCreationError = '';
  walletCreationSuccess = '';
  showWalletCreationModal = false;

  // Wallet deletion
  isDeletingWallet = false;
  walletDeletionError = '';
  walletToDelete: Wallet | null = null;
  showWalletDeletionModal = false;

  // Default wallet management
  isSettingDefaultWallet = false;
  defaultWalletError = '';

  readonly walletCreationForm = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(50)]],
    type: ['checking' as 'checking' | 'savings' | 'investment' | 'reserve', [Validators.required]],
    currency: ['USD', [Validators.required]],
    initialBalance: [0, [Validators.min(0)]]
  });

  readonly transferForm = this.fb.nonNullable.group({
    recipientEmail: ['', [Validators.required, Validators.email]],
    amount: ['', [Validators.required, Validators.min(0.01)]],
    description: ['']
  });

  readonly walletTransferForm = this.fb.nonNullable.group({
    sourceWallet: ['', [Validators.required]],
    targetWallet: ['', [Validators.required]],
    amount: ['', [Validators.required, Validators.min(0.01)]]
  });

  readonly qrTransferForm = this.fb.nonNullable.group({
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

  /**
   * Calculate trend percentage based on the last transaction
   * Returns the percentage change relative to current balance
   */
  get trendPercentage(): number {
    if (!this.recentTransactions.length || !this.balance?.balance) {
      return 0;
    }

    const lastTransaction = this.recentTransactions[0];
    const transactionAmount = Math.abs(lastTransaction.amount);
    const currentBalance = this.balance.balance;
    const oldBalance = lastTransaction.type.toLowerCase() === 'credit' ? currentBalance - transactionAmount : currentBalance + transactionAmount;

    if (oldBalance === 0) {
      return 0;
    }

    // Calculate percentage of the transaction relative to old balance
    const percentage = (transactionAmount / oldBalance) * 100;

    // If it's a credit (incoming), show positive; if debit (outgoing), show negative
    return lastTransaction.type.toLowerCase() === 'credit' ? percentage : -percentage;
  }

  /**
   * Get trend icon based on the last transaction type
   * Returns 'trending_up' for credits, 'trending_down' for debits
   */
  get trendIcon(): string {
    if (!this.recentTransactions.length) {
      return 'trending_up';
    }

    const lastTransaction = this.recentTransactions[0];
    return lastTransaction.type.toLowerCase() === 'credit' ? 'trending_up' : 'trending_down';
  }

  /**
   * Get trend color class based on the last transaction type
   * Returns positive color for credits, negative color for debits
   */
  get trendColorClass(): string {
    if (!this.recentTransactions.length) {
      return 'text-on-secondary-container';
    }

    const lastTransaction = this.recentTransactions[0];
    return lastTransaction.type.toLowerCase() === 'credit' 
      ? 'text-on-secondary-container' 
      : 'text-error';
  }

  constructor(
    private readonly sessionService: SessionService,
    private readonly walletService: WalletService,
    private readonly authService: AuthService,
    private readonly router: Router,
    private readonly fb: FormBuilder,
    private readonly securityService: SecurityService,
    private readonly notificationService: NotificationService
  ) {}

  ngOnInit(): void {
    this.user = this.sessionService.currentUser;
    this.loadWallets();
    this.loadRecentTransactions();
    this.loadStats();
    this.loadBalanceDistribution();
  }

  /**
   * Return a user-friendly description. Masks emails partially for privacy and
   * preserves original text when no email is present.
   */
  formatDescription(entry: WalletHistoryEntry): string {
    if (!entry.description) return 'Wallet operation';

    const emailRegex = /([a-zA-Z0-9._%+-]+)@([a-zA-Z0-9.-]+\.[a-zA-Z]{2,})/g;

    return entry.description.replace(emailRegex, (_match, local, domain) => {
      const keep = Math.ceil(local.length / 2);
      const visible = local.slice(0, keep);
      const masked = '*'.repeat(Math.max(3, local.length - keep));
      return `${visible}${masked}@${domain}`;
    });
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
          if (this.selectedWallet) {
            this.loadWalletBalance(this.selectedWallet.id); // Refresh default wallet balance
          }
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

  /**
   * Load recent transactions for dashboard display
   * Shows the 5 most recent transactions across all types
   */
  private loadRecentTransactions(): void {
    this.isLoadingRecentTransactions = true;
    this.recentTransactionsError = '';

    this.walletService.getHistory(5).subscribe({
      next: (response) => {
        this.recentTransactions = response.entries;
        this.recentTransactionsError = '';
        this.isLoadingRecentTransactions = false;
      },
      error: () => {
        this.recentTransactions = [];
        this.recentTransactionsError = 'Unable to load recent transactions.';
        this.isLoadingRecentTransactions = false;
      }
    });
  }

  /**
   * Load dashboard statistics from the wallet service
   * Includes pending transfers, monthly inflow, and security level
   */
  private loadStats(): void {
    this.isLoadingStats = true;
    this.statsError = '';

    // Calculate stats from recent transactions and balance
    this.walletService.getHistory(100).subscribe({
      next: (response) => {
        const now = new Date();
        const oneMonthAgo = new Date();
        oneMonthAgo.setMonth(now.getMonth() - 1);

        // Calculate monthly inflow (credits in the last 30 days)
        const monthlyInflow = response.entries
          .filter(entry => {
            const entryDate = new Date(entry.createdAt);
            return entryDate >= oneMonthAgo && entry.type.toLowerCase() === 'credit';
          })
          .reduce((sum, entry) => sum + entry.amount, 0);

        // Calculate pending transfers (debits with 'pending' status)
        const pendingTransfers = response.entries
          .filter(entry => entry.type.toLowerCase() === 'debit' && entry.description?.toLowerCase().includes('pending'))
          .reduce((sum, entry) => sum + Math.abs(entry.amount), 0);

        // Get security level from security service
        this.securityService.getSecurityLevel().subscribe({
          next: (securityAssessment) => {
            this.stats = {
              pendingTransfers,
              monthlyInflow,
              securityLevel: securityAssessment.level
            };

            this.statsError = '';
            this.isLoadingStats = false;
          },
          error: () => {
            // Fallback to medium security if service fails
            this.stats = {
              pendingTransfers,
              monthlyInflow,
              securityLevel: SecurityLevel.MEDIUM
            };

            this.statsError = '';
            this.isLoadingStats = false;
          }
        });
      },
      error: () => {
        this.stats = null;
        this.statsError = 'Unable to load statistics.';
        this.isLoadingStats = false;
      }
    });
  }

  /**
   * Load balance distribution data
   * Shows how funds are allocated across different wallet types
   */
  private loadBalanceDistribution(): void {
    this.isLoadingDistribution = true;
    this.distributionError = '';

    // Calculate distribution from actual wallets
    this.walletService.getWallets().subscribe({
      next: (wallets) => {
        const activeWallets = wallets.filter(w => w.isActive && w.balance > 0);

        if (activeWallets.length === 0) {
          this.balanceDistribution = [];
          this.distributionError = '';
          this.isLoadingDistribution = false;
          return;
        }

        const totalBalance = activeWallets.reduce((sum, wallet) => sum + wallet.balance, 0);

        // Color palette: class names and their corresponding actual color values
        // Values must match Tailwind config to ensure legend and pie chart colors are consistent
        const colorPalette = [
          { class: 'bg-primary', value: '#003d9b' },      // Blue
          { class: 'bg-secondary', value: '#525f73' },    // Gray
          { class: 'bg-tertiary', value: '#7b2600' },     // Brown
          { class: 'bg-error', value: '#ba1a1a' },        // Red
          { class: 'bg-surface-tint', value: '#0c56d0' }  // Light Blue
        ];

        this.balanceDistribution = activeWallets.map((wallet, index) => {
          const palette = colorPalette[index % colorPalette.length];
          return {
            walletId: wallet.id,
            walletName: wallet.name,
            walletType: wallet.type,
            balance: wallet.balance,
            percentage: totalBalance > 0 ? (wallet.balance / totalBalance) * 100 : 0,
            color: palette.class,
            colorValue: palette.value
          };
        });

        this.distributionError = '';
        this.isLoadingDistribution = false;
      },
      error: () => {
        this.balanceDistribution = [];
        this.distributionError = 'Unable to load balance distribution.';
        this.isLoadingDistribution = false;
      }
    });
  }

  /**
   * Dynamically builds the conic-gradient string for the donut chart
   */
  get pieChartGradient(): string {
    if (!this.balanceDistribution.length) return '';

    // Filter out zero-percentage items to avoid empty slices
    const nonZeroItems = this.balanceDistribution.filter(item => item.percentage > 0);
    if (nonZeroItems.length === 0) return 'conic-gradient(from 0deg, #e0e0e0 0%, #e0e0e0 100%)';

    let gradient = 'conic-gradient(from 0deg, ';
    let startPercent = 0;

    for (const item of nonZeroItems) {
      const endPercent = startPercent + item.percentage;
      gradient += `${item.colorValue} ${startPercent}%, ${item.colorValue} ${endPercent}%, `;
      startPercent = endPercent;
    }

    // Remove trailing comma and space, then close
    gradient = gradient.slice(0, -2) + ')';
    return gradient;
  }

  /**
   * Handle search input for transactions
   * Filters the displayed transactions based on search query
   */
  onSearch(query: string): void {
    this.searchQuery = query.trim();
    this.isSearching = this.searchQuery.length > 0;
  }

  toggleNotificationsPopover(): void {
    this.showNotificationPopover = !this.showNotificationPopover;
  }

  closeNotificationsPopover(): void {
    this.showNotificationPopover = false;
  }

  onNotificationClick(notification: Notification): void {
    if (!notification.isRead) {
      this.notificationService.markAsRead(notification.id).subscribe();
    }

    this.showNotificationPopover = false;
    this.router.navigate(['/wallet-history']);
  }

  trackByNotificationId(_: number, notification: Notification): string {
    return notification.id;
  }

  get filteredRecentTransactions(): WalletHistoryEntry[] {
    if (!this.searchQuery) {
      return this.recentTransactions;
    }

    const query = this.searchQuery.toLowerCase();
    return this.recentTransactions.filter(entry => this.getSearchableTransactionText(entry).includes(query));
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
    // Navigate to settings page
    /* this.router.navigate(['/settings']) */
    this.router.navigate(['/profile']);
  }

  /**
   * Open transfer between wallets modal
   * Shows form for internal wallet transfers
   */
  openWalletTransferModal(): void {
    this.showWalletTransferModal = true;
    this.walletTransferError = '';
    this.walletTransferSuccess = '';
    this.walletTransferForm.reset();
  }

  /**
   * Close transfer between wallets modal
   */
  closeWalletTransferModal(): void {
    this.showWalletTransferModal = false;
    this.transferAllSuccess = '';
    this.transferAllError = '';
  }

  openReceiveQrModal(): void {
    const receiveWallet = this.selectedWallet ?? this.wallets.find(wallet => wallet.isDefault && wallet.isActive) ?? this.wallets.find(wallet => wallet.isActive) ?? null;

    this.showReceiveQrModal = true;
    this.receiveQrError = '';
    this.receiveQrToken = '';
    this.receiveQrImageDataUrl = '';
    this.receiveQrWalletId = receiveWallet?.id ?? '';
    this.receiveQrWalletName = receiveWallet?.name ?? '';
    this.receiveQrCurrency = receiveWallet?.currency ?? 'USD';
    this.receiveQrExpiresAt = '';

    if (!receiveWallet) {
      this.receiveQrError = 'Create or activate a wallet before generating a receive QR code.';
      return;
    }

    this.loadReceiveQr(receiveWallet.id);
  }

  closeReceiveQrModal(): void {
    this.showReceiveQrModal = false;
    this.receiveQrError = '';
    this.receiveQrToken = '';
    this.receiveQrImageDataUrl = '';
    this.receiveQrWalletId = '';
    this.receiveQrWalletName = '';
    this.receiveQrCurrency = '';
    this.receiveQrExpiresAt = '';
    this.isLoadingReceiveQr = false;
  }

  onReceiveWalletChange(walletId: string): void {
    this.receiveQrWalletId = walletId;
    if (walletId) {
      this.loadReceiveQr(walletId);
    }
  }

  openQrTransferModal(): void {
    this.showQrTransferModal = true;
    this.qrTransferError = '';
    this.qrTransferSuccess = '';
    this.qrTransferScannerError = '';
    this.qrTransferScannerStatus = '';
    this.qrTransferTokenInput = '';
    this.qrTransferResolvedRecipient = null;
    this.qrTransferForm.reset();

    setTimeout(() => {
      void this.startQrScanner();
    });
  }

  closeQrTransferModal(): void {
    this.stopQrScanner();
    this.showQrTransferModal = false;
    this.qrTransferError = '';
    this.qrTransferSuccess = '';
    this.qrTransferScannerError = '';
    this.qrTransferScannerStatus = '';
    this.qrTransferTokenInput = '';
    this.qrTransferResolvedRecipient = null;
    this.isResolvingQrTransfer = false;
    this.isSubmittingQrTransfer = false;
    this.qrTransferForm.reset();
  }

  async loadReceiveQr(walletId: string): Promise<void> {
    if (!walletId) {
      this.receiveQrError = 'Select a wallet first.';
      return;
    }

    this.isLoadingReceiveQr = true;
    this.receiveQrError = '';

    this.walletService.getReceiveQr(walletId).subscribe({
      next: (response: ReceiveWalletQrResponse) => {
        this.isLoadingReceiveQr = false;

        if (!response.success || !response.token) {
          this.receiveQrError = response.errorMessage || 'Unable to generate a receive QR code.';
          return;
        }

        this.receiveQrToken = response.token;
        this.receiveQrWalletId = response.walletId || walletId;
        this.receiveQrWalletName = response.walletName || this.receiveQrWalletName;
        this.receiveQrCurrency = response.currency || this.receiveQrCurrency;
        this.receiveQrExpiresAt = response.expiresAt ? new Date(response.expiresAt).toLocaleString() : '';

        void this.renderReceiveQrImage(response.token);
      },
      error: () => {
        this.isLoadingReceiveQr = false;
        this.receiveQrError = 'Unable to generate a receive QR code. Please try again.';
      }
    });
  }

  async renderReceiveQrImage(token: string): Promise<void> {
    try {
      this.receiveQrImageDataUrl = await QRCode.toDataURL(token, {
        width: 320,
        margin: 1,
        errorCorrectionLevel: 'M'
      });
    } catch {
      this.receiveQrError = 'Unable to render the QR code image.';
    }
  }

  copyReceiveQrToken(): void {
    if (!this.receiveQrToken || !navigator.clipboard) {
      return;
    }

    navigator.clipboard.writeText(this.receiveQrToken).catch(() => {
      this.receiveQrError = 'Unable to copy the QR token.';
    });
  }

  async startQrScanner(): Promise<void> {
    if (!this.showQrTransferModal || this.qrTransferResolvedRecipient) {
      return;
    }

    this.qrTransferScannerError = '';
    this.qrTransferScannerStatus = '';

    const barcodeDetectorFactory = (window as Window & {
      BarcodeDetector?: new (options: { formats: string[] }) => {
        detect(source: HTMLVideoElement): Promise<Array<{ rawValue?: string }>>;
      };
    }).BarcodeDetector;

    if (!barcodeDetectorFactory) {
      this.qrTransferScannerError = 'This browser does not support camera-based QR scanning. Paste the token below instead.';
      return;
    }

    const videoElement = document.getElementById('qr-transfer-video') as HTMLVideoElement | null;
    if (!videoElement) {
      this.qrTransferScannerError = 'QR scanner is not ready yet.';
      return;
    }

    try {
      this.qrTransferScannerStream = await navigator.mediaDevices.getUserMedia({
        video: { facingMode: 'environment' },
        audio: false
      });
      videoElement.srcObject = this.qrTransferScannerStream;
      await videoElement.play();

      const detector = new barcodeDetectorFactory({ formats: ['qr_code'] });
      this.qrTransferScannerStatus = 'Scanning for a QR code...';
      this.qrTransferScannerTimer = window.setInterval(async () => {
        if (!this.showQrTransferModal || this.qrTransferResolvedRecipient) {
          return;
        }

        try {
          const codes = await detector.detect(videoElement);
          const token = codes.find(code => code.rawValue)?.rawValue;
          if (token) {
            this.resolveQrTransferToken(token);
          }
        } catch {
          this.qrTransferScannerError = 'Unable to read the QR code from the camera.';
        }
      }, 700);
    } catch {
      this.qrTransferScannerError = 'Unable to access the camera. You can paste the QR token instead.';
    }
  }

  stopQrScanner(): void {
    if (this.qrTransferScannerTimer !== null) {
      window.clearInterval(this.qrTransferScannerTimer);
      this.qrTransferScannerTimer = null;
    }

    if (this.qrTransferScannerStream) {
      this.qrTransferScannerStream.getTracks().forEach(track => track.stop());
      this.qrTransferScannerStream = null;
    }

    const videoElement = document.getElementById('qr-transfer-video') as HTMLVideoElement | null;
    if (videoElement) {
      videoElement.srcObject = null;
    }

    this.qrTransferScannerStatus = '';
  }

  resolveQrTransferToken(token?: string): void {
    const qrToken = (token || this.qrTransferTokenInput).trim();
    if (!qrToken || this.isResolvingQrTransfer) {
      return;
    }

    this.isResolvingQrTransfer = true;
    this.qrTransferError = '';
    this.qrTransferScannerError = '';
    this.qrTransferScannerStatus = 'Resolving QR token...';

    this.walletService.resolveReceiveQr(qrToken).subscribe({
      next: (response: ResolveWalletQrResponse) => {
        this.isResolvingQrTransfer = false;

        if (!response.success || !response.walletId || !response.walletName || !response.currency) {
          this.qrTransferError = response.errorMessage || 'Unable to resolve the scanned QR code.';
          this.qrTransferResolvedRecipient = null;
          return;
        }

        this.stopQrScanner();
        this.qrTransferResolvedRecipient = {
          walletId: response.walletId,
          walletName: response.walletName,
          currency: response.currency
        };
        this.qrTransferScannerStatus = `Ready to send to ${response.walletName}.`;
        this.qrTransferTokenInput = qrToken;
      },
      error: () => {
        this.isResolvingQrTransfer = false;
        this.qrTransferError = 'Unable to resolve the scanned QR code. Please try again.';
        this.qrTransferScannerStatus = '';
      }
    });
  }

  submitQrTransfer(): void {
    if (this.qrTransferForm.invalid || this.isSubmittingQrTransfer || !this.qrTransferResolvedRecipient) {
      this.qrTransferForm.markAllAsTouched();
      return;
    }

    this.isSubmittingQrTransfer = true;
    this.qrTransferError = '';
    this.qrTransferSuccess = '';

    const { amount, description } = this.qrTransferForm.getRawValue();
    const amountAsNumber = parseFloat(amount);

    this.walletService.transferToWallet(this.qrTransferResolvedRecipient.walletId, amountAsNumber, description).subscribe({
      next: (response: WalletTransferResponse) => {
        this.isSubmittingQrTransfer = false;

        if (response.success) {
          this.qrTransferSuccess = `Transfer of $${amountAsNumber.toFixed(2)} to ${this.qrTransferResolvedRecipient?.walletName || 'the scanned wallet'} completed successfully.`;
          this.loadWallets();
          this.loadBalanceDistribution();
          if (this.selectedWallet) {
            this.loadWalletBalance(this.selectedWallet.id);
          }
          setTimeout(() => this.closeQrTransferModal(), 2000);
          return;
        }

        this.qrTransferError = response.errorMessage || 'QR transfer failed. Please try again.';
      },
      error: (error: HttpErrorResponse) => {
        this.isSubmittingQrTransfer = false;
        this.qrTransferError = error?.error?.message || 'QR transfer failed. Please check your connection and try again.';
      }
    });
  }

  /**
   * Handle transfer between wallets form submission
   * Transfers funds between user's own wallets using real wallet IDs
   */
  onWalletTransfer(): void {
    if (this.walletTransferForm.invalid || this.isTransferringBetweenWallets) {
      this.walletTransferForm.markAllAsTouched();
      return;
    }

    this.isTransferringBetweenWallets = true;
    this.walletTransferError = '';
    this.walletTransferSuccess = '';

    const { sourceWallet, targetWallet, amount } = this.walletTransferForm.getRawValue();
    const amountAsNumber = parseFloat(amount);

    // Validate source and target are different
    if (sourceWallet === targetWallet) {
      this.isTransferringBetweenWallets = false;
      this.walletTransferError = 'Source and target wallets cannot be the same.';
      return;
    }

    // Call wallet transfer API with real wallet IDs
    this.walletService.transferBetweenWallets(sourceWallet, targetWallet, amountAsNumber).subscribe({
      next: (response) => {
        this.isTransferringBetweenWallets = false;
        if (response.success) {
          const sourceWalletObj = this.wallets.find(w => w.id === sourceWallet);
          const targetWalletObj = this.wallets.find(w => w.id === targetWallet);
          this.walletTransferSuccess = `Transfer of $${amountAsNumber.toFixed(2)} from ${sourceWalletObj?.name || sourceWallet} to ${targetWalletObj?.name || targetWallet} completed successfully!`;
          if (this.selectedWallet) {
            this.loadWalletBalance(this.selectedWallet.id); // Refresh default wallet balance
          }
          this.loadWallets(); // Refresh wallet balances
          this.loadBalanceDistribution(); // Refresh balance distribution
          setTimeout(() => this.closeWalletTransferModal(), 2000);
        } else {
          this.walletTransferError = response.errorMessage || 'Wallet transfer failed. Please try again.';
        }
      },
      error: (error: HttpErrorResponse) => {
        this.isTransferringBetweenWallets = false;
        this.walletTransferError =
          error?.error?.errorMessage ||
          error?.error?.message ||
          'Wallet transfer failed. Please check your connection and try again.';
      }
    });
  }

  /**
   * Handle transfer all funds from source wallet to target wallet
   * Transfers entire balance from source wallet to target wallet
   */
  onTransferAll(): void {
    if (this.isTransferringAll) {
      return;
    }

    const sourceWallet = this.walletTransferForm.get('sourceWallet')?.value;
    const targetWallet = this.walletTransferForm.get('targetWallet')?.value;

    // Validate source and target wallets are selected and different
    if (!sourceWallet || !targetWallet || sourceWallet === targetWallet) {
      this.walletTransferForm.markAllAsTouched();
      return;
    }

    // Find source wallet and get its balance
    const sourceWalletObj = this.wallets.find(w => w.id === sourceWallet);
    if (!sourceWalletObj) {
      this.transferAllError = 'Source wallet not found.';
      return;
    }

    // Check if source wallet has funds to transfer
    if (sourceWalletObj.balance <= 0) {
      this.transferAllError = 'Source wallet has no funds to transfer.';
      return;
    }

    this.isTransferringAll = true;
    this.transferAllError = '';
    this.transferAllSuccess = '';

    const amountToTransfer = sourceWalletObj.balance;

    // Call wallet transfer API with entire balance
    this.walletService.transferBetweenWallets(sourceWallet, targetWallet, amountToTransfer).subscribe({
      next: (response) => {
        this.isTransferringAll = false;
        if (response.success) {
          const targetWalletObj = this.wallets.find(w => w.id === targetWallet);
          this.transferAllSuccess = `Successfully transferred all funds ($${amountToTransfer.toFixed(2)}) from ${sourceWalletObj.name} to ${targetWalletObj?.name || targetWallet}!`;
          if (this.selectedWallet) {
            this.loadWalletBalance(this.selectedWallet.id); // Refresh default wallet balance
          }
          this.loadWallets(); // Refresh wallet balances
          this.loadBalanceDistribution(); // Refresh balance distribution
          setTimeout(() => this.closeWalletTransferModal(), 2000);
        } else {
          this.transferAllError = response.errorMessage || 'Transfer all failed. Please try again.';
        }
      },
      error: (error: HttpErrorResponse) => {
        this.isTransferringAll = false;
        this.transferAllError =
          error?.error?.errorMessage ||
          error?.error?.message ||
          'Transfer all failed. Please check your connection and try again.';
      }
    });
  }

  /**
   * Format transaction amount for display
   * Shows appropriate formatting based on transaction type
   */
  formatTransactionAmount(entry: WalletHistoryEntry): string {
    const amount = Math.abs(entry.amount).toLocaleString('en-US', {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    });

    const isCredit = entry.type.toLowerCase() === 'credit';
    return `${isCredit ? '+' : '-'}$${amount}`;
  }

  /**
   * Get transaction status for display
   * Returns appropriate status text and styling class based on transaction data
   */
  getTransactionStatus(entry: WalletHistoryEntry): { text: string; class: string } {
    // Check transaction description for status indicators
    const description = entry.description?.toLowerCase() || '';

    if (description.includes('pending') || description.includes('processing')) {
      return {
        text: 'Pending',
        class: 'text-warning'
      };
    } else if (description.includes('failed') || description.includes('rejected')) {
      return {
        text: 'Failed',
        class: 'text-danger'
      };
    } else if (description.includes('cancelled')) {
      return {
        text: 'Cancelled',
        class: 'text-muted'
      };
    } else {
      // Default to confirmed for completed transactions
      return {
        text: 'Confirmed',
        class: entry.type.toLowerCase() === 'credit' ? 'text-success' : 'text-on-surface'
      };
    }
  }

  /**
   * Get transaction type display text
   * Returns human-readable transaction type
   */
  getTransactionTypeText(entry: WalletHistoryEntry): string {
    const description = entry.description?.toLowerCase() || '';

    if (description.includes('transfer')) {
      return 'Transfer';
    } else if (description.includes('deposit')) {
      return 'Deposit';
    } else if (description.includes('withdrawal')) {
      return 'Withdrawal';
    } else if (entry.type.toLowerCase() === 'credit') {
      return 'Credit';
    } else {
      return 'Debit';
    }
  }

  private getSearchableTransactionText(entry: WalletHistoryEntry): string {
    const parts = [
      entry.description,
      entry.type,
      entry.amount.toFixed(2),
      entry.createdAt,
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

  /**
   * Load all wallets for the current user
   * Fetches wallet list from service and sets default wallet selection
   */
  private loadWallets(): void {
    this.isLoadingWallets = true;
    this.walletsError = '';

    this.walletService.getWallets().subscribe({
      next: (wallets) => {
        this.wallets = wallets;
        // Set default wallet if available
        this.selectedWallet = wallets.find(w => w.isDefault) || wallets[0] || null;
        this.walletsError = '';
        this.isLoadingWallets = false;
        // Load balance for the default wallet
        if (this.selectedWallet) {
          this.loadWalletBalance(this.selectedWallet.id);
        }
        // Refresh balance distribution when wallets are loaded
        this.loadBalanceDistribution();
      },
      error: () => {
        this.wallets = [];
        this.walletsError = 'Unable to load wallets.';
        this.isLoadingWallets = false;
      }
    });
  }

  /**
   * Open wallet creation modal
   * Resets form and clears previous errors
   */
  openWalletCreationModal(): void {
    this.showWalletCreationModal = true;
    this.walletCreationError = '';
    this.walletCreationSuccess = '';
    this.walletCreationForm.reset({
      name: '',
      type: 'checking',
      currency: 'USD',
      initialBalance: 0
    });
  }

  /**
   * Close wallet creation modal
   */
  closeWalletCreationModal(): void {
    this.showWalletCreationModal = false;
  }

  /**
   * Handle wallet creation form submission
   * Creates new wallet via service and refreshes wallet list
   */
  onCreateWallet(): void {
    if (this.walletCreationForm.invalid || this.isCreatingWallet) {
      this.walletCreationForm.markAllAsTouched();
      return;
    }

    this.isCreatingWallet = true;
    this.walletCreationError = '';
    this.walletCreationSuccess = '';

    const { name, type, currency, initialBalance } = this.walletCreationForm.getRawValue();

    const request: CreateWalletRequest = {
      name,
      type,
      currency,
      initialBalance: initialBalance > 0 ? initialBalance : undefined
    };

    this.walletService.createWallet(request).subscribe({
      next: (response) => {
        this.isCreatingWallet = false;
        if (response.success && response.wallet) {
          this.walletCreationSuccess = `Wallet "${response.wallet.name}" created successfully!`;
          this.loadWallets(); // Refresh wallet list
          this.loadBalanceDistribution(); // Refresh balance distribution
          setTimeout(() => this.closeWalletCreationModal(), 2000);
        } else {
          this.walletCreationError = response.errorMessage || 'Wallet creation failed. Please try again.';
        }
      },
      error: () => {
        this.isCreatingWallet = false;
        this.walletCreationError = 'Wallet creation failed. Please check your connection and try again.';
      }
    });
  }

  /**
   * Open wallet deletion confirmation modal
   * @param wallet - Wallet to delete
   */
  openWalletDeletionModal(wallet: Wallet): void {
    this.walletToDelete = wallet;
    this.showWalletDeletionModal = true;
    this.walletDeletionError = '';
  }

  /**
   * Close wallet deletion modal
   */
  closeWalletDeletionModal(): void {
    this.showWalletDeletionModal = false;
    this.walletToDelete = null;
  }

  /**
   * Handle wallet deletion confirmation
   * Deletes wallet via service and refreshes wallet list
   */
  onDeleteWallet(): void {
    if (!this.walletToDelete || this.isDeletingWallet) {
      return;
    }

    this.isDeletingWallet = true;
    this.walletDeletionError = '';

    // Save wallet name before deletion for success notification
    const walletName = this.walletToDelete.name;

    this.walletService.deleteWallet(this.walletToDelete.id).subscribe({
      next: () => {
        this.isDeletingWallet = false;
        this.loadWallets(); // Refresh wallet list
        this.loadBalanceDistribution(); // Refresh balance distribution
        this.closeWalletDeletionModal();
        // Show success notification with saved wallet name
        this.notificationService.showSuccess(`Wallet "${walletName}" deleted successfully.`);
      },
      error: (error) => {
        this.isDeletingWallet = false;
        // Display specific error message from backend
        this.walletDeletionError = error.message || 'Failed to delete wallet. Please try again.';
      }
    });
  }

  /**
   * Set a wallet as the default wallet
   * @param wallet - Wallet to set as default
   */
  onSetDefaultWallet(wallet: Wallet): void {
    if (this.isSettingDefaultWallet || wallet.isDefault) {
      return;
    }

    this.isSettingDefaultWallet = true;
    this.defaultWalletError = '';

    this.walletService.setDefaultWallet(wallet.id).subscribe({
      next: () => {
        this.isSettingDefaultWallet = false;
        this.loadWallets(); // Refresh wallet list to update default status
        this.notificationService.showSuccess(`"${wallet.name}" is now your default wallet.`);
      },
      error: () => {
        this.isSettingDefaultWallet = false;
        this.defaultWalletError = 'Failed to set default wallet. Please try again.';
      }
    });
  }

  /**
   * Select a wallet for operations
   * @param wallet - Wallet to select
   */
  selectWallet(wallet: Wallet): void {
    this.selectedWallet = wallet;
    // Load balance for selected wallet
    this.loadWalletBalance(wallet.id);
  }

  /**
   * Load balance for a specific wallet
   * @param walletId - ID of wallet to load balance for
   */
  private loadWalletBalance(walletId: string): void {
    this.walletService.getWalletBalance(walletId).subscribe({
      next: (balance) => {
        this.balance = balance;
        this.balanceError = '';
      },
      error: () => {
        this.balanceError = 'Unable to load wallet balance.';
      }
    });
  }

  /**
   * Get wallet type display text
   * @param type - Wallet type
   * @returns Human-readable wallet type
   */
  getWalletTypeText(type: string): string {
    const typeMap: Record<string, string> = {
      'checking': 'Checking',
      'savings': 'Savings',
      'investment': 'Investment',
      'reserve': 'Reserve'
    };
    return typeMap[type] || type;
  }

  /**
   * Get wallet type icon
   * @param type - Wallet type
   * @returns Material icon name for wallet type
   */
  getWalletTypeIcon(type: string): string {
    const iconMap: Record<string, string> = {
      'checking': 'account_balance',
      'savings': 'savings',
      'investment': 'trending_up',
      'reserve': 'shield'
    };
    return iconMap[type] || 'account_balance_wallet';
  }

  /**
   * Check if wallet can be deleted
   * Prevents deletion of default wallet or wallet with non-zero balance
   * @param wallet - Wallet to check
   * @returns True if wallet can be deleted
   */
  canDeleteWallet(wallet: Wallet): boolean {
    // Cannot delete default wallet
    if (wallet.isDefault) {
      return false;
    }

    // Cannot delete wallet with non-zero balance
    if (wallet.balance !== 0) {
      return false;
    }

    // Cannot delete if it's the only wallet
    if (this.wallets.length <= 1) {
      return false;
    }

    return true;
  }

  /**
   * Check if wallet can be set as default
   * @param wallet - Wallet to check
   * @returns True if wallet can be set as default
   */
  canSetDefaultWallet(wallet: Wallet): boolean {
    // Cannot set inactive wallet as default
    if (!wallet.isActive) {
      return false;
    }

    // Already default
    if (wallet.isDefault) {
      return false;
    }

    return true;
  }

  /**
   * Get available wallets for transfer source selection
   * @returns Array of wallets that can be used as transfer source
   */
  getSourceWallets(): Wallet[] {
    return this.wallets.filter(wallet => wallet.isActive && wallet.balance > 0);
  }

  /**
   * Get available wallets for transfer target selection
   * @returns Array of wallets that can be used as transfer target
   */
  getTargetWallets(): Wallet[] {
    return this.wallets.filter(wallet => wallet.isActive);
  }

  /**
   * Format transaction date for display
   * Returns human-readable date format
   */
  formatTransactionDate(entry: WalletHistoryEntry): string {
    const date = new Date(entry.createdAt);
    const now = new Date();
    const diffTime = Math.abs(now.getTime() - date.getTime());
    const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));

    if (diffDays === 0) {
      return 'Today';
    } else if (diffDays === 1) {
      return 'Yesterday';
    } else if (diffDays < 7) {
      return `${diffDays} days ago`;
    } else {
      return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
    }
  }

  /**
   * Check if Transfer All button should be enabled
   * Requires valid source and target wallet selections that are different
   */
  get canTransferAll(): boolean {
    const sourceWallet = this.walletTransferForm.get('sourceWallet')?.value;
    const targetWallet = this.walletTransferForm.get('targetWallet')?.value;
    const sourceValid = this.walletTransferForm.get('sourceWallet')?.valid ?? false;
    const targetValid = this.walletTransferForm.get('targetWallet')?.valid ?? false;

    // Both wallets must be selected and valid, and they must be different
    return Boolean(sourceValid && targetValid && sourceWallet && targetWallet && sourceWallet !== targetWallet);
  }

  /**
   * Get the balance of the selected source wallet
   * @returns Source wallet balance or 0 if not selected
   */
  getSourceWalletBalance(): number {
    const sourceWalletId = this.walletTransferForm.get('sourceWallet')?.value;
    if (!sourceWalletId) return 0;

    const sourceWallet = this.wallets.find(w => w.id === sourceWalletId);
    return sourceWallet?.balance || 0;
  }

  /**
   * Get the name of the selected source wallet
   * @returns Source wallet name or empty string if not selected
   */
  getSourceWalletName(): string {
    const sourceWalletId = this.walletTransferForm.get('sourceWallet')?.value;
    if (!sourceWalletId) return '';

    const sourceWallet = this.wallets.find(w => w.id === sourceWalletId);
    return sourceWallet?.name || '';
  }

  /**
   * Get total balance across all wallets
   * @returns Total balance
   */
  getTotalBalance(): number {
    return this.balanceDistribution.reduce((sum, item) => sum + item.balance, 0);
  }
}