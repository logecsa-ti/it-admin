import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '@core/auth/auth.service';
import { PERMISSIONS as P } from '@core/auth/permissions.generated';
import { TicketListItemDto } from '@core/models';
import { CellDefDirective, DataTableComponent, TableColumn } from '@shared/components/data-table/data-table.component';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { SearchBoxComponent } from '@shared/components/search-box/search-box.component';
import { StatusBadgeComponent } from '@shared/components/status-badge/status-badge.component';
import { CanDirective } from '@shared/directives/can.directive';
import { enumOptions } from '@shared/labels/enum-labels';
import { AppDatePipe } from '@shared/pipes/app-date.pipe';
import { ExportService } from '@shared/services/export.service';
import { pagedList } from '@shared/utils/paged-list';
import { TicketFilters, TicketsService } from '../tickets.service';

type Scope = 'all' | 'mine' | 'assigned';

@Component({
  selector: 'app-ticket-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    MatButtonModule,
    MatButtonToggleModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatMenuModule,
    MatSelectModule,
    MatTooltipModule,
    PageHeaderComponent,
    SearchBoxComponent,
    DataTableComponent,
    CellDefDirective,
    StatusBadgeComponent,
    CanDirective,
    AppDatePipe,
  ],
  template: `
    <div class="page">
      <app-page-header title="Tickets" [subtitle]="isAgent() ? 'Incidentes y solicitudes de servicio.' : 'Sus incidentes y solicitudes de servicio.'">
        <button mat-stroked-button type="button" [matMenuTriggerFor]="exportMenu" *appCan="P.ReportsExport" [disabled]="exporter.busy()">
          <mat-icon>download</mat-icon>Exportar
        </button>
        <mat-menu #exportMenu="matMenu">
          <button mat-menu-item type="button" (click)="export('xlsx')">Excel (.xlsx)</button>
          <button mat-menu-item type="button" (click)="export('csv')">CSV</button>
        </mat-menu>
        <a mat-flat-button routerLink="nuevo"><mat-icon>add</mat-icon>Nuevo ticket</a>
      </app-page-header>

      <section class="panel">
        <div class="filters">
          @if (isAgent()) {
            <mat-button-toggle-group [value]="scope()" (change)="setScope($event.value)" aria-label="Alcance">
              <mat-button-toggle value="all">Todos</mat-button-toggle>
              <mat-button-toggle value="assigned">Asignados a mi</mat-button-toggle>
              <mat-button-toggle value="mine">Creados por mi</mat-button-toggle>
            </mat-button-toggle-group>
          }
          <app-search-box placeholder="Numero o titulo" (search)="list.setSearch($event)" />
          <mat-form-field>
            <mat-label>Tipo</mat-label>
            <mat-select [value]="list.filters().type ?? null" (selectionChange)="list.patchFilters({ type: $event.value })">
              <mat-option [value]="null">Todos</mat-option>
              @for (o of types; track o.value) { <mat-option [value]="o.value">{{ o.label }}</mat-option> }
            </mat-select>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Estado</mat-label>
            <mat-select [value]="list.filters().status ?? null" (selectionChange)="list.patchFilters({ status: $event.value })">
              <mat-option [value]="null">Todos</mat-option>
              @for (o of statuses; track o.value) { <mat-option [value]="o.value">{{ o.label }}</mat-option> }
            </mat-select>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Prioridad</mat-label>
            <mat-select [value]="list.filters().priority ?? null" (selectionChange)="list.patchFilters({ priority: $event.value })">
              <mat-option [value]="null">Todas</mat-option>
              @for (o of priorities; track o.value) { <mat-option [value]="o.value">{{ o.label }}</mat-option> }
            </mat-select>
          </mat-form-field>
          @if (isAgent()) {
            <mat-checkbox [checked]="!!list.filters().overdue" (change)="list.patchFilters({ overdue: $event.checked || null })">Fuera de SLA</mat-checkbox>
          }
        </div>

        <app-data-table
          caption="Tickets"
          [columns]="columns()"
          [rows]="list.items()"
          [loading]="list.loading()"
          [total]="list.total()"
          [page]="list.page()"
          [pageSize]="list.pageSize()"
          [clickable]="true"
          (pageChange)="list.onPage($event)"
          (sortChange)="list.onSort($event)"
          (rowClick)="open($event)"
          emptyIcon="confirmation_number"
          emptyTitle="Sin tickets"
        >
          <ng-template appCell="number" let-row><span class="mono">{{ row.ticketNumber }}</span></ng-template>
          <ng-template appCell="title" let-row>
            <div class="title-cell">
              <span class="title">{{ row.title }}</span>
              <span class="subtle">{{ row.categoryName }}</span>
            </div>
          </ng-template>
          <ng-template appCell="type" let-row><app-status-badge kind="TicketType" [value]="row.type" /></ng-template>
          <ng-template appCell="priority" let-row><app-status-badge kind="TicketPriority" [value]="row.priority" /></ng-template>
          <ng-template appCell="status" let-row>
            <div class="status-cell">
              <app-status-badge kind="TicketStatus" [value]="row.status" />
              @if (row.approvalStatus === 'Pending') {
                <app-status-badge label="Por aprobar" tone="warning" />
              }
            </div>
          </ng-template>
          <ng-template appCell="due" let-row>
            @if (row.sla.dueAtResolution) {
              <span [class.breached]="row.sla.isSlaBreached" [matTooltip]="row.sla.isSlaBreached ? 'SLA vencido' : 'Vencimiento de resolucion'">
                @if (row.sla.isSlaBreached) { <mat-icon inline>timer_off</mat-icon> }
                {{ row.sla.dueAtResolution | appDate: 'short' }}
              </span>
            } @else {
              —
            }
          </ng-template>
          <ng-template appCell="created" let-row>{{ row.createdAt | appDate: 'relative' }}</ng-template>
        </app-data-table>
      </section>
    </div>
  `,
  styles: `
    .title-cell { display: flex; flex-direction: column; }
    .title { font-weight: 500; }
    .title-cell { min-width: 220px; }
    .status-cell { display: flex; gap: 4px; flex-wrap: wrap; }
    .breached { color: var(--app-tone-danger-fg); font-weight: 600; }
    span:has(> mat-icon), .due { white-space: nowrap; }
    mat-checkbox { margin-left: -8px; }
  `,
})
export class TicketListComponent implements OnInit {
  private readonly tickets = inject(TicketsService);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);
  protected readonly exporter = inject(ExportService);
  protected readonly P = P;

  /** Query params (enlaces del dashboard): ?mine=true, ?assignedToMe=true, ?overdue=true. */
  readonly mine = input<string>();
  readonly assignedToMe = input<string>();
  readonly overdue = input<string>();

  protected readonly types = enumOptions('TicketType');
  protected readonly statuses = enumOptions('TicketStatus');
  protected readonly priorities = enumOptions('TicketPriority');

  protected readonly isAgent = computed(() => this.auth.hasAny(P.TicketsUpdate));
  protected readonly list = pagedList<TicketListItemDto, TicketFilters>((page, filters) => this.tickets.list(page, filters), {});
  protected readonly scope = computed<Scope>(() =>
    this.list.filters().assignedToMe ? 'assigned' : this.list.filters().mine ? 'mine' : 'all',
  );

  protected readonly columns = computed<TableColumn<TicketListItemDto>[]>(() => [
    { key: 'number', header: 'Numero', sortKey: 'number', width: '140px' },
    { key: 'title', header: 'Titulo' },
    { key: 'type', header: 'Tipo', width: '120px', hideOnMobile: true },
    { key: 'priority', header: 'Prioridad', sortKey: 'priority', width: '110px' },
    { key: 'status', header: 'Estado', sortKey: 'status', width: '170px' },
    ...(this.isAgent()
      ? [
          { key: 'requester', header: 'Solicitante', value: (t: TicketListItemDto) => t.requesterName, hideOnMobile: true },
          { key: 'assignee', header: 'Asignado a', value: (t: TicketListItemDto) => t.assignedToName ?? 'Sin asignar', hideOnMobile: true },
        ]
      : []),
    { key: 'due', header: 'Vence', sortKey: 'dueAtResolution', width: '150px', hideOnMobile: true },
    { key: 'created', header: 'Creado', width: '120px', hideOnMobile: true },
  ]);

  ngOnInit(): void {
    this.list.patchFilters({
      mine: this.mine() === 'true' || null,
      assignedToMe: this.assignedToMe() === 'true' || null,
      overdue: this.overdue() === 'true' || null,
    });
  }

  setScope(scope: Scope): void {
    this.list.patchFilters({ mine: scope === 'mine' || null, assignedToMe: scope === 'assigned' || null });
  }

  open(ticket: TicketListItemDto): void {
    void this.router.navigate(['/tickets', ticket.id]);
  }

  export(format: 'xlsx' | 'csv'): void {
    this.exporter.export('tickets', format, this.list.filters());
  }
}
