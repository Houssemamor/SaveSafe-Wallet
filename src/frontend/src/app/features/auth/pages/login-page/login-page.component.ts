import { NgIf } from '@angular/common';
import { Component } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../data-access/auth.service';
import { SessionService } from '../../../../core/session/session.service';

@Component({
  selector: 'app-login-page',
  standalone: true,
  imports: [RouterLink, ReactiveFormsModule, NgIf],
  templateUrl: './login-page.component.html'
})
export class LoginPageComponent {
  isSubmitting = false;
  errorMessage = '';
  showPassword = false;
  isGoogleSubmitting = false;

  readonly loginForm = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]]
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
      next: () => {
        this.isSubmitting = false;
        const role = this.sessionService.currentUser?.role?.toLowerCase();
        this.router.navigate([role === 'admin' ? '/admin' : '/dashboard']);
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
      next: () => {
        this.isGoogleSubmitting = false;
        const role = this.sessionService.currentUser?.role?.toLowerCase();
        this.router.navigate([role === 'admin' ? '/admin' : '/dashboard']);
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
}