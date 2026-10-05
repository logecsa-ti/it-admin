import { Routes } from '@angular/router';

/** Cualquier usuario autenticado entra: la API filtra lo que puede ver (ADR-026). */
export const TICKET_ROUTES: Routes = [
  {
    path: '',
    title: 'Tickets · TI Admin',
    loadComponent: () => import('./ticket-list/ticket-list.component').then((m) => m.TicketListComponent),
  },
  {
    path: 'nuevo',
    title: 'Nuevo ticket · TI Admin',
    data: { breadcrumb: 'Nuevo' },
    loadComponent: () => import('./ticket-create/ticket-create.component').then((m) => m.TicketCreateComponent),
  },
  {
    path: ':id',
    title: 'Ticket · TI Admin',
    data: { breadcrumb: 'Detalle' },
    loadComponent: () => import('./ticket-detail/ticket-detail.component').then((m) => m.TicketDetailComponent),
  },
];
