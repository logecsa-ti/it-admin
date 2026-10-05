// Genera src/app/core/auth/permissions.generated.ts desde el catalogo del backend
// (src/TIAdmin.Application/Common/Constants/Permissions.cs), unica fuente de verdad.
// Uso: npm run api:permissions
import { readFile, writeFile } from 'node:fs/promises';

const source = await readFile('../src/TIAdmin.Application/Common/Constants/Permissions.cs', 'utf8');
const entries = [...source.matchAll(/public const string (\w+) = "([A-Z_]+\.[A-Z_]+)";/g)];
if (entries.length === 0) {
  console.error('No se encontraron permisos en Permissions.cs');
  process.exit(1);
}

const body = entries.map(([, name, code]) => `  ${name}: '${code}',`).join('\n');
const output = `// Generado por scripts/sync-permissions.mjs desde Permissions.cs. No editar a mano.
export const PERMISSIONS = {
${body}
} as const;

export type PermissionCode = (typeof PERMISSIONS)[keyof typeof PERMISSIONS];
`;

await writeFile('src/app/core/auth/permissions.generated.ts', output);
console.log(`permissions.generated.ts: ${entries.length} permisos.`);
