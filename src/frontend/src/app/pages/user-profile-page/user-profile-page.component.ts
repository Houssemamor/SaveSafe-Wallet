import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { SessionUser } from '../../models/auth.models';
import { UserProfileResponse } from '../../models/user.models';
import { AuthService } from '../../services/auth.service';
import { SessionService } from '../../services/session.service';
import { UserService } from '../../services/user.service';

@Component({
  selector: 'app-user-profile-page',
  standalone: true,
  imports: [RouterLink, ReactiveFormsModule, CommonModule],
  templateUrl: './user-profile-page.component.html'
})
export class UserProfilePageComponent implements OnInit {
  user: SessionUser | null = null;
  profile: UserProfileResponse | null = null;
  isLoading = true;
  isSaving = false;
  errorMessage = '';
  successMessage = '';

  readonly profileForm = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.minLength(2)]],
    email: [{ value: '', disabled: true }],
    role: [{ value: '', disabled: true }],
    accountStatus: [{ value: '', disabled: true }]
  });

  constructor(
    private readonly fb: FormBuilder,
    private readonly sessionService: SessionService,
    private readonly userService: UserService,
    private readonly authService: AuthService,
    private readonly router: Router
  ) {}

  ngOnInit(): void {
    this.user = this.sessionService.currentUser;
    this.loadProfile();
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

  logout(): void {
    this.authService.logout().subscribe({
      next: () => this.router.navigate(['/login']),
      error: () => {
        this.authService.clearLocalSession();
        this.router.navigate(['/login']);
      }
    });
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
}