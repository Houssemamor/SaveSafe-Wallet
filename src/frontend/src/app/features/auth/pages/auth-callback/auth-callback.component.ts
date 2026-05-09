import { Component } from '@angular/core';
import { Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { SessionService } from '../../../../core/session/session.service';
import { API_CONFIG } from '../../../../core/config/api.config';

@Component({
  selector: 'app-auth-callback',
  template: '<p>Signing you in...</p>',
  standalone: true
})
export class AuthCallbackComponent {
  constructor(
    private readonly router: Router,
    private readonly http: HttpClient,
    private readonly sessionService: SessionService
  ) {
    this.handleCallback();
  }

  private async handleCallback() {
    try {
      const params = new URLSearchParams(window.location.search);
      const token = params.get('token');
      if (!token) {
        this.router.navigate(['/login']);
        return;
      }

      // Temporarily store token so jwtInterceptor will attach it
      localStorage.setItem('ssw_access_token', token);

      // Fetch profile
      const profile: any = await this.http.get(`${API_CONFIG.usersBaseUrl}/profile`).toPromise();

      const user = {
        userId: profile.userId,
        email: profile.email,
        name: profile.name,
        role: profile.role
      };

      this.sessionService.setSession(token, user);

      const role = (user.role || '').toLowerCase();
      this.router.navigate([role === 'admin' ? '/admin' : '/dashboard']);
    } catch (e) {
      console.error('Auth callback failed', e);
      localStorage.removeItem('ssw_access_token');
      this.router.navigate(['/login']);
    }
  }
}
