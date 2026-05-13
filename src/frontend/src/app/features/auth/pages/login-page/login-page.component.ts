import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators, FormControl } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../data-access/auth.service';
import { SessionService } from '../../../../core/session/session.service';

@Component({
  selector: 'app-login-page',
  standalone: true,
  imports: [RouterLink, ReactiveFormsModule, CommonModule],
  templateUrl: './login-page.component.html'
})
export class LoginPageComponent {
  isSubmitting = false;
  errorMessage = '';
  showPassword = false;
  isGoogleSubmitting = false;
  rememberMe = false;
  isMfaRequired = false;
  mfaChallengeToken = '';
  mfaQuestionText = '';
  mfaExpiresAt = '';
  // Forgot-password flow
  isForgotFlow = false;
  passwordResetToken = '';
  newPasswordControl = new FormControl('', [Validators.required, Validators.minLength(8)]);
  confirmPasswordControl = new FormControl('', [Validators.required]);

  readonly loginForm = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]]
  });

  readonly mfaForm = this.fb.nonNullable.group({
    answer: ['', [Validators.required, Validators.minLength(1)]]
  });

  constructor(
    private readonly fb: FormBuilder,
    private readonly authService: AuthService,
    private readonly sessionService: SessionService,
    private readonly router: Router
  ) {}

  togglePasswordVisibility(): void {
    this.showPassword = !this.showPassword;
  }

  onSubmit(): void {
    if (this.loginForm.invalid || this.isSubmitting) {
      this.loginForm.markAllAsTouched();
      return;
    }

    this.errorMessage = '';
    this.isSubmitting = true;

    this.authService.login(this.loginForm.getRawValue()).subscribe({
      next: (response) => {
        this.isSubmitting = false;

        if (response.mfaRequired && response.mfaChallengeToken) {
          this.beginMfaChallenge(response);
          return;
        }

        this.navigateAfterAuth();
      },
      error: () => {
        this.isSubmitting = false;
        this.errorMessage = 'Login failed. Check your credentials and try again.';
      }
    });
  }

  onGoogleLogin(): void {
    if (this.isGoogleSubmitting) {
      return;
    }

    this.isGoogleSubmitting = true;
    this.errorMessage = '';

    this.authService.googleLogin().subscribe({
      next: (response) => {
        this.isGoogleSubmitting = false;

        if (response.mfaRequired && response.mfaChallengeToken) {
          this.beginMfaChallenge(response);
          return;
        }

        this.navigateAfterAuth();
      },
      error: (error) => {
        this.isGoogleSubmitting = false;
        console.error('Google login error:', error);
        if (error.status === 400) {
          this.errorMessage = 'Google login failed. Please make sure your Google account is properly configured.';
        } else if (error.status === 401) {
          this.errorMessage = 'Authentication failed. Please try again.';
        } else {
          this.errorMessage = 'Google login is currently unavailable. Please use email/password login.';
        }
      }
    });
  }

  onSubmitMfa(): void {
    if (this.mfaForm.invalid || this.isSubmitting) {
      this.mfaForm.markAllAsTouched();
      return;
    }

    if (!this.mfaChallengeToken) {
      this.errorMessage = 'MFA challenge is missing. Please sign in again.';
      return;
    }

    this.errorMessage = '';
    this.isSubmitting = true;

    if (this.isForgotFlow) {
      this.authService.verifyForgotPassword({
        challengeToken: this.mfaChallengeToken,
        answer: this.mfaForm.getRawValue().answer
      }).subscribe({
        next: (res) => {
          this.isSubmitting = false;
          if (res?.success && res.passwordResetToken) {
            this.passwordResetToken = res.passwordResetToken;
            // show new password inputs
          } else {
            this.errorMessage = 'Unable to verify answer for password reset.';
          }
        },
        error: () => {
          this.isSubmitting = false;
          this.errorMessage = 'The security answer was not accepted. Please try again.';
        }
      });
      return;
    }

    this.authService.verifyMfaLogin({
      challengeToken: this.mfaChallengeToken,
      answer: this.mfaForm.getRawValue().answer
    }).subscribe({
      next: () => {
        this.isSubmitting = false;
        this.navigateAfterAuth();
      },
      error: () => {
        this.isSubmitting = false;
        this.errorMessage = 'The security answer was not accepted. Please try again.';
      }
    });
  }

  backToPasswordLogin(): void {
    this.isMfaRequired = false;
    this.mfaChallengeToken = '';
    this.mfaQuestionText = '';
    this.mfaExpiresAt = '';
    this.mfaForm.reset();
    this.errorMessage = '';
  }

  private beginMfaChallenge(response: { mfaChallengeToken?: string | null; mfaQuestionText?: string | null; mfaExpiresAt?: string | null }): void {
    this.isMfaRequired = true;
    this.mfaChallengeToken = response.mfaChallengeToken || '';
    this.mfaQuestionText = response.mfaQuestionText || 'Security question';
    this.mfaExpiresAt = response.mfaExpiresAt || '';
    this.mfaForm.reset();
    this.isForgotFlow = false;
    this.passwordResetToken = '';
  }

  private navigateAfterAuth(): void {
    const role = this.sessionService.currentUser?.role?.toLowerCase();
    this.router.navigate([role === 'admin' ? '/admin' : '/dashboard']);
  }

  startForgotPassword(event: Event): void {
    event.preventDefault();
    if (this.loginForm.get('email')?.invalid) {
      this.loginForm.get('email')?.markAsTouched();
      this.errorMessage = 'Please provide your account email to reset password.';
      return;
    }

    this.errorMessage = '';
    this.isSubmitting = true;
    const email = this.loginForm.getRawValue().email;

    this.authService.initiateForgotPassword({ email }).subscribe({
      next: (res) => {
        this.isSubmitting = false;
        if (res?.success && res.challengeToken) {
          this.isForgotFlow = true;
          this.mfaChallengeToken = res.challengeToken;
          this.mfaQuestionText = res.questionText || 'Security question';
          this.isMfaRequired = true;
        } else {
          this.errorMessage = 'Unable to initiate password reset. Please contact support.';
        }
      },
      error: () => {
        this.isSubmitting = false;
        this.errorMessage = 'Unable to initiate password reset. Please contact support.';
      }
    });
  }

  submitNewPassword(): void {
    if (!this.passwordResetToken) {
      this.errorMessage = 'No password reset token available.';
      return;
    }

    if (this.newPasswordControl.invalid || this.confirmPasswordControl.invalid) {
      this.newPasswordControl.markAsTouched();
      this.confirmPasswordControl.markAsTouched();
      return;
    }

    if (this.newPasswordControl.value !== this.confirmPasswordControl.value) {
      this.errorMessage = 'Passwords do not match.';
      return;
    }

    this.isSubmitting = true;
    this.authService.resetPassword({ passwordResetToken: this.passwordResetToken, newPassword: String(this.newPasswordControl.value) }).subscribe({
      next: (res) => {
        this.isSubmitting = false;
        if (res?.success) {
          this.errorMessage = '';
          // Reset complete - navigate to login view
          this.backToPasswordLogin();
          this.loginForm.get('password')?.setValue('');
          // Inform user
          this.errorMessage = 'Password has been reset. Please log in with your new password.';
        } else {
          this.errorMessage = 'Failed to reset password.';
        }
      },
      error: () => {
        this.isSubmitting = false;
        this.errorMessage = 'Failed to reset password.';
      }
    });
  }
}