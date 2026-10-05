import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { AuthService } from '@core/auth/auth.service';
import { PERMISSIONS as P } from '@core/auth/permissions.generated';
import { TicketDetailDto, TicketStatus } from '@core/models';
import { ToastService } from '@core/services/toast.service';
import { ConfirmService } from '@shared/components/confirm-dialog/confirm.service';
import { DocumentsPanelComponent } from '@shared/components/documents-panel/documents-panel.component';
import { FieldDef } from '@shared/components/form-dialog/form-dialog.component';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { StatusBadgeComponent } from '@shared/components/status-badge/status-badge.component';
import { CanDirective } from '@shared/directives/can.directive';
import { enumLabel } from '@shared/labels/enum-labels';
import { AppDatePipe } from '@shared/pipes/app-date.pipe';
import { DialogsService } from '@shared/services/dialogs.service';
import { LookupService } from '@shared/services/lookup.service';
import { valueOf } from '@shared/utils/resource-utils';
import { TICKET_TRANSITIONS, TicketsService } from '../tickets.service';

interface TransitionAction {
  status: TicketStatus;
  label: string;
  icon: string;
  primary?: boolean;
}

@Component({
  selector: 'app-ticket-detail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatMenuModule,
    MatProgressBarModule,
    PageHeaderComponent,
    StatusBadgeComponent,
    DocumentsPanelComponent,
    CanDirective,
    AppDatePipe,
  ],
  templateUrl: './ticket-detail.component.html',
  styleUrl: './ticket-detail.component.scss',
})
export class TicketDetailComponent {
  private readonly tickets = inject(TicketsService);
  private readonly auth = inject(AuthService);
  private readonly dialogs = inject(DialogsService);
  private readonly confirmService = inject(ConfirmService);
  private readonly lookups = inject(LookupService);
  private readonly toast = inject(ToastService);
  protected readonly P = P;

  readonly id = input.required<number, string>({ transform: Number });

  protected readonly ticket = rxResource({ params: () => this.id(), stream: ({ params }) => this.tickets.get(params) });
  protected readonly commentsResource = rxResource({ params: () => this.id(), stream: ({ params }) => this.tickets.comments(params) });
  protected readonly historyResource = rxResource({ params: () => this.id(), stream: ({ params }) => this.tickets.history(params) });

  protected readonly data = computed(() => valueOf(this.ticket));
  protected readonly comments = computed(() => valueOf(this.commentsResource) ?? []);
  protected readonly history = computed(() => [...(valueOf(this.historyResource) ?? [])].reverse());

  protected readonly myId = computed(() => this.auth.user()?.id);
  protected readonly isAgent = computed(() => this.auth.hasAny(P.TicketsUpdate));
  protected readonly isRequester = computed(() => this.data()?.requesterId === this.auth.user()?.id);
  protected readonly awaitingApproval = computed(() => this.data()?.approvalStatus === 'Pending');
  protected readonly canApprove = computed(() => this.awaitingApproval() && this.auth.hasAny(P.RequestsApprove) && !this.isRequester());
  protected readonly canAssign = computed(() => {
    const t = this.data();
    return !!t && !['Closed', 'Cancelled'].includes(t.status) && !this.awaitingApproval() && this.auth.hasAny(P.TicketsAssign);
  });
  protected readonly canComment = computed(() => {
    const t = this.data();
    return !!t && !['Closed', 'Cancelled'].includes(t.status);
  });

  /** Transiciones validas que este usuario puede ejecutar (mismas reglas que TicketService.ChangeStatusAsync). */
  protected readonly transitions = computed<TransitionAction[]>(() => {
    const t = this.data();
    if (!t || this.awaitingApproval()) return [];
    const agent = this.isAgent();
    const requester = this.isRequester();
    return TICKET_TRANSITIONS[t.status]
      .filter((to) => {
        switch (to) {
          case 'Resolved':
            return this.auth.hasAny(P.TicketsResolve);
          case 'Closed':
            return this.auth.hasAny(P.TicketsClose) || (requester && t.status === 'Resolved');
          case 'Cancelled':
            return agent || requester;
          case 'InProgress':
            return t.status === 'Resolved' ? agent || requester : agent;
          default:
            return agent;
        }
      })
      .map((to) => this.describe(to, t.status, requester && !agent));
  });

