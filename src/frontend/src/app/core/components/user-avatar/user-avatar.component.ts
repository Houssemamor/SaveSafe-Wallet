import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';
import { SessionService } from '../../session/session.service';

@Component({
  selector: 'app-user-avatar',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './user-avatar.component.html'
})
export class UserAvatarComponent {
  @Input() size: 'sm' | 'md' | 'lg' = 'md';
  @Input() src?: string | null;
  @Input() alt = 'User avatar';

  broken = false;

  constructor(private readonly session: SessionService) {}

  get url(): string | null | undefined {
    // prefer explicit src, then session user
    return this.src ?? this.session.currentUser?.profilePictureUrl ?? null;
  }

  get imageSrc(): string | null | undefined {
    const u = this.url;
    if (!u) return null;

    try {
      const hostname = new URL(u).hostname;
      // If image comes from Googleusercontent, route through backend proxy to avoid rate limits
      if (hostname.includes('googleusercontent.com') || hostname.includes('lh3.googleusercontent')) {
        return `/api/users/profile/avatar?url=${encodeURIComponent(u)}`;
      }
    } catch {
      // ignore URL parse errors and use original
    }

    return u;
  }

  get classes(): string {
    switch (this.size) {
      case 'sm':
        return 'w-8 h-8 rounded-full';
      case 'lg':
        return 'w-12 h-12 rounded-full';
      default:
        return 'w-10 h-10 rounded-full';
    }
  }

  onError(): void {
    this.broken = true;
    console.warn('UserAvatar: image failed to load', this.url);
  }
}
