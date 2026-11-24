import { NOME_BANCO } from './marca';

// O nome antigo ("Bank" sem "One") não pode voltar à interface (specs/013, SC-003; research R6).
// Fora da busca: testes e o schema gerado a partir da API.
const FONTES = import.meta.glob<string>(['./**/*.{ts,tsx}', '../index.html', '!./**/*.test.*', '!./api/schema.d.ts'], {
  query: '?raw',
  import: 'default',
  eager: true,
});

describe('nome do banco (specs/013, US2)', () => {
  it('é One Bank', () => {
    expect(NOME_BANCO).toBe('One Bank');
  });

  it('a busca cobre o código e o index.html, sem testes nem o schema gerado', () => {
    const arquivos = Object.keys(FONTES);
    expect(arquivos).toContain('../index.html');
    expect(arquivos).toContain('./design/componentes/Feedback.tsx');
    expect(arquivos.some((a) => a.includes('.test.') || a.includes('schema.d.ts'))).toBe(false);
  });

  it('nenhum arquivo usa o nome antigo', () => {
    const comNomeAntigo = Object.entries(FONTES)
      .filter(([, texto]) => /(?<!One )\bBank\b/.test(texto))
      .map(([arquivo]) => arquivo);
    expect(comNomeAntigo).toEqual([]);
  });

  it('o index.html traz o nome no título e na descrição', () => {
    const html = FONTES['../index.html'] ?? '';
    expect(html).toContain('<title>One Bank</title>');
    expect(html).toContain('content="One Bank — sua carteira digital, para você e para sua empresa."');
  });
});
