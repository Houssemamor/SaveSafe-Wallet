import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { SessionService } from '../../../../core/session/session.service';
import { Subscription } from 'rxjs';
import { AuthService } from '../../../auth/data-access/auth.service';
import { NotificationService } from '../../../../core/notifications/notification.service';
import { SessionUser } from '../../../auth/models/auth.models';
import { AdminService } from '../../data-access/admin.service';
import {
  AdminFailedLoginByIp,
  AdminLokiQueryResponse,
  AdminLokiSeries,
  AdminLoginEvent,
  AdminSecuritySummary,
  AdminUser
} from '../../models/admin.models';

interface SourceSignal {
  name: string;
  icon: string;
  role: string;
  value: string;
  detail: string;
  status: 'Online' | 'Internal' | 'Watch' | 'Linked';
  href?: string;
}

@Component({
  selector: 'app-admin-dashboard-page',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule, ReactiveFormsModule],
  templateUrl: './admin-dashboard-page.component.html',
  styles: [`
    :host {
      display: block;
      background: #f8f9fa;
    }

    .admin-enter {
      animation: adminFadeUp 520ms ease both;
    }

    .admin-enter-delay {
      animation: adminFadeUp 640ms ease 80ms both;
    }

    .metric-card {
      transition: transform 180ms ease, box-shadow 180ms ease, border-color 180ms ease;
    }

    .metric-card:hover {
      transform: translateY(-2px);
      box-shadow: 0 18px 40px rgba(25, 28, 29, 0.08);
      border-color: rgba(0, 82, 204, 0.24);
    }

    .pulse-dot {
      animation: adminPulse 1.9s ease-in-out infinite;
    }

    @keyframes adminFadeUp {
      from {
        opacity: 0;
        transform: translateY(14px);
      }
      to {
        opacity: 1;
        transform: translateY(0);
      }
    }

    @keyframes adminPulse {
      0%, 100% {
        transform: scale(1);
        opacity: 0.72;
      }
      50% {
        transform: scale(1.35);
        opacity: 1;
      }
    }
  `]
})
export class AdminDashboardPageComponent implements OnInit, OnDestroy {
  user: SessionUser | null = null;
  summary: AdminSecuritySummary | null = null;
  failedLogins: AdminFailedLoginByIp[] = [];
  loginEvents: AdminLoginEvent[] = [];
  users: AdminUser[] = [];

  isLoading = true;
  isRefreshing = false;
  isObservabilityLoading = false;
  errorMessage = '';
  observabilityErrorMessage = '';
  observabilityQuery = 'sum by (service) (count_over_time({service=~".+"}[5m]))';
  observabilityHours = 1;
  observabilityResult: AdminLokiQueryResponse | null = null;
  selectedUserForContact: AdminUser | null = null;
  selectedUserForPassword: AdminUser | null = null;
  adminActionMessage = '';
  isResettingPassword = false;
  readonly generatedAt = new Date();
  readonly observabilityPalette = [
    '#0052cc',
    '#a33500',
    '#047857',
    '#b45309',
    '#7c3aed',
    '#be123c',
    '#0f766e',
    '#4338ca'
  ];

  readonly contactForm = this.fb.nonNullable.group({
    subject: ['Account support message', [Validators.required, Validators.maxLength(120)]],
    message: ['', [Validators.required, Validators.minLength(8), Validators.maxLength(500)]]
  });

  readonly passwordResetForm = this.fb.nonNullable.group({
    newPassword: ['', [Validators.required, Validators.minLength(8)]],
    confirmPassword: ['', [Validators.required]]
  });

  constructor(
    private readonly adminService: AdminService,
    private readonly sessionService: SessionService,
    private readonly authService: AuthService,
    private readonly notificationService: NotificationService,
    private readonly fb: FormBuilder,
    private readonly router: Router
  ) {}

  ngOnInit(): void {
    this.user = this.sessionService.currentUser;
    this.loadDashboard();
    this.runObservabilityQuery();
    this.ensureRefreshOnLogin();
  }

  ngOnDestroy(): void {
    this.currentUserSub?.unsubscribe();
  }

  private currentUserSub: Subscription | null = null;

