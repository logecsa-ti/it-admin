import { Pipe, PipeTransform } from '@angular/core';
import { EnumName, enumLabel } from '@shared/labels/enum-labels';

/** `{{ ticket.status | enumLabel: 'TicketStatus' }}` → "En progreso". */
@Pipe({ name: 'enumLabel' })
export class EnumLabelPipe implements PipeTransform {
  transform(value: string | null | undefined, kind: EnumName): string {
    return enumLabel(kind, value);
  }
}
