// Actualiza openapi/tiadmin-api.json desde la API local en ejecucion (Development expone /swagger).
// Uso: npm run api:snapshot && npm run api:types
import { writeFile } from 'node:fs/promises';

const url = process.env.TIADMIN_OPENAPI_URL ?? 'http://localhost:5142/swagger/v1/swagger.json';
const response = await fetch(url);
if (!response.ok) {
  console.error(`No se pudo obtener ${url}: HTTP ${response.status}. ¿Esta la API ejecutandose en Development?`);
  process.exit(1);
}

const document = await response.json();
await writeFile('openapi/tiadmin-api.json', JSON.stringify(document, null, 2) + '\n');
console.log(`openapi/tiadmin-api.json actualizado (${Object.keys(document.paths).length} rutas).`);
