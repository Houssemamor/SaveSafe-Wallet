import { HttpClient } from '@angular/common/http';
import { Injectable, Optional } from '@angular/core';
import { Observable, tap, from, switchMap } from 'rxjs';
// Firebase imports are optional - only used for Google login
import { Auth, GoogleAuthProvider, signInWithPopup, UserCredential } from '@angular/fire/auth';
import { API_CONFIG } from '../../../core/config/api.config';
import { AuthResponse, LoginRequest, RegisterRequest, SessionUser } from '../models/auth.models';
import { SessionService } from '../../../core/session/session.service';

@Injectable({ providedIn: 'root' })
export class AuthService {
  constructor(
    private readonly http: HttpClient,
    private readonly sessionService: SessionService,
    @Optional() private readonly auth: Auth
  ) {}

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
        const idToken = user.getIdToken(true);

        // Send the Google ID token to your backend for verification
        return this.http.post<AuthResponse>(
          `${API_CONFIG.authBaseUrl}/google-login`,
          { idToken },
          { withCredentials: true }
        ).pipe(tap((response) => this.saveAuthSession(response)));
      })
    );
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
    const user: SessionUser = {
      userId: response.userId,
      email: response.email,
      name: response.name,
      role: response.role
    };

    this.sessionService.setSession(response.accessToken, user);
  }
}
