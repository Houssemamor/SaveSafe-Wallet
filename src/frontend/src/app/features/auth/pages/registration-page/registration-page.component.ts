import { NgIf } from '@angular/common';
import { Component } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../data-access/auth.service';

function matchPasswords(control: AbstractControl): { passwordMismatch: true } | null {
  const password = control.get('password')?.value;
  const confirmPassword = control.get('confirmPassword')?.value;

  if (!password || !confirmPassword) {
    return null;
  }

  return password === confirmPassword ? null : { passwordMismatch: true };
}

@Component({
  selector: 'app-registration-page',
  standalone: true,
  imports: [RouterLink, ReactiveFormsModule, NgIf],
  templateUrl: './registration-page.component.html'
})
export class RegistrationPageComponent {
  isSubmitting = false;
  errorMessage = '';
  isGoogleSubmitting = false;

  readonly registerForm = this.fb.nonNullable.group(
    {
      name: ['', [Validators.required, Validators.minLength(2)]],
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(8)]],
      confirmPassword: ['', [Validators.required]],
      termsAccepted: [false, [Validators.requiredTrue]]
    },
    { validators: [matchPasswords] }
  );

  constructor(
    private readonly fb: FormBuilder,
    private readonly authService: AuthService,
    private readonly router: Router
  ) {}

  onSubmit(): void {
    if (this.registerForm.invalid || this.isSubmitting) {
      this.registerForm.markAllAsTouched();
      return;
    }

    const formValue = this.registerForm.getRawValue();
    this.errorMessage = '';
    this.isSubmitting = true;

    this.authService
      .register({
        name: formValue.name,
        email: formValue.email,
        password: formValue.password
      })
      .subscribe({
        next: () => {
          this.isSubmitting = false;
          this.router.navigate(['/dashboard']);
        },
        error: () => {
          this.isSubmitting = false;
          this.errorMessage = 'Registration failed. Please verify your data and retry.';
        }
      });
  }

  onGoogleRegister(): void {
    if (this.isGoogleSubmitting) {
      return;
    }

    this.isGoogleSubmitting = true;
    this.errorMessage = '';

    this.authService.googleLogin().subscribe({
      next: () => {
        this.isGoogleSubmitting = false;
        this.router.navigate(['/dashboard']);
      },
      error: (error) => {
        this.isGoogleSubmitting = false;
        console.error('Google registration error:', error);
        if (error.status === 400) {
          this.errorMessage = 'Google registration failed. Please make sure your Google account is properly configured.';
        } else if (error.status === 401) {
          this.errorMessage = 'Authentication failed. Please try again.';
        } else {
          this.errorMessage = 'Google registration is currently unavailable. Please use email/password registration.';
        }
      }
    });
  }
}