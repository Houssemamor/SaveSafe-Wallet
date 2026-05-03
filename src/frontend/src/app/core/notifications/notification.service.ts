import { Injectable } from '@angular/core';
import { Observable, of, BehaviorSubject } from 'rxjs';
import { HttpClient } from '@angular/common/http';
import { API_CONFIG } from '../config/api.config';

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

/**
 * Service for managing user notifications
 * Handles notification retrieval, marking as read, and notification counts
 */
@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly notificationsSubject = new BehaviorSubject<Notification[]>([]);
  private readonly unreadCountSubject = new BehaviorSubject<number>(0);

  constructor(private readonly http: HttpClient) {}

  /**
   * Get all notifications for the current user
   * @returns Observable containing array of notifications
   */
  getNotifications(): Observable<Notification[]> {
    // In production, this would call the backend API
    // For now, return mock notifications
    return of(this.getMockNotifications());
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

  /**
   * Update unread count based on notifications array
   * @param notifications - Array of notifications to count
   */
  private updateUnreadCount(notifications: Notification[]): void {
    const unreadCount = notifications.filter(notification => !notification.isRead).length;
    this.unreadCountSubject.next(unreadCount);
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
        title: 'Transfer Completed',
        message: 'Your transfer of $150.00 to john@example.com has been completed successfully.',
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