import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { UserProfileResponse } from '../../models/user.models';
import { SessionService } from '../../../../core/session/session.service';
import { AuthService } from '../../../auth/data-access/auth.service';
import { SessionUser } from '../../../auth/models/auth.models';
import { UserService } from '../../data-access/user.service';
import { UserAvatarComponent } from '../../../../core/components/user-avatar/user-avatar.component';
import { Notification, NotificationService } from '../../../../core/notifications/notification.service';
import { NotificationItemComponent } from '../../../dashboard/components/notification-item/notification-item.component';
import { SecurityQuestionCatalogItem } from '../../../auth/models/auth.models';

function matchPasswordFields(control: AbstractControl): { passwordMismatch: true } | null {
  const newPassword = control.get('newPassword')?.value;
  const confirmPassword = control.get('confirmPassword')?.value;

  if (!newPassword || !confirmPassword) {
    return null;
  }

  return newPassword === confirmPassword ? null : { passwordMismatch: true };
}

@Component({
  selector: 'app-user-profile-page',
  standalone: true,
  imports: [RouterLink, ReactiveFormsModule, CommonModule, UserAvatarComponent, NotificationItemComponent],
  templateUrl: './user-profile-page.component.html'
})
export class UserProfilePageComponent implements OnInit {
  user: SessionUser | null = null;
  profile: UserProfileResponse | null = null;
  isLoading = true;
  isSaving = false;
  isMfaUpdating = false;
  isPasswordUpdating = false;
  errorMessage = '';
  successMessage = '';
  mfaErrorMessage = '';
  mfaSuccessMessage = '';
  passwordErrorMessage = '';
  passwordSuccessMessage = '';
  securityQuestions: SecurityQuestionCatalogItem[] = [];
  readonly unreadNotificationCount$ = this.notificationService.getUnreadCount();
  readonly notifications$: Observable<Notification[]> = this.notificationService.getNotifications();
  showNotificationPopover = false;

