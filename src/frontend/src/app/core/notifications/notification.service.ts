import { Injectable } from '@angular/core';
import { Observable, of, BehaviorSubject } from 'rxjs';
import { HttpClient } from '@angular/common/http';
import { API_CONFIG } from '../config/api.config';
import { SessionService } from '../session/session.service';

/**
 * Notification type enumeration for categorizing different notification kinds
 */
export enum NotificationType {
  TRANSACTION = 'transaction',
  SECURITY = 'security',
  SYSTEM = 'system',
  PROMOTION = 'promotion'
}

/**
 * Notification priority levels
 */
export enum NotificationPriority {
  LOW = 'low',
  MEDIUM = 'medium',
  HIGH = 'high',
  URGENT = 'urgent'
}

/**
 * Notification interface representing a single notification
 */
export interface Notification {
  id: string;
  type: NotificationType;
  priority: NotificationPriority;
  title: string;
  message: string;
  isRead: boolean;
  createdAt: string;
  actionUrl?: string;
}

const ADMIN_NOTIFICATION_STORAGE_KEY = 'ssw_admin_notifications';

/**
 * Service for managing user notifications
 * Handles notification retrieval, marking as read, and notification counts
 */
@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly notificationsSubject = new BehaviorSubject<Notification[]>([]);
  private readonly unreadCountSubject = new BehaviorSubject<number>(0);

  constructor(
    private readonly http: HttpClient,
    private readonly sessionService: SessionService
  ) {
    const initialNotifications = this.buildNotificationsForCurrentUser();

    this.notificationsSubject.next(initialNotifications);
    this.updateUnreadCount(initialNotifications);

    this.sessionService.currentUser$.subscribe(() => {
      const notifications = this.buildNotificationsForCurrentUser();
      this.notificationsSubject.next(notifications);
      this.updateUnreadCount(notifications);
    });
  }

  /**
   * Get all notifications for the current user
   * @returns Observable containing array of notifications
   */
  getNotifications(): Observable<Notification[]> {
    // In production, this would call the backend API.
    // The shared subject keeps unread counts and read state consistent across the UI.
    return this.notificationsSubject.asObservable();
  }

  /**
   * Get unread notifications count
   * @returns Observable containing unread count
   */
  getUnreadCount(): Observable<number> {
    return this.unreadCountSubject.asObservable();
  }

  /**
   * Mark notification as read
   * @param notificationId - ID of notification to mark as read
   * @returns Observable of void
   */
  markAsRead(notificationId: string): Observable<void> {
    // In production, this would call the backend API
    this.updateAdminMailboxNotification(notificationId, (notification) => ({ ...notification, isRead: true }));

    const currentNotifications = this.notificationsSubject.value;
    const updatedNotifications = currentNotifications.map(notification =>
      notification.id === notificationId ? { ...notification, isRead: true } : notification
    );

    this.notificationsSubject.next(updatedNotifications);
    this.updateUnreadCount(updatedNotifications);

    return of(void 0);
  }

  /**
   * Mark all notifications as read
   * @returns Observable of void
   */
  markAllAsRead(): Observable<void> {
    // In production, this would call the backend API
    this.markAllAdminMailboxNotificationsAsRead();

    const currentNotifications = this.notificationsSubject.value;
    const updatedNotifications = currentNotifications.map(notification => ({
      ...notification,
      isRead: true
    }));

    this.notificationsSubject.next(updatedNotifications);
    this.updateUnreadCount(updatedNotifications);

    return of(void 0);
  }

  /**
   * Delete notification
   * @param notificationId - ID of notification to delete
   * @returns Observable of void
   */
  deleteNotification(notificationId: string): Observable<void> {
    // In production, this would call the backend API
    this.deleteAdminMailboxNotification(notificationId);

    const currentNotifications = this.notificationsSubject.value;
    const updatedNotifications = currentNotifications.filter(
      notification => notification.id !== notificationId
    );

    this.notificationsSubject.next(updatedNotifications);
    this.updateUnreadCount(updatedNotifications);

    return of(void 0);
  }

  /**
   * Show success message to user
   * In production, this would integrate with a toast/notification system
   * @param message - Success message to display
   */
  showSuccess(message: string): void {
    // For now, log to console - in production this would show a toast notification
    console.log(`Success: ${message}`);
  }

  /**
   * Show error message to user
   * In production, this would integrate with a toast/notification system
   * @param message - Error message to display
   */
  showError(message: string): void {
    // For now, log to console - in production this would show a toast notification
    console.error(`Error: ${message}`);
  }

  addAdminMessageForUser(email: string, title: string, message: string): Notification {
    const notification: Notification = {
      id: `admin-${Date.now()}-${Math.random().toString(36).slice(2)}`,
      type: NotificationType.SYSTEM,
      priority: NotificationPriority.HIGH,
      title,
      message,
      isRead: false,
      createdAt: new Date().toISOString(),
      actionUrl: '/dashboard'
    };

    const mailbox = this.readAdminMailbox();
    const normalizedEmail = email.trim().toLowerCase();
    mailbox[normalizedEmail] = [notification, ...(mailbox[normalizedEmail] ?? [])].slice(0, 20);
    localStorage.setItem(ADMIN_NOTIFICATION_STORAGE_KEY, JSON.stringify(mailbox));

    if (this.sessionService.currentUser?.email?.toLowerCase() === normalizedEmail) {
      const notifications = this.buildNotificationsForCurrentUser();
      this.notificationsSubject.next(notifications);
      this.updateUnreadCount(notifications);
    }

    return notification;
  }

  getAdminMessagesForCurrentUser(): Notification[] {
    const email = this.sessionService.currentUser?.email?.trim().toLowerCase();
    if (!email) {
      return [];
    }

    return this.readAdminMailbox()[email] ?? [];
  }

  /**
   * Update unread count based on notifications array
   * @param notifications - Array of notifications to count
   */
  private updateUnreadCount(notifications: Notification[]): void {
    const unreadCount = notifications.filter(notification => !notification.isRead).length;
    this.unreadCountSubject.next(unreadCount);
  }

  private buildNotificationsForCurrentUser(): Notification[] {
    return [
      ...this.getAdminMessagesForCurrentUser(),
      ...this.getMockNotifications()
    ];
  }

  private readAdminMailbox(): Record<string, Notification[]> {
    const raw = localStorage.getItem(ADMIN_NOTIFICATION_STORAGE_KEY);
    if (!raw) {
      return {};
    }

    try {
      return JSON.parse(raw) as Record<string, Notification[]>;
    } catch {
      localStorage.removeItem(ADMIN_NOTIFICATION_STORAGE_KEY);
      return {};
    }
  }

  private updateAdminMailboxNotification(notificationId: string, update: (notification: Notification) => Notification): void {
    const mailbox = this.readAdminMailbox();
    let changed = false;

    for (const email of Object.keys(mailbox)) {
      mailbox[email] = mailbox[email].map((notification) => {
        if (notification.id !== notificationId) {
          return notification;
        }

        changed = true;
        return update(notification);
      });
    }

    if (changed) {
      localStorage.setItem(ADMIN_NOTIFICATION_STORAGE_KEY, JSON.stringify(mailbox));
    }
  }

  private markAllAdminMailboxNotificationsAsRead(): void {
    const mailbox = this.readAdminMailbox();
    const email = this.sessionService.currentUser?.email?.trim().toLowerCase();
    if (!email || !mailbox[email]) {
      return;
    }

    mailbox[email] = mailbox[email].map((notification) => ({ ...notification, isRead: true }));
    localStorage.setItem(ADMIN_NOTIFICATION_STORAGE_KEY, JSON.stringify(mailbox));
  }

  private deleteAdminMailboxNotification(notificationId: string): void {
    const mailbox = this.readAdminMailbox();
    let changed = false;

    for (const email of Object.keys(mailbox)) {
      const before = mailbox[email].length;
      mailbox[email] = mailbox[email].filter((notification) => notification.id !== notificationId);
      changed = changed || mailbox[email].length !== before;
    }

    if (changed) {
      localStorage.setItem(ADMIN_NOTIFICATION_STORAGE_KEY, JSON.stringify(mailbox));
    }
  }

  /**
   * Get mock notifications for development
   * In production, this would be replaced with actual API calls
   * @returns Array of mock notifications
   */
  private getMockNotifications(): Notification[] {
    const now = new Date();
    const yesterday = new Date(now);
    yesterday.setDate(yesterday.getDate() - 1);

    const twoDaysAgo = new Date(now);
    twoDaysAgo.setDate(twoDaysAgo.getDate() - 2);

    return [
      {
        id: '1',
        type: NotificationType.TRANSACTION,
        priority: NotificationPriority.HIGH,
        title: 'Money Received',
        message: 'You received $150.00 from john@example.com.',
        isRead: false,
        createdAt: now.toISOString(),
        actionUrl: '/wallet/history'
      },
      {
        id: '2',
        type: NotificationType.SECURITY,
        priority: NotificationPriority.MEDIUM,
        title: 'Security Reminder',
        message: 'Consider enabling two-factor authentication for enhanced account security.',
        isRead: false,
        createdAt: yesterday.toISOString(),
        actionUrl: '/settings/security'
      },
      {
        id: '3',
        type: NotificationType.SYSTEM,
        priority: NotificationPriority.LOW,
        title: 'System Update',
        message: 'New features have been added to your wallet dashboard.',
        isRead: true,
        createdAt: twoDaysAgo.toISOString()
      }
    ];
  }
}
