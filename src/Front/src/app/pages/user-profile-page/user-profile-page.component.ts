import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-user-profile-page',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './user-profile-page.component.html'
})
export class UserProfilePageComponent {}