import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_CONFIG } from '../../../core/config/api.config';
import {
  AdminFailedLoginByIp,
  AdminLoginEvent,
  AdminSecuritySummary,
  AdminUser
} from '../models/admin.models';

@Injectable({ providedIn: 'root' })
export class AdminService {
  constructor(private readonly http: HttpClient) {}

  getSecuritySummary(): Observable<AdminSecuritySummary> {
    return this.http.get<AdminSecuritySummary>(`${API_CONFIG.adminBaseUrl}/security-summary`);
  }

  refreshSecuritySummary(): Observable<AdminSecuritySummary> {
    return this.http.post<AdminSecuritySummary>(
      `${API_CONFIG.adminBaseUrl}/security-summary/refresh`,
      {});
  }

  getLoginEvents(limit = 50): Observable<AdminLoginEvent[]> {
    const params = new HttpParams().set('limit', limit);
    return this.http.get<AdminLoginEvent[]>(`${API_CONFIG.adminBaseUrl}/login-events`, { params });
  }

  getFailedLogins(top = 20): Observable<AdminFailedLoginByIp[]> {
    const params = new HttpParams().set('top', top);
    return this.http.get<AdminFailedLoginByIp[]>(`${API_CONFIG.adminBaseUrl}/failed-logins`, { params });
  }

  getUsers(limit = 100): Observable<AdminUser[]> {
    const params = new HttpParams().set('limit', limit);
    return this.http.get<AdminUser[]>(`${API_CONFIG.adminBaseUrl}/users`, { params });
  }

  suspendUser(userId: string): Observable<void> {
    return this.http.post<void>(`${API_CONFIG.adminBaseUrl}/users/${userId}/suspend`, {});
  }

  activateUser(userId: string): Observable<void> {
    return this.http.post<void>(`${API_CONFIG.adminBaseUrl}/users/${userId}/activate`, {});
  }

  deleteUser(userId: string): Observable<void> {
    return this.http.delete<void>(`${API_CONFIG.adminBaseUrl}/users/${userId}`);
  }
}
