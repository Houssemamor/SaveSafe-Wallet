import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { API_CONFIG } from '../../../core/config/api.config';
import { AuthResponse, LoginRequest, RegisterRequest, SessionUser } from '../models/auth.models';
import { SessionService } from '../../../core/session/session.service';

@Injectable({ providedIn: 'root' })
export class AuthService {
  constructor(
    private readonly http: HttpClient,
    private readonly sessionService: SessionService
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

  refresh(): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${API_CONFIG.authBaseUrl}/refresh`, {}, { withCredentials: true })
      .pipe(tap((response) => this.saveAuthSession(response)));
  }

  logout(): Observable<void> {
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
