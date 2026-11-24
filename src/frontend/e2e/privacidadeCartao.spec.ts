import { expect, test } from '@playwright/test';
import { entrar } from './apoio';
import { criarPfComCartao, criarPj, emitirCobranca } from './apoioCartao';

// Spec 015 SC-005 / FR-008 — número e CVV fora do armazenamento, do endereço, do histórico, do título e da telemetria.
test.describe('Privacidade do cartão', () => {
  test.skip(({ isMobile }) => isMobile, 'verificado no desktop');

  test('percorre cartão e pagamento sem deixar rastro', async ({ page }) => {
    const corposOtlp: string[] = [];
    const enderecos: string[] = [];
    const mensagens: string[] = [];
    page.on('console', (m) => mensagens.push(m.text()));
    page.on('request', (requisicao) => {
      const url = new URL(requisicao.url());
      if (url.pathname.startsWith('/otlp/')) corposOtlp.push(requisicao.postDataBuffer()?.toString('latin1') ?? '');
      else enderecos.push(requisicao.url());
    });
    page.on('framenavigated', (quadro) => enderecos.push(quadro.url()));

    const descricao = `Segredo-${Date.now()}`;
    const pj = await criarPj(page, 'Loja Sigilosa Ltda');
    const cartao = await criarPfComCartao(page, 'Titular Sigiloso');
    const { numero, cvv, validade } = cartao.dados;
    const { codigo } = await emitirCobranca(page, pj, 317.43, descricao);
    const guardados: string[] = [];
    const fotografar = async () => {
      guardados.push(
        await page.evaluate(() =>
          JSON.stringify([{ ...sessionStorage }, { ...localStorage }, history.state, location.href, document.title, document.cookie]),
        ),
      );
    };

    await entrar(page, cartao.pf);
    await page.goto('/cartao');
    await page.getByRole('button', { name: 'Exibir dados do cartão' }).click();
    await expect(page.getByRole('button', { name: 'Copiar número' })).toBeVisible();
    await fotografar();
    // Navegação dentro da aplicação: a telemetria já está carregada e registra a rota.
    await page.getByLabel('Link ou código da cobrança').fill(codigo);
    await page.getByRole('button', { name: 'Abrir' }).click();
    await page.getByRole('button', { name: 'Usar meu cartão' }).click();
    await expect(page.getByLabel('Validade')).toHaveValue(validade);
    await page.getByLabel('CVV').fill(cvv === '000' ? '001' : '000');
    await page.getByRole('button', { name: /^Pagar R\$/ }).click();
    await expect(page.getByRole('alert')).toContainText('Cartão não autorizado');
    await fotografar();
    const chaves = await page.evaluate(() => Object.values(JSON.parse(sessionStorage.getItem('bank.operacoes') ?? '{}') as Record<string, string>));
    expect(chaves).toHaveLength(1);
    await page.getByLabel('CVV').fill(cvv);
    await fotografar();
    await page.getByRole('button', { name: /^Pagar R\$/ }).click();
    await expect(page.getByRole('heading', { name: 'Pagamento aprovado' })).toBeVisible();
    await fotografar();
    // Sem recarregar: um carregamento novo descartaria o que a telemetria ainda não enviou.
    await page.getByRole('link', { name: 'Ver faturas' }).click();
    await expect(page.getByRole('region', { name: 'Lista de faturas' })).toBeVisible();

    // Espera a telemetria do pagamento e da navegação, e mais um envio.
    await expect.poll(() => corposOtlp.some((c) => c.includes('/c/:codigo')), { timeout: 20_000 }).toBe(true);
    await expect.poll(() => corposOtlp.some((c) => c.includes('/pagar')), { timeout: 20_000 }).toBe(true);
    const enviados = corposOtlp.length;
    await expect.poll(() => corposOtlp.length, { timeout: 20_000 }).toBeGreaterThan(enviados);

    const formatado = numero.replace(/(.{4})(?=.)/g, '$1 ');
    const separado = [...numero].join(' ');
    for (const proibido of [numero, formatado, separado]) {
      for (const [onde, textos] of Object.entries({ guardados, corposOtlp, enderecos, mensagens })) {
        expect(textos.some((t) => t.includes(proibido)), `${onde} contém o número`).toBe(false);
      }
    }
    // CVV e validade são curtos: procurados como valor isolado.
    const isolado = new RegExp(`(?<![\\w.-])${cvv}(?![\\w.-])`);
    for (const texto of [...guardados, ...enderecos]) expect(isolado.test(texto.replace(/"expiraEm":\d+/g, '')), 'CVV').toBe(false);
    for (const texto of [...guardados, ...corposOtlp, ...enderecos]) {
      expect(texto.includes(validade) || texto.includes(encodeURIComponent(validade)), 'validade').toBe(false);
    }
    for (const proibido of ['nomeTitularCartao', 'numeroCartao', '"cvv"', descricao, 'Loja Sigilosa', 'Titular Sigiloso', '317.43', '317,43', ...chaves]) {
      expect(corposOtlp.some((c) => c.includes(proibido)), `telemetria contém ${proibido}`).toBe(false);
    }
  });
});