  protected readonly newComment = new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(4000)] });
  protected readonly internal = new FormControl(false, { nonNullable: true });
  protected readonly posting = signal(false);

  protected readonly canUploadDocs = computed(() => this.isRequester() || this.isAgent() || this.auth.hasAny(P.DocumentsManage));

  changeStatus(action: TransitionAction): void {
    const t = this.data();
    if (!t) return;
    const fields: FieldDef[] =
      action.status === 'Resolved'
        ? [{ key: 'resolutionNotes', label: 'Solucion aplicada', type: 'textarea', required: true, maxLength: 4000, hint: 'El solicitante la vera al confirmar.' }]
        : [{ key: 'comment', label: action.status === 'Cancelled' ? 'Motivo' : 'Comentario (opcional)', type: 'textarea', required: action.status === 'Cancelled', maxLength: 1000 }];

    this.dialogs
      .form({
        title: action.label,
        submitText: action.label,
        fields,
        save: (value) => this.tickets.changeStatus(t.id, { status: action.status, ...value }),
      })
      .subscribe(() => this.refresh(`Ticket ${enumLabel('TicketStatus', action.status).toLowerCase()}.`));
  }

  assignToMe(): void {
    const me = this.auth.user()?.id;
    const t = this.data();
    if (me && t) this.tickets.assign(t.id, me).subscribe(() => this.refresh('Ticket asignado a usted.'));
  }

  assign(): void {
    const t = this.data();
    if (!t) return;
    this.dialogs
      .form({
        title: 'Asignar ticket',
        submitText: 'Asignar',
        fields: [{ key: 'assignedToId', label: 'Tecnico', type: 'picker', required: true, wide: true, search: this.lookups.searchUsers }],
        save: (value) => this.tickets.assign(t.id, value['assignedToId'] as number),
      })
      .subscribe(() => this.refresh('Ticket asignado.'));
  }

  decide(approve: boolean): void {
    const t = this.data();
    if (!t) return;
    const action = (comment: string | null): Observable<TicketDetailDto> =>
      approve ? this.tickets.approve(t.id, comment) : this.tickets.reject(t.id, comment);
    this.confirmService
      .askReason({
        title: approve ? 'Aprobar solicitud' : 'Rechazar solicitud',
        message: approve ? 'La solicitud pasara a la cola de atencion.' : 'La solicitud se cerrara como rechazada y se notificara al solicitante.',
        confirmText: approve ? 'Aprobar' : 'Rechazar',
        destructive: !approve,
        reason: { label: approve ? 'Comentario (opcional)' : 'Motivo del rechazo', required: !approve },
      })
      .subscribe((comment) => {
        if (comment !== null) action(comment || null).subscribe(() => this.refresh(approve ? 'Solicitud aprobada.' : 'Solicitud rechazada.'));
      });
  }

  postComment(): void {
    const t = this.data();
    if (!t || this.newComment.invalid) {
      this.newComment.markAsTouched();
      return;
    }
    this.posting.set(true);
    this.tickets
      .addComment(t.id, { content: this.newComment.value.trim(), isInternal: this.isAgent() && this.internal.value })
      .subscribe({
        next: () => {
          this.posting.set(false);
          this.newComment.reset();
          this.internal.reset();
          this.commentsResource.reload();
          this.ticket.reload();
        },
        error: () => this.posting.set(false),
      });
  }

  private refresh(message: string): void {
    this.toast.success(message);
    this.ticket.reload();
    this.historyResource.reload();
    this.commentsResource.reload();
  }

  private describe(to: TicketStatus, from: TicketStatus, asRequester: boolean): TransitionAction {
    switch (to) {
      case 'Open':
        return { status: to, label: 'Abrir', icon: 'drafts' };
      case 'InProgress':
        return from === 'Resolved'
          ? { status: to, label: 'Reabrir', icon: 'replay' }
          : { status: to, label: 'Iniciar atencion', icon: 'play_arrow', primary: true };
      case 'WaitingUser':
        return { status: to, label: 'Esperar al usuario', icon: 'hourglass_empty' };
      case 'WaitingVendor':
        return { status: to, label: 'Esperar al proveedor', icon: 'local_shipping' };
      case 'Resolved':
        return { status: to, label: 'Resolver', icon: 'task_alt', primary: true };
      case 'Closed':
        return asRequester
          ? { status: to, label: 'Confirmar solucion', icon: 'thumb_up', primary: true }
          : { status: to, label: 'Cerrar', icon: 'lock' };
      case 'Cancelled':
        return { status: to, label: 'Cancelar ticket', icon: 'block' };
      default:
        return { status: to, label: enumLabel('TicketStatus', to), icon: 'arrow_forward' };
    }
  }
}
