import { Routes } from '@angular/router';
import { DashboardComponent } from './pages/dashboard/dashboard.component';
import { ReviewComponent } from './pages/review/review.component';
import { AdminComponent } from './pages/admin/admin.component';

export const routes: Routes = [
  { path: '', redirectTo: '/dashboard', pathMatch: 'full' },
  { path: 'dashboard', component: DashboardComponent },
  { path: 'review', component: ReviewComponent },
  { path: 'admin', component: AdminComponent },
  { path: '**', redirectTo: '/dashboard' }
];
