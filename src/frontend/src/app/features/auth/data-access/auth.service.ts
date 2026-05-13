import { HttpClient } from '@angular/common/http';
import { Injectable, Optional } from '@angular/core';
import { Observable, tap, from, switchMap } from 'rxjs';
// Firebase imports are optional - only used for Google login
import { Auth, GoogleAuthProvider, signInWithPopup, UserCredential } from '@angular/fire/auth';
import { API_CONFIG } from '../../../core/config/api.config';
import {
  AuthResponse,
  LoginRequest,
  MfaDisableResponse,
  MfaEnrollRequest,
  MfaEnrollResponse,
  MfaVerifyRequest,
  RegisterRequest,
  SecurityQuestionCatalogResponse,
  SessionUser
} from '../models/auth.models';
import { SessionService } from '../../../core/session/session.service';

@Injectable({ providedIn: 'root' })
export class AuthService {
  constructor(
    private readonly http: HttpClient,
    private readonly sessionService: SessionService,
    @Optional() private readonly auth: Auth
  ) {
  }

  login(request: LoginRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${API_CONFIG.authBaseUrl}/login`, request, { withCredentials: true })
      .pipe(tap((response) => this.saveAuthSession(response)));
  }

  register(request: RegisterRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${API_CONFIG.authBaseUrl}/register`, request, { withCredentials: true })
      .pipe(tap((response) => this.saveAuthSession(response)));
  }

  googleLogin(): Observable<AuthResponse> {
    if (!this.auth) {
      throw new Error('Google login is not available. Firebase Auth is not configured.');
    }

    const provider = new GoogleAuthProvider();

    return from(signInWithPopup(this.auth, provider)).pipe(
      switchMap((userCredential: UserCredential) => {
        const user = userCredential.user;

        // getIdToken returns a Promise, so convert it to an Observable before posting it.
        return from(user.getIdToken(true)).pipe(
          switchMap((idToken) =>
            this.http.post<AuthResponse>(
              `${API_CONFIG.authBaseUrl}/google-login`,
              { idToken },
              { withCredentials: true }
            ).pipe(tap((response) => this.saveAuthSession(response)))
          )
        );
      })
    );
  }

  verifyMfaLogin(request: MfaVerifyRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${API_CONFIG.authBaseUrl}/mfa/verify`, request, { withCredentials: true })
      .pipe(tap((response) => this.saveAuthSession(response)));
  }

  getMfaQuestions(): Observable<SecurityQuestionCatalogResponse> {
    return this.http.get<SecurityQuestionCatalogResponse>(`${API_CONFIG.authBaseUrl}/mfa/questions`, { withCredentials: true });
  }

  enrollMfa(request: MfaEnrollRequest): Observable<MfaEnrollResponse> {
    return this.http.post<MfaEnrollResponse>(`${API_CONFIG.authBaseUrl}/mfa/enroll`, request, { withCredentials: true });
  }

  initiateForgotPassword(email: { email: string }): Observable<{ success: boolean; questionText?: string; challengeToken?: string }> {
    return this.http.post<{ success: boolean; questionText?: string; challengeToken?: string }>(
      `${API_CONFIG.authBaseUrl}/mfa/forgot/initiate`,
      email,
      { withCredentials: true }
    );
  }

  verifyForgotPassword(request: { challengeToken: string; answer: string }): Observable<{ success: boolean; passwordResetToken?: string }> {
    return this.http.post<{ success: boolean; passwordResetToken?: string }>(`${API_CONFIG.authBaseUrl}/mfa/forgot/verify`, request, { withCredentials: true });
  }

  resetPassword(request: { passwordResetToken: string; newPassword: string }): Observable<{ success: boolean }> {
    return this.http.post<{ success: boolean }>(`${API_CONFIG.authBaseUrl}/reset-password`, request, { withCredentials: true });
  }

  disableMfa(): Observable<MfaDisableResponse> {
    return this.http.post<MfaDisableResponse>(`${API_CONFIG.authBaseUrl}/mfa/disable`, {}, { withCredentials: true });
  }

  refresh(): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${API_CONFIG.authBaseUrl}/refresh`, {}, { withCredentials: true })
      .pipe(tap((response) => this.saveAuthSession(response)));
  }

  logout(): Observable<void> {
    // Sign out from Firebase as well if available
    if (this.auth) {
      this.auth.signOut().catch(console.error);
    }

    return this.http.post<void>(`${API_CONFIG.authBaseUrl}/logout`, {}, { withCredentials: true }).pipe(
      tap(() => this.sessionService.clearSession())
    );
  }

  clearLocalSession(): void {
    this.sessionService.clearSession();
  }

  private saveAuthSession(response: AuthResponse): void {
    if (!response.accessToken || !response.userId || !response.email || !response.name || !response.role) {
      return;
    }

    const user: SessionUser = {
      userId: response.userId,
      email: response.email,
      name: response.name,
      role: response.role,
      profilePictureUrl: response.profilePictureUrl || null
    };

    this.sessionService.setSession(response.accessToken, user);
  }
}
