import { expect, test, type Locator, type Page } from '@playwright/test';
import { entrar, semRolagemHorizontal, semViolacoesA11y } from './apoio';
import { comprar, criarPf, criarPfComCartao, criarPj, emitirCobranca, prepararFatura } from './apoioCartao';

// Spec 015 US7 — telas novas de 320 a 1920 px, com axe e só pelo teclado (FR-025, SC-007).
const LARGURAS = [320, 390, 768, 1024, 1440, 1920];

async function alvosDeToqueAdequados(page: Page): Promise<void> {
  const pequenos = await page.evaluate(() =>
    Array.from(document.querySelectorAll<HTMLElement>('a, button, input, textarea'))
      .filter((el) => el.offsetParent !== null && !el.classList.contains('apenas-leitor-de-tela'))
      .filter((el) => !(el.tagName === 'A' && el.closest('p')))
      .filter((el) => el.getAttribute('href') !== '#conteudo')
      .map((el) => ({ el, r: el.getBoundingClientRect() }))
      .filter(({ r }) => r.width < 43.5 || r.height < 43.5)
      .map(({ el, r }) => `${el.tagName} "${(el.textContent ?? '').trim().slice(0, 30)}" ${Math.round(r.width)}x${Math.round(r.height)}`),
  );
  expect(pequenos).toEqual([]);
}

/** Em todas as larguras: sem rolagem horizontal, sem corte, alvos adequados e axe. */
async function verificar(page: Page): Promise<void> {
  for (const largura of LARGURAS) {
    await page.setViewportSize({ width: largura, height: 900 });
    await semRolagemHorizontal(page);
    await alvosDeToqueAdequados(page);
    await semViolacoesA11y(page);
  }
  await page.setViewportSize({ width: 1440, height: 900 });
}

const NOME_LONGO = 'Cafeteria '.repeat(15).trim().slice(0, 150);
const DESCRICAO_LONGA = 'Descrição comprida de cobrança '.repeat(10).slice(0, 200).trim();

test.describe('Telas de cartão — larguras e acessibilidade', () => {
  test.skip(({ isMobile }) => isMobile, 'executado só no projeto desktop');
  test.setTimeout(180_000);

  test('PF: solicitação, cartão, pagamento, comprovante e faturas', async ({ page }) => {
    const pj = await criarPj(page, NOME_LONGO);
    const nova = await criarPf(page);
    await entrar(page, nova);
    await page.goto('/cartao');
    await page.getByRole('button', { name: 'Solicitar cartão' }).click();
    await expect(page.getByRole('alert')).toBeVisible();
    await verificar(page);
    await page.evaluate(() => sessionStorage.clear());

    const cartao = await criarPfComCartao(page, 'Maria '.repeat(25).trim());
    await comprar(page, cartao, pj, 4321.09, DESCRICAO_LONGA);
    const competencia = prepararFatura(cartao.pf.email, 'Vencida');
    await entrar(page, cartao.pf);
    await expect(page.getByRole('region', { name: 'Aviso de fatura' })).toBeVisible();
    await verificar(page);

    await page.goto('/cartao');
    await expect(page.getByRole('heading', { name: /^Fatura de/ })).toBeVisible();
    await page.getByRole('button', { name: 'Exibir dados do cartão' }).click();
    await expect(page.getByRole('button', { name: 'Copiar número' })).toBeVisible();
    await verificar(page);

    await page.goto('/cartao/faturas');
    await expect(page.getByText('Vencida', { exact: true })).toBeVisible();
    await verificar(page);

    await page.goto(`/cartao/faturas/${competencia}`);
    await page.getByRole('button', { name: 'Pagar fatura' }).click();
    await expect(page.getByText(/Você tem R\$/)).toBeVisible();
    await verificar(page);

    const { codigo } = await emitirCobranca(page, pj, 12345678.9, DESCRICAO_LONGA);
    await page.goto(`/c/${codigo}`);
    await page.getByRole('button', { name: /^Pagar R\$/ }).click();
    await expect(page.getByText('Confira o número do cartão')).toBeVisible();
    await verificar(page);
    await page.getByRole('button', { name: 'Usar meu cartão' }).click();
    await expect(page.getByLabel('Validade')).not.toHaveValue('');
    await page.getByLabel('CVV').fill(cartao.dados.cvv);
    await page.getByRole('button', { name: /^Pagar R\$/ }).click();
    await expect(page.getByText('Cartão bloqueado por fatura vencida')).toBeVisible();
    await verificar(page);

    await page.evaluate(() => sessionStorage.clear());
    const outro = await criarPfComCartao(page, 'João Comprovante');
    const paga = await comprar(page, outro, pj, 4999.99, DESCRICAO_LONGA);
    await entrar(page, outro.pf);
    await page.goto(`/c/${paga.codigo}`);
    await expect(page.getByRole('heading', { name: 'Pagamento aprovado' })).toBeVisible();
    await verificar(page);
    await page.goto('/c/2222-2222');
    await expect(page.getByText('Cobrança não encontrada')).toBeVisible();
    await verificar(page);
    await page.goto('/cobrancas');
    await expect(page.getByText('Área indisponível para o seu tipo de conta')).toBeVisible();
    await verificar(page);
  });

  test('PJ: lista, emissão, detalhe, cancelamento e antecipação', async ({ page }) => {
    const pj = await criarPj(page, NOME_LONGO);
    const cartao = await criarPfComCartao(page);
    await comprar(page, cartao, pj, 4321.09, DESCRICAO_LONGA);
    const { codigo } = await emitirCobranca(page, pj, 1234567.89, DESCRICAO_LONGA);
    await entrar(page, pj);

    await page.goto('/cobrancas');
    await expect(page.getByText('Pendente', { exact: true })).toBeVisible();
    await verificar(page);
    await page.goto('/cobrancas/nova');
    await page.getByRole('button', { name: 'Emitir cobrança' }).click();
    await expect(page.getByText('Informe a descrição da cobrança')).toBeVisible();
    await verificar(page);
    await page.goto(`/c/${codigo}`);
    await page.getByRole('button', { name: 'Cancelar cobrança' }).click();
    await expect(page.getByRole('region', { name: 'Cancelar esta cobrança?' })).toBeVisible();
    await verificar(page);
    await page.goto('/cobrancas/recebiveis');
    await page.getByRole('button', { name: /^Antecipar / }).click();
    await expect(page.getByRole('region', { name: 'Antecipar este recebível' })).toBeVisible();
    await verificar(page);
  });
});

