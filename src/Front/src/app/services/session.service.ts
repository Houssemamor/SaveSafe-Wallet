import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { SessionUser } from '../models/auth.models';

const ACCESS_TOKEN_KEY = 'ssw_access_token';
const SESSION_USER_KEY = 'ssw_session_user';

@Injectable({ providedIn: 'root' })
export class SessionService {
  private readonly currentUserSubject = new BehaviorSubject<SessionUser | null>(this.readUserFromStorage());
  readonly currentUser$ = this.currentUserSubject.asObservable();

  get accessToken(): string | null {
    return localStorage.getItem(ACCESS_TOKEN_KEY);
  }

  get currentUser(): SessionUser | null {
    return this.currentUserSubject.value;
  }

  get isAuthenticated(): boolean {
    return !!this.accessToken;
  }

  setSession(accessToken: string, user: SessionUser): void {
    localStorage.setItem(ACCESS_TOKEN_KEY, accessToken);
    localStorage.setItem(SESSION_USER_KEY, JSON.stringify(user));
    this.currentUserSubject.next(user);
  }

  clearSession(): void {
    localStorage.removeItem(ACCESS_TOKEN_KEY);
    localStorage.removeItem(SESSION_USER_KEY);
    this.currentUserSubject.next(null);
  }

  private readUserFromStorage(): SessionUser | null {
    const raw = localStorage.getItem(SESSION_USER_KEY);
    if (!raw) {
      return null;
    }

    try {
      return JSON.parse(raw) as SessionUser;
    } catch {
      localStorage.removeItem(SESSION_USER_KEY);
      return null;
    }
  }
}
