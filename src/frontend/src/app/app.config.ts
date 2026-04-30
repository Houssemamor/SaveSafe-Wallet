import { ApplicationConfig } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter } from '@angular/router';
// Temporarily disabled Firebase Auth until proper Firebase setup is completed
// import { provideFirebaseApp, initializeApp } from '@angular/fire/app';
// import { provideAuth, getAuth } from '@angular/fire/auth';
// import { environment } from '../environments/environment';
import { jwtInterceptor } from './core/interceptors/jwt.interceptor';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes),
    provideHttpClient(withInterceptors([jwtInterceptor])),
    // Temporarily disabled Firebase Auth - uncomment when Firebase is properly configured
    // provideFirebaseApp(() => initializeApp(environment.firebase)),
    // provideAuth(() => getAuth())
  ]
};