import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Notification } from '../../../../core/notifications/notification.service';

@Component({
  selector: 'app-notification-item',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './notification-item.component.html',
  styleUrl: './notification-item.component.css'
})
export class NotificationItemComponent {
  @Input() notification!: Notification;
  @Output() notificationClick = new EventEmitter<Notification>();

  onClick(): void {
    this.notificationClick.emit(this.notification);
  }
}
