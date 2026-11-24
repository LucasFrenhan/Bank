import { expect, test } from '@playwright/test';
import { contaBancariaPelaApi, cpf, criarContaPelaApi, email, entrar, SENHA } from './apoio';

// Spec 007 FR-029 / SC-007 — transferência sem dados pessoais, valor, descrição nem chave de
// idempotência na telemetria; a navegação do comprovante aparece pelo padrão da rota.
test.describe('Telemetria — transferências', () => {
  test.skip(({ isMobile }) => isMobile, 'verificado no desktop');

  test('transferência e comprovante sem dados sensíveis nos sinais', async ({ page }) => {
    const corpos: string[] = [];
    page.on('request', (requisicao) => {
      if (new URL(requisicao.url()).pathname.startsWith('/otlp/')) {
        corpos.push(requisicao.postDataBuffer()?.toString('latin1') ?? '');
      }
    });

    const pagador = await criarContaPelaApi(page, 'PessoaFisica', 'Lucas Telemetria');
    const recebedor = await criarContaPelaApi(page, 'PessoaJuridica', 'Padaria Sigilo Ltda');
    const descricao = `Descricao-secreta-${Date.now()}`;
    await entrar(page, pagador);

    await page.goto('/transferir');
    await page.getByLabel('CPF ou CNPJ de quem vai receber').fill(recebedor.documento);
    await expect(page.getByRole('group', { name: 'Destinatário' })).toBeVisible();
    await page.getByLabel('Valor').fill('6,17');
    await page.getByLabel('Descrição (opcional)').fill(descricao);
    await page.getByRole('button', { name: 'Continuar' }).click();
    const chave = await page.evaluate(() => (JSON.parse(sessionStorage.getItem('bank.transferencia')!) as { chave: string }).chave);
    await page.getByRole('button', { name: /^Transferir R\$/ }).click();
    await expect(page.getByRole('heading', { name: 'Transferência concluída' })).toBeVisible();
    // Reabre o comprovante pela consulta (spec 008), sem o cache da aba: a chamada também é coberta.
    await page.evaluate(() => sessionStorage.removeItem('bank.comprovantes'));
    await page.reload();
    await expect(page.getByRole('heading', { name: 'Transferência concluída' })).toBeVisible();

    await expect.poll(() => corpos.some((c) => c.includes('/transferencias/:id')), { timeout: 15_000 }).toBe(true);
    const sensiveis = [pagador.documento, recebedor.documento, 'Lucas Telemetria', 'Padaria Sigilo Ltda', descricao, chave, '6,17'];
    for (const sensivel of sensiveis) {
      expect(corpos.some((c) => c.includes(sensivel)), `telemetria contém "${sensivel.slice(0, 12)}…"`).toBe(false);
    }
    // O identificador da transferência não é dado pessoal (spec 008, FR-007): pode aparecer no
    // caminho da chamada GET /api/v1/transferencias/{id}; a navegação continua registrada pelo
    // padrão da rota (verificado acima).
  });
});

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

// Spec 013 FR-013 / SC-005 — exibir e copiar os dados da conta não os leva à telemetria.
test.describe('Telemetria — dados da conta', () => {
  test.skip(({ isMobile }) => isMobile, 'verificado no desktop');

  test('número da conta e atributos da conta bancária fora dos sinais', async ({ page, context }) => {
    await context.grantPermissions(['clipboard-read', 'clipboard-write']);
    const corpos: string[] = [];
    page.on('request', (requisicao) => {
      if (new URL(requisicao.url()).pathname.startsWith('/otlp/')) {
        corpos.push(requisicao.postDataBuffer()?.toString('latin1') ?? '');
      }
    });

    const conta = await criarContaPelaApi(page);
    const { numero, digito } = await contaBancariaPelaApi(page, conta);
    await entrar(page, conta);
    await page.getByRole('button', { name: 'Copiar dados da conta' }).click();
    await expect(page.getByText('Dados da conta copiados')).toBeVisible();

    // Espera o span da consulta da carteira (a origem dos dados) e, depois da cópia, mais um envio.
    await expect.poll(() => corpos.some((c) => c.includes('/carteiras/minha')), { timeout: 15_000 }).toBe(true);
    const enviados = corpos.length;
    await expect.poll(() => corpos.length, { timeout: 15_000 }).toBeGreaterThan(enviados);

    // Número delimitado por não dígitos: não confunde com trechos de timestamps.
    const proibidos = [new RegExp(`(?<!\\d)${numero}(?!\\d)`), new RegExp(`${numero}-${digito}`), /ag[eê]ncia/i, /contaBancaria/i];
    for (const proibido of proibidos) {
      expect(corpos.some((c) => proibido.test(c)), `telemetria contém ${proibido}`).toBe(false);
    }
  });
});
