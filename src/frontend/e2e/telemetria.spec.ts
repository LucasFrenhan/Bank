import { expect, test } from '@playwright/test';
import { cpf, email, SENHA } from './apoio';

// SC-008 / FR-019a–c — traces e métricas do navegador chegam ao coletor (aceitos), sem dados
// sensíveis, e a chamada à API leva o traceparent (requer o Compose, que liga a telemetria).
test.describe('Telemetria', () => {
  test.skip(({ isMobile }) => isMobile, 'verificado no desktop');

  test('cadastro e login: traces e métricas aceitos pelo coletor, sem dados sensíveis e com traceparent', async ({ page }) => {
    const corpos: string[] = [];
    const respostasOtlp: string[] = [];
    const traceparents: string[] = [];
    page.on('request', (requisicao) => {
      const url = new URL(requisicao.url());
      if (url.pathname.startsWith('/otlp/')) {
        corpos.push(requisicao.postDataBuffer()?.toString('latin1') ?? '');
      }
      if (url.pathname === '/api/v1/autenticacao/login') {
        traceparents.push(requisicao.headers()['traceparent'] ?? '');
      }
    });
    page.on('response', (resposta) => {
      const url = new URL(resposta.url());
      if (url.pathname.startsWith('/otlp/')) {
        respostasOtlp.push(`${url.pathname} ${resposta.status()}`);
      }
    });

    const dados = { email: email(), documento: cpf() };
    await page.goto('/criar-conta');
    await page.getByLabel('Nome completo').fill('Lucas Silva');
    await page.getByLabel('CPF', { exact: true }).fill(dados.documento);
    await page.getByLabel('E-mail').fill(dados.email);
    await page.getByLabel('Senha', { exact: true }).fill(SENHA);
    await page.getByRole('button', { name: 'Criar conta' }).click();
    await expect(page.getByRole('heading', { name: 'Olá, Lucas' })).toBeVisible();
    const token = await page.evaluate(() => (JSON.parse(sessionStorage.getItem('bank.sessao')!) as { token: string }).token);

    // Exportação periódica de métricas a cada 5 s; traces em lote.
    await expect.poll(() => respostasOtlp.filter((r) => r.startsWith('/otlp/v1/metrics')).length, { timeout: 15_000 }).toBeGreaterThan(0);
    await expect.poll(() => respostasOtlp.filter((r) => r.startsWith('/otlp/v1/traces')).length, { timeout: 15_000 }).toBeGreaterThan(0);

    expect(respostasOtlp.every((r) => r.endsWith(' 200')), respostasOtlp.join(', ')).toBe(true);
    expect(corpos.some((c) => c.includes('bank.frontend.carregamento'))).toBe(true);
    expect(traceparents[0]).toMatch(/^00-[0-9a-f]{32}-[0-9a-f]{16}-01$/);
    for (const sensivel of [SENHA, dados.email, dados.documento, token.split('.')[1]!]) {
      expect(corpos.some((c) => c.includes(sensivel)), `telemetria contém "${sensivel.slice(0, 12)}…"`).toBe(false);
    }
  });
});
