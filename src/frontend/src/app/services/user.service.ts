import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_CONFIG } from '../config/api.config';
import { UpdateUserProfileRequest, UserProfileResponse } from '../models/user.models';

@Injectable({ providedIn: 'root' })
export class UserService {
  constructor(private readonly http: HttpClient) {}

  getProfile(): Observable<UserProfileResponse> {
    return this.http.get<UserProfileResponse>(`${API_CONFIG.usersBaseUrl}/profile`);
  }

  updateProfile(request: UpdateUserProfileRequest): Observable<void> {
    return this.http.put<void>(`${API_CONFIG.usersBaseUrl}/profile`, request);
  }
}
