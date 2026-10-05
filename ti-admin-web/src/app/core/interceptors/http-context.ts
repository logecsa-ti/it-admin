import { HttpContextToken } from '@angular/common/http';

/** La pagina maneja el error por su cuenta (p. ej. login, validaciones de formulario): sin toast global. */
export const SILENT_ERRORS = new HttpContextToken<boolean>(() => false);

/** No mostrar la barra de progreso global (sondeos en segundo plano). */
export const BACKGROUND_REQUEST = new HttpContextToken<boolean>(() => false);
