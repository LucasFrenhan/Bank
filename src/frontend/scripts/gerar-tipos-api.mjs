// Gera src/api/schema.d.ts a partir do OpenAPI da API em execução
// (specs/004-frontend-base, research R9). Uso: npm run api:tipos [BANK_API_URL=...]
import { writeFile } from 'node:fs/promises';
import openapiTS, { astToString } from 'openapi-typescript';

const apiUrl = process.env.BANK_API_URL ?? 'http://localhost:5080';
const origem = new URL('/openapi/v1.json', apiUrl);

const ast = await openapiTS(origem);
const cabecalho = `// Arquivo gerado por \`npm run api:tipos\` a partir de ${origem.pathname}. Não editar.\n`;
await writeFile(new URL('../src/api/schema.d.ts', import.meta.url), cabecalho + astToString(ast));
console.log(`Tipos gerados a partir de ${origem.href}`);
