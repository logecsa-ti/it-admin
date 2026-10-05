/**
 * Valor de un resource sin lanzar: `resource.value()` lanza si la carga fallo (el error ya se mostro
 * como toast); las plantillas leen este valor para degradar a "sin datos" en vez de romper la vista.
 */
export function valueOf<T>(resource: { hasValue(): boolean; value(): T }): T | undefined {
  return resource.hasValue() ? resource.value() : undefined;
}