async function tabAte(page: Page, alvo: Locator, limite = 40): Promise<void> {
  for (let i = 0; i < limite && !(await alvo.evaluate((el) => el === document.activeElement)); i++) {
    await page.keyboard.press('Tab');
  }
  await expect(alvo).toBeFocused();
}

async function focoVisivel(alvo: Locator): Promise<void> {
  expect(await alvo.evaluate((el) => getComputedStyle(el).outlineStyle), 'foco visível').not.toBe('none');
}

test.describe('Telas de cartão — teclado', () => {
  test.skip(({ isMobile }) => isMobile, 'navegação por teclado verificada no desktop');

  test('PF: solicita, paga a cobrança e paga a fatura só pelo teclado', async ({ page }) => {
    const pj = await criarPj(page);
    const pf = await criarPf(page, 'Lucas Teclado');
    await entrar(page, pf);
    await page.goto('/cartao');
    await expect(page.getByRole('heading', { name: 'Peça o seu cartão de crédito' })).toBeVisible();
    await tabAte(page, page.getByRole('radio', { name: 'Visa' }));
    await page.keyboard.press('Space');
    await expect(page.getByRole('radio', { name: 'Visa' })).toBeChecked();
    const solicitar = page.getByRole('button', { name: 'Solicitar cartão Visa' });
    await tabAte(page, solicitar);
    await focoVisivel(solicitar);
    await page.keyboard.press('Enter');

    const olho = page.getByRole('button', { name: 'Exibir dados do cartão' });
    await expect(olho).toBeVisible();
    await tabAte(page, olho);
    await focoVisivel(olho);
    await page.keyboard.press('Enter');
    await expect(page.getByRole('button', { name: 'Ocultar dados do cartão' })).toBeFocused();
    const cvv = (await page.getByRole('region', { name: 'Meu cartão' }).getByText(/^\d{3}$/).textContent())!;
    await page.keyboard.press('Enter');

    const { codigo } = await emitirCobranca(page, pj, 5);
    await page.goto(`/c/${codigo}`);
    await expect(page.getByRole('button', { name: 'Usar meu cartão' })).toBeVisible();
    await tabAte(page, page.getByRole('button', { name: 'Usar meu cartão' }));
    await page.keyboard.press('Enter');
    await expect(page.getByLabel('CVV')).toBeFocused();
    await page.keyboard.type(cvv);
    await page.keyboard.press('Enter');
    await expect(page.getByRole('heading', { name: 'Pagamento aprovado' })).toBeFocused();

    const competencia = prepararFatura(pf.email, 'Disponivel');
    await page.goto(`/cartao/faturas/${competencia}`);
    await expect(page.getByRole('button', { name: 'Pagar fatura' })).toBeVisible();
    await tabAte(page, page.getByRole('button', { name: 'Pagar fatura' }));
    await page.keyboard.press('Enter');
    await expect(page.getByRole('heading', { name: 'Confira antes de pagar' })).toBeFocused();
    await expect(page.getByText('Saldo depois')).toBeVisible();
    await tabAte(page, page.getByRole('button', { name: /^Pagar R\$/ }));
    await page.keyboard.press('Enter');
    await expect(page.getByText('Paga', { exact: true })).toBeVisible();
    await expect(page.getByRole('heading', { level: 1 })).toBeFocused();
  });

  test('PJ: emite, cancela e antecipa só pelo teclado', async ({ page }) => {
    const pj = await criarPj(page);
    await comprar(page, await criarPfComCartao(page), pj, 100, 'Moedor');
    await entrar(page, pj);
    await page.goto('/cobrancas/nova');
    await tabAte(page, page.getByLabel('Valor'));
    await page.keyboard.type('12,50');
    await page.keyboard.press('Tab');
    await page.keyboard.type('Café coado');
    await tabAte(page, page.getByRole('button', { name: 'Emitir cobrança' }));
    await page.keyboard.press('Enter');

    const cancelar = page.getByRole('button', { name: 'Cancelar cobrança' });
    await expect(cancelar).toBeVisible();
    await tabAte(page, cancelar);
    await page.keyboard.press('Enter');
    await expect(page.getByRole('heading', { name: 'Cancelar esta cobrança?' })).toBeFocused();
    await page.keyboard.press('Tab');
    await page.keyboard.press('Enter');
    await expect(page.getByText('Cancelada', { exact: true })).toBeVisible();
    await expect(page.getByRole('heading', { level: 1 })).toBeFocused();

    await page.goto('/cobrancas/recebiveis');
    const antecipar = page.getByRole('button', { name: 'Antecipar Moedor' });
    await expect(antecipar).toBeVisible();
    await tabAte(page, antecipar);
    await page.keyboard.press('Enter');
    await expect(page.getByRole('heading', { name: 'Antecipar este recebível' })).toBeFocused();
    await page.keyboard.press('Tab');
    await page.keyboard.press('Enter');
    await expect(page.getByText('Antecipado', { exact: true })).toBeVisible();
  });
});