  // Subscribe to session changes so we reload the dashboard when an admin logs in
  // This ensures the admin view is refreshed immediately after login navigation.
  private ensureRefreshOnLogin(): void {
    if (this.currentUserSub) {
      return;
    }

    this.currentUserSub = this.sessionService.currentUser$.subscribe((u) => {
      if (!u) {
        return;
      }

      const role = u.role?.toLowerCase();
      if (role === 'admin') {
        // If the component is already visible, reload data.
        this.loadDashboard();
      }
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

  formatDate(value: string | null): string {
    if (!value) {
      return 'N/A';
    }

    return new Date(value).toLocaleString();
  }

  get sourceSignals(): SourceSignal[] {
    const riskLevel = this.summary?.aiRiskLevel ?? 'Pending';
    const riskScore = this.summary ? `${this.summary.aiRiskScore}/100` : 'N/A';
    const eventCount = this.summary?.totalLoginEventsLast24Hours ?? 0;

    return [
      {
        name: 'Grafana',
        icon: 'monitoring',
        role: 'Dashboards',
        value: '3000',
        detail: 'Open observability workspace',
        status: 'Linked',
        href: 'http://localhost:3000'
      },
      {
        name: 'Loki',
        icon: 'article',
        role: 'Log aggregation',
        value: '3100',
        detail: 'Auth logs shipped through Serilog',
        status: 'Linked',
        href: 'http://localhost:3100/ready'
      },
      {
        name: 'Kafka',
        icon: 'hub',
        role: 'Event bus',
        value: `${eventCount}`,
        detail: 'Login events stream',
        status: 'Internal'
      },
      {
        name: 'Zookeeper',
        icon: 'account_tree',
        role: 'Kafka coordinator',
        value: '2181',
        detail: 'Internal dependency for broker health',
        status: 'Internal'
      },
      {
        name: 'AI Service',
        icon: 'network_intelligence',
        role: 'Risk scoring',
        value: riskScore,
        detail: `Current risk level: ${riskLevel}`,
        status: this.summary?.aiRiskLevel === 'High' ? 'Watch' : 'Online',
        href: 'http://localhost:5010'
      },
      {
        name: 'Auth API',
        icon: 'verified_user',
        role: 'Identity service',
        value: '8080',
        detail: 'Admin, MFA and login telemetry',
        status: 'Online',
        href: '/health'
      }
    ];
  }

  get successRate(): number {
    if (!this.summary || this.summary.totalLoginEventsLast24Hours <= 0) {
      return 100;
    }

    const successful = this.summary.totalLoginEventsLast24Hours - this.summary.failedLoginEventsLast24Hours;
    return Math.max(0, Math.round((successful / this.summary.totalLoginEventsLast24Hours) * 100));
  }

  get failedRate(): number {
    return Math.max(0, 100 - this.successRate);
  }

  get activeUserRate(): number {
    if (!this.summary || this.summary.totalUsers <= 0) {
      return 0;
    }

    return Math.round((this.summary.activeUsers / this.summary.totalUsers) * 100);
  }

  get mfaCoverageRate(): number {
    if (this.users.length === 0) {
      return 0;
    }

    const enabled = this.users.filter((user) => user.mfaEnabled).length;
    return Math.round((enabled / this.users.length) * 100);
  }

  get flaggedRate(): number {
    if (!this.summary || this.summary.totalLoginEventsLast24Hours <= 0) {
      return 0;
    }

    return Math.round((this.summary.flaggedEventsLast24Hours / this.summary.totalLoginEventsLast24Hours) * 100);
  }

  get riskStrokeOffset(): number {
    const circumference = 263.89;
    const score = Math.min(100, Math.max(0, this.summary?.aiRiskScore ?? 0));
    return circumference - (score / 100) * circumference;
  }

  get loginTrendPoints(): string {
    const buckets = new Array(12).fill(0);
    const now = Date.now();
    const bucketSize = 2 * 60 * 60 * 1000;

    this.loginEvents.forEach((event) => {
      const timestamp = new Date(event.timestamp).getTime();
      if (Number.isNaN(timestamp)) {
        return;
      }

      const diff = now - timestamp;
      const index = 11 - Math.floor(diff / bucketSize);
      if (index >= 0 && index < buckets.length) {
        buckets[index] += 1;
      }
    });

    if (buckets.every((count) => count === 0) && this.summary?.totalLoginEventsLast24Hours) {
      buckets[11] = this.summary.totalLoginEventsLast24Hours;
      buckets[9] = Math.max(1, Math.round(this.summary.totalLoginEventsLast24Hours * 0.45));
      buckets[6] = Math.max(1, Math.round(this.summary.totalLoginEventsLast24Hours * 0.28));
    }

    const max = Math.max(1, ...buckets);
    return buckets
      .map((count, index) => {
        const x = 8 + index * 23;
        const y = 92 - (count / max) * 72;
        return `${x},${y}`;
      })
      .join(' ');
  }

  get maxFailedAttempts(): number {
    return Math.max(1, ...this.failedLogins.map((item) => item.failedAttempts));
  }

  get latestFlaggedEvents(): AdminLoginEvent[] {
    return this.loginEvents.filter((event) => event.isFlagged).slice(0, 4);
  }

  get observabilitySeries(): AdminLokiSeries[] {
    return this.observabilityResult?.series ?? [];
  }

  get primaryObservabilitySeriesName(): string {
    return this.observabilitySeries.length > 0
      ? `${this.observabilitySeries.length} service series`
      : 'No series loaded';
  }

  get observabilityFallbackLinePoints(): string {
    return '8,92 70,92 132,92 194,92 256,92';
  }

  get observabilityMaxValue(): number {
    const values = this.observabilitySeries.flatMap((series) => series.points.map((point) => point.value));
    return Math.max(1, ...values);
  }

  get observabilityPieGradient(): string {
    if (this.observabilityTotal <= 0 || this.observabilitySeries.length === 0) {
      return 'conic-gradient(#e2e8f0 0deg 360deg)';
    }

    let current = 0;
    const slices = this.observabilitySeries.map((series, index) => {
      const start = current;
      const degrees = (series.total / this.observabilityTotal) * 360;
      current += degrees;
      return `${this.observabilityColor(index)} ${start.toFixed(2)}deg ${current.toFixed(2)}deg`;
    });

    return `conic-gradient(${slices.join(', ')})`;
  }

  observabilityLinePointsFor(series: AdminLokiSeries): string {
    if (series.points.length === 0) {
      return this.observabilityFallbackLinePoints;
    }

    const count = Math.max(1, series.points.length - 1);

    return series.points
      .map((point, index) => {
        const x = 8 + (index / count) * 248;
        const y = 92 - (point.value / this.observabilityMaxValue) * 72;
        return `${x.toFixed(1)},${y.toFixed(1)}`;
      })
      .join(' ');
  }

  get observabilityTotal(): number {
    return this.observabilitySeries.reduce((sum, series) => sum + series.total, 0);
  }

  get grafanaExploreUrl(): string {
    const params = encodeURIComponent(JSON.stringify({
      datasource: 'Loki',
      queries: [{ refId: 'A', expr: this.observabilityQuery }],
      range: { from: `now-${this.observabilityHours}h`, to: 'now' }
    }));

    return `http://localhost:3000/explore?left=${params}`;
  }

  failedLoginWidth(item: AdminFailedLoginByIp): number {
    return Math.max(8, Math.round((item.failedAttempts / this.maxFailedAttempts) * 100));
  }

  observabilitySliceWidth(series: AdminLokiSeries): number {
    if (this.observabilityTotal <= 0) {
      return 0;
    }

    return Math.max(4, Math.round((series.total / this.observabilityTotal) * 100));
  }

  observabilityColor(index: number): string {
    return this.observabilityPalette[index % this.observabilityPalette.length];
  }

  statusClass(status: string): string {
    if (status === 'Online') {
      return 'bg-emerald-50 text-emerald-700 border-emerald-200';
    }

    if (status === 'Watch') {
      return 'bg-amber-50 text-amber-700 border-amber-200';
    }

    if (status === 'Linked') {
      return 'bg-blue-50 text-blue-700 border-blue-200';
    }

    return 'bg-slate-100 text-slate-700 border-slate-200';
  }

  riskClass(): string {
    if (this.summary?.aiRiskLevel === 'High') {
      return 'text-red-700';
    }

    if (this.summary?.aiRiskLevel === 'Medium') {
      return 'text-amber-700';
    }

    return 'text-emerald-700';
  }

  userStatusClass(status: string): string {
    if (status === 'Active') {
      return 'bg-emerald-50 text-emerald-700 border-emerald-200';
    }

    if (status === 'Suspended') {
      return 'bg-amber-50 text-amber-700 border-amber-200';
    }

    return 'bg-red-50 text-red-700 border-red-200';
  }

  eventStatusClass(event: AdminLoginEvent): string {
    if (event.isFlagged) {
      return 'bg-amber-50 text-amber-700 border-amber-200';
    }

    if (event.success) {
      return 'bg-emerald-50 text-emerald-700 border-emerald-200';
    }

    return 'bg-red-50 text-red-700 border-red-200';
  }

  private loadDashboard(): void {
    this.isLoading = true;
    this.errorMessage = '';

    forkJoin({
      summary: this.adminService.getSecuritySummary(),
      failedLogins: this.adminService.getFailedLogins(10),
      loginEvents: this.adminService.getLoginEvents(10),
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

  runObservabilityQuery(): void {
    if (this.isObservabilityLoading || !this.observabilityQuery.trim()) {
      return;
    }

    this.isObservabilityLoading = true;
    this.observabilityErrorMessage = '';

    this.adminService.queryLoki({
      query: this.observabilityQuery.trim(),
      hours: this.observabilityHours,
      limit: 20
    }).subscribe({
      next: (result) => {
        this.observabilityResult = result;
        this.isObservabilityLoading = false;
      },
      error: (error) => {
        const detail = typeof error?.error === 'string'
          ? error.error
          : error?.error?.message;
        this.observabilityErrorMessage = detail
          ? `Unable to run Loki query: ${detail}`
          : 'Unable to run Loki query. Check Loki availability and query syntax.';
        this.isObservabilityLoading = false;
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

  openContactUser(user: AdminUser): void {
    this.selectedUserForContact = user;
    this.adminActionMessage = '';
    this.contactForm.reset({
      subject: 'Account support message',
      message: `Hello ${user.name}, an administrator reviewed your account and left a support note.`
    });
  }

  closeContactUser(): void {
    this.selectedUserForContact = null;
    this.contactForm.reset();
  }

  sendContactMessage(): void {
    if (!this.selectedUserForContact || this.contactForm.invalid) {
      this.contactForm.markAllAsTouched();
      return;
    }

    const values = this.contactForm.getRawValue();
    this.notificationService.addAdminMessageForUser(
      this.selectedUserForContact.email,
      values.subject,
      values.message
    );

    this.adminActionMessage = `Message sent to ${this.selectedUserForContact.email}.`;
    this.closeContactUser();
  }

  openPasswordReset(user: AdminUser): void {
    this.selectedUserForPassword = user;
    this.adminActionMessage = '';
    this.passwordResetForm.reset();
  }

  closePasswordReset(): void {
    this.selectedUserForPassword = null;
    this.passwordResetForm.reset();
  }

  resetUserPassword(): void {
    if (!this.selectedUserForPassword || this.passwordResetForm.invalid || this.isResettingPassword) {
      this.passwordResetForm.markAllAsTouched();
      return;
    }

    const values = this.passwordResetForm.getRawValue();
    if (values.newPassword !== values.confirmPassword) {
      this.passwordResetForm.controls.confirmPassword.setErrors({ mismatch: true });
      return;
    }

    this.isResettingPassword = true;
    this.adminService.resetUserPassword(this.selectedUserForPassword.userId, values.newPassword).subscribe({
      next: () => {
        this.isResettingPassword = false;
        this.notificationService.addAdminMessageForUser(
          this.selectedUserForPassword!.email,
          'Your password was changed',
          'An administrator updated your account password. If you did not request this, contact support immediately.'
        );
        this.adminActionMessage = `Password updated for ${this.selectedUserForPassword!.email}.`;
        this.closePasswordReset();
      },
      error: () => {
        this.isResettingPassword = false;
        this.adminActionMessage = 'Unable to update user password.';
      }
    });
  }
}
