import type {
  ApprovalStatus,
  AssetMovementType,
  AssetStatus,
  AuditAction,
  ChangeImpact,
  ChangeRisk,
  ChangeStatus,
  ChangeType,
  ContractStatus,
  ContractType,
  ExportJobStatus,
  LicenseAlertType,
  LicenseType,
  MaintenanceAlertType,
  MaintenanceStatus,
  MaintenanceType,
  PurchaseStatus,
  TicketPriority,
  TicketStatus,
  TicketType,
  VendorStatus,
} from '@core/models';

/** Tono visual de un estado (badges): apagados y legibles sobre blanco. */
export type Tone = 'neutral' | 'info' | 'success' | 'warning' | 'danger' | 'accent';

interface EnumMeta<T extends string> {
  labels: Record<T, string>;
  tones?: Partial<Record<T, Tone>>;
}

const level = { Low: 'Baja', Medium: 'Media', High: 'Alta' } as const;
const levelTones: Partial<Record<'Low' | 'Medium' | 'High', Tone>> = {
  Low: 'neutral',
  Medium: 'warning',
  High: 'danger',
};

/** Etiquetas en espanol de los enums de la API (se serializan como texto, en ingles). */
export const ENUMS = {
  AssetStatus: {
    labels: {
      Available: 'Disponible',
      Assigned: 'Asignado',
      Maintenance: 'En mantenimiento',
      Repair: 'En reparacion',
      Retired: 'Retirado',
      Lost: 'Extraviado',
      Disposed: 'Dado de baja',
    },
    tones: {
      Available: 'success',
      Assigned: 'info',
      Maintenance: 'warning',
      Repair: 'warning',
      Retired: 'neutral',
      Lost: 'danger',
      Disposed: 'neutral',
    },
  } satisfies EnumMeta<AssetStatus>,
  AssetMovementType: {
    labels: {
      Assignment: 'Asignacion',
      Return: 'Devolucion',
      LocationChange: 'Cambio de ubicacion',
      DepartmentChange: 'Cambio de departamento',
      StatusChange: 'Cambio de estado',
      Transfer: 'Transferencia',
      Repair: 'Reparacion',
      Disposal: 'Baja',
    },
  } satisfies EnumMeta<AssetMovementType>,
  TicketStatus: {
    labels: {
      New: 'Nuevo',
      Open: 'Abierto',
      InProgress: 'En progreso',
      WaitingUser: 'Esperando usuario',
      WaitingVendor: 'Esperando proveedor',
      Resolved: 'Resuelto',
      Closed: 'Cerrado',
      Cancelled: 'Cancelado',
    },
    tones: {
      New: 'info',
      Open: 'info',
      InProgress: 'accent',
      WaitingUser: 'warning',
      WaitingVendor: 'warning',
      Resolved: 'success',
      Closed: 'neutral',
      Cancelled: 'neutral',
    },
  } satisfies EnumMeta<TicketStatus>,
  TicketPriority: {
    labels: { Low: 'Baja', Medium: 'Media', High: 'Alta', Critical: 'Critica' },
    tones: { Low: 'neutral', Medium: 'info', High: 'warning', Critical: 'danger' },
  } satisfies EnumMeta<TicketPriority>,
  TicketType: {
    labels: { Incident: 'Incidente', ServiceRequest: 'Solicitud' },
    tones: { Incident: 'accent', ServiceRequest: 'info' },
  } satisfies EnumMeta<TicketType>,
  ApprovalStatus: {
    labels: { Pending: 'Pendiente', Approved: 'Aprobada', Rejected: 'Rechazada' },
    tones: { Pending: 'warning', Approved: 'success', Rejected: 'danger' },
  } satisfies EnumMeta<ApprovalStatus>,
  LicenseType: {
    labels: {
      Perpetual: 'Perpetua',
      Subscription: 'Suscripcion',
      Trial: 'Prueba',
      OpenSource: 'Codigo abierto',
      Oem: 'OEM',
    },
  } satisfies EnumMeta<LicenseType>,
  LicenseAlertType: {
    labels: {
      ExpiringSoon: 'Por vencer',
      Expired: 'Vencida',
      Exhausted: 'Sin puestos',
      LowUtilization: 'Baja utilizacion',
    },
    tones: { ExpiringSoon: 'warning', Expired: 'danger', Exhausted: 'danger', LowUtilization: 'info' },
  } satisfies EnumMeta<LicenseAlertType>,
  ContractStatus: {
    labels: {
      Draft: 'Borrador',
      Active: 'Activo',
      Expiring: 'Por vencer',
      Expired: 'Vencido',
      Terminated: 'Terminado',
      Renewed: 'Renovado',
    },
    tones: {
      Draft: 'neutral',
      Active: 'success',
      Expiring: 'warning',
      Expired: 'danger',
      Terminated: 'neutral',
      Renewed: 'info',
    },
  } satisfies EnumMeta<ContractStatus>,
  ContractType: {
    labels: {
      Purchase: 'Compra',
      Service: 'Servicio',
      Maintenance: 'Mantenimiento',
      Subscription: 'Suscripcion',
      Lease: 'Arrendamiento',
      Support: 'Soporte',
    },
  } satisfies EnumMeta<ContractType>,
  VendorStatus: {
    labels: { Active: 'Activo', Inactive: 'Inactivo', Blocked: 'Bloqueado' },
    tones: { Active: 'success', Inactive: 'neutral', Blocked: 'danger' },
  } satisfies EnumMeta<VendorStatus>,
  MaintenanceStatus: {
    labels: {
      Planned: 'Planificado',
      Scheduled: 'Programado',
      InProgress: 'En progreso',
      Completed: 'Completado',
      Cancelled: 'Cancelado',
    },
    tones: {
      Planned: 'neutral',
      Scheduled: 'info',
      InProgress: 'accent',
      Completed: 'success',
      Cancelled: 'neutral',
    },
  } satisfies EnumMeta<MaintenanceStatus>,
  MaintenanceType: {
    labels: { Preventive: 'Preventivo', Corrective: 'Correctivo', Emergency: 'Emergencia' },
    tones: { Preventive: 'info', Corrective: 'warning', Emergency: 'danger' },
  } satisfies EnumMeta<MaintenanceType>,
  MaintenanceAlertType: {
    labels: { Upcoming: 'Proximo', Overdue: 'Vencido', PreventiveDue: 'Preventivo pendiente' },
    tones: { Upcoming: 'info', Overdue: 'danger', PreventiveDue: 'warning' },
  } satisfies EnumMeta<MaintenanceAlertType>,
  ChangeStatus: {
    labels: {
      Draft: 'Borrador',
      Requested: 'Solicitado',
      UnderReview: 'En revision',
      Approved: 'Aprobado',
      Rejected: 'Rechazado',
      Implementing: 'En implementacion',
      Completed: 'Completado',
      RolledBack: 'Revertido',
      Closed: 'Cerrado',
    },
    tones: {
      Draft: 'neutral',
      Requested: 'info',
      UnderReview: 'warning',
      Approved: 'success',
      Rejected: 'danger',
      Implementing: 'accent',
      Completed: 'success',
      RolledBack: 'danger',
      Closed: 'neutral',
    },
  } satisfies EnumMeta<ChangeStatus>,
  ChangeType: {
    labels: { Standard: 'Estandar', Normal: 'Normal', Emergency: 'Emergencia' },
    tones: { Standard: 'neutral', Normal: 'info', Emergency: 'danger' },
  } satisfies EnumMeta<ChangeType>,
  ChangeRisk: { labels: level, tones: levelTones } satisfies EnumMeta<ChangeRisk>,
  ChangeImpact: { labels: level, tones: levelTones } satisfies EnumMeta<ChangeImpact>,
  PurchaseStatus: {
    labels: {
      Draft: 'Borrador',
      Submitted: 'Enviada',
      Approved: 'Aprobada',
      Rejected: 'Rechazada',
      Ordered: 'Ordenada',
      Received: 'Recibida',
      Cancelled: 'Cancelada',
    },
    tones: {
      Draft: 'neutral',
      Submitted: 'info',
      Approved: 'success',
      Rejected: 'danger',
      Ordered: 'accent',
      Received: 'success',
      Cancelled: 'neutral',
    },
  } satisfies EnumMeta<PurchaseStatus>,
  AuditAction: {
    labels: {
      Create: 'Creacion',
      Update: 'Modificacion',
      Delete: 'Eliminacion',
      Login: 'Inicio de sesion',
      Logout: 'Cierre de sesion',
      Export: 'Exportacion',
      AccessDenied: 'Acceso denegado',
      SensitiveRead: 'Lectura sensible',
    },
    tones: {
      Create: 'success',
      Update: 'info',
      Delete: 'danger',
      Login: 'neutral',
      Logout: 'neutral',
      Export: 'accent',
      AccessDenied: 'danger',
      SensitiveRead: 'warning',
    },
  } satisfies EnumMeta<AuditAction>,
  ExportJobStatus: {
    labels: { Pending: 'Pendiente', Processing: 'Procesando', Completed: 'Lista', Failed: 'Fallida' },
    tones: { Pending: 'neutral', Processing: 'info', Completed: 'success', Failed: 'danger' },
  } satisfies EnumMeta<ExportJobStatus>,
} as const;

export type EnumName = keyof typeof ENUMS;

export function enumLabel(kind: EnumName, value: string | null | undefined): string {
  if (!value) {
    return '—';
  }
  return (ENUMS[kind].labels as Record<string, string>)[value] ?? value;
}

export function enumTone(kind: EnumName, value: string | null | undefined): Tone {
  const tones = ('tones' in ENUMS[kind] ? ENUMS[kind].tones : {}) as Record<string, Tone>;
  return (value && tones[value]) || 'neutral';
}

/** Opciones para selects: [{ value, label }] en el orden declarado. */
export function enumOptions(kind: EnumName): { value: string; label: string }[] {
  return Object.entries(ENUMS[kind].labels as Record<string, string>).map(([value, label]) => ({
    value,
    label,
  }));
}
