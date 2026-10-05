import { ENUMS, enumLabel, enumOptions, enumTone } from './enum-labels';
import api from '../../../../openapi/tiadmin-api.json';

describe('enum-labels', () => {
  it('traduce valores y conserva desconocidos', () => {
    expect(enumLabel('TicketStatus', 'WaitingUser')).toBe('Esperando usuario');
    expect(enumLabel('TicketStatus', 'Nuevo valor')).toBe('Nuevo valor');
    expect(enumLabel('TicketStatus', null)).toBe('—');
    expect(enumTone('TicketPriority', 'Critical')).toBe('danger');
  });

  it('cubre todos los valores que publica la API (contrato OpenAPI)', () => {
    const schemas = api.components.schemas as Record<string, { enum?: string[] }>;
    for (const [name, meta] of Object.entries(ENUMS)) {
      const values = schemas[name]?.enum ?? [];
      expect(values.length, `${name} no existe en la API`).toBeGreaterThan(0);
      expect(Object.keys(meta.labels).sort(), name).toEqual([...values].sort());
    }
  });

  it('genera opciones en el orden declarado', () => {
    expect(enumOptions('ApprovalStatus').map((o) => o.value)).toEqual(['Pending', 'Approved', 'Rejected']);
  });
});