  readonly profileForm = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.minLength(2)]],
    email: [{ value: '', disabled: true }],
    role: [{ value: '', disabled: true }],
    accountStatus: [{ value: '', disabled: true }]
  });

  readonly mfaForm = this.fb.nonNullable.group({
    question1Id: ['', [Validators.required]],
    question1Answer: ['', [Validators.required, Validators.minLength(1)]],
    question2Id: ['', [Validators.required]],
    question2Answer: ['', [Validators.required, Validators.minLength(1)]]
  });

  readonly passwordForm = this.fb.nonNullable.group({
    currentPassword: [''],
    newPassword: ['', [Validators.required, Validators.minLength(8)]],
    confirmPassword: ['', [Validators.required]]
  }, { validators: matchPasswordFields });

  constructor(
    private readonly fb: FormBuilder,
    private readonly sessionService: SessionService,
    private readonly userService: UserService,
    private readonly authService: AuthService,
    private readonly router: Router,
    private readonly notificationService: NotificationService
  ) {}

  ngOnInit(): void {
    this.user = this.sessionService.currentUser;
    this.loadProfile();
    this.loadSecurityQuestions();
  }

  get createdAt(): string {
    if (!this.profile?.createdAt) {
      return 'N/A';
    }

    return new Date(this.profile.createdAt).toLocaleDateString();
  }

  get lastLoginAt(): string {
    if (!this.profile?.lastLoginAt) {
      return 'N/A';
    }

    return new Date(this.profile.lastLoginAt).toLocaleString();
  }

  get profilePictureUrl(): string | null | undefined {
    // Prefer profile data, fall back to session user data
    return this.profile?.profilePictureUrl || this.user?.profilePictureUrl || null;
  }

  get passwordActionLabel(): string {
    return this.profile?.hasPassword ? 'CHANGE PASSWORD' : 'SET PASSWORD';
  }

  get passwordSectionTitle(): string {
    return this.profile?.hasPassword ? 'Password' : 'Set Password';
  }

  get passwordSectionDescription(): string {
    if (!this.profile?.hasPassword && this.profile?.isGoogleAccount) {
      return 'Add a password so this Google account can also sign in with email and password.';
    }

    return 'Keep your account password up to date.';
  }

  onSave(): void {
    if (this.profileForm.invalid || this.isSaving) {
      this.profileForm.markAllAsTouched();
      return;
    }

    const name = this.profileForm.controls.name.value.trim();
    if (!name) {
      this.profileForm.controls.name.setErrors({ required: true });
      return;
    }

    this.isSaving = true;
    this.errorMessage = '';
    this.successMessage = '';

    this.userService.updateProfile({ name }).subscribe({
      next: () => {
        this.isSaving = false;
        this.successMessage = 'Profile updated successfully.';

        const accessToken = this.sessionService.accessToken;
        if (this.user && accessToken) {
          this.sessionService.setSession(accessToken, {
            ...this.user,
            name
          });
          this.user = this.sessionService.currentUser;
        }

        this.loadProfile();
      },
      error: () => {
        this.isSaving = false;
        this.errorMessage = 'Unable to update profile. Please try again.';
      }
    });
  }

  enableMfa(): void {
    if (this.mfaForm.invalid || this.isMfaUpdating) {
      this.mfaForm.markAllAsTouched();
      return;
    }

    const values = this.mfaForm.getRawValue();
    if (values.question1Id === values.question2Id) {
      this.mfaErrorMessage = 'Choose two different security questions.';
      return;
    }

    this.isMfaUpdating = true;
    this.mfaErrorMessage = '';
    this.mfaSuccessMessage = '';

    this.authService.enrollMfa({
      questions: [
        { questionId: values.question1Id, answer: values.question1Answer },
        { questionId: values.question2Id, answer: values.question2Answer }
      ]
    }).subscribe({
      next: () => {
        this.isMfaUpdating = false;
        this.mfaSuccessMessage = 'MFA has been enabled.';
        this.loadProfile();
      },
      error: () => {
        this.isMfaUpdating = false;
        this.mfaErrorMessage = 'Unable to enable MFA. Please try again.';
      }
    });
  }

  disableMfa(): void {
    if (this.isMfaUpdating) {
      return;
    }

    this.isMfaUpdating = true;
    this.mfaErrorMessage = '';
    this.mfaSuccessMessage = '';

    this.authService.disableMfa().subscribe({
      next: () => {
        this.isMfaUpdating = false;
        this.mfaSuccessMessage = 'MFA has been disabled.';
        this.mfaForm.reset();
        this.loadProfile();
      },
      error: () => {
        this.isMfaUpdating = false;
        this.mfaErrorMessage = 'Unable to disable MFA. Please try again.';
      }
    });
  }

  updatePassword(): void {
    this.passwordErrorMessage = '';
    this.passwordSuccessMessage = '';

    if (this.passwordForm.invalid || this.isPasswordUpdating) {
      this.passwordForm.markAllAsTouched();
      if (this.passwordForm.hasError('passwordMismatch')) {
        this.passwordErrorMessage = 'Passwords do not match.';
      }
      return;
    }

    const values = this.passwordForm.getRawValue();
    const hasPassword = this.profile?.hasPassword ?? true;

    if (hasPassword && !values.currentPassword.trim()) {
      this.passwordForm.controls.currentPassword.setErrors({ required: true });
      this.passwordErrorMessage = 'Enter your current password.';
      return;
    }

    this.isPasswordUpdating = true;

    this.userService.updatePassword({
      currentPassword: hasPassword ? values.currentPassword : undefined,
      newPassword: values.newPassword
    }).subscribe({
      next: () => {
        this.isPasswordUpdating = false;
        this.passwordSuccessMessage = hasPassword ? 'Password changed successfully.' : 'Password set successfully.';
        this.passwordForm.reset();
        this.loadProfile();
      },
      error: (error) => {
        this.isPasswordUpdating = false;
        this.passwordErrorMessage = error.status === 401
          ? 'Current password is incorrect.'
          : 'Unable to update password. Please try again.';
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

  /**
   * Handle search input
   * Filters profile content based on search query
   */
  onSearch(query: string): void {
    // Profile page doesn't have searchable content
    // This method is included for layout consistency
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
    // Navigate to profile page (current page)
    this.router.navigate(['/profile']);
  }

  private loadProfile(): void {
    this.isLoading = true;
    this.errorMessage = '';

    this.userService.getProfile().subscribe({
      next: (profile) => {
        this.profile = profile;
        this.profileForm.patchValue({
          name: profile.name,
          email: profile.email,
          role: profile.role,
          accountStatus: profile.accountStatus
        });
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
        this.errorMessage = 'Unable to load profile data.';
      }
    });
  }

  private loadSecurityQuestions(): void {
    this.authService.getMfaQuestions().subscribe({
      next: (response) => {
        this.securityQuestions = response.questions;
      },
      error: () => {
        this.securityQuestions = [];
      }
    });
  }
}
