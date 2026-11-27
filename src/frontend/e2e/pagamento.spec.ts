import { expect, test, type Page } from '@playwright/test';
import { encerrarSessaoDoNavegador, SENHA } from './apoio';
import { comprar, criarPf, criarPfComCartao, criarPj, emitirCobranca, limiteDisponivel, type CartaoPf } from './apoioCartao';

// Spec 015 US3 — pagar uma cobrança com o cartão.

/** Abre o endereço sem sessão, entra e volta à cobrança. */
async function abrirPeloLink(page: Page, cartao: Pick<CartaoPf, 'pf'>, caminho: string): Promise<void> {
  await page.goto(caminho);
  await page.getByLabel('E-mail').fill(cartao.pf.email);
  await page.getByLabel('Senha', { exact: true }).fill(SENHA);
  await page.getByRole('button', { name: 'Entrar' }).click();
  await expect(page).toHaveURL(new RegExp(`${caminho}$`));
}

async function preencher(page: Page, { dados }: CartaoPf, cvv = dados.cvv): Promise<void> {
  await page.getByRole('button', { name: 'Usar meu cartão' }).click();
  await expect(page.getByLabel('CVV')).toBeFocused();
  await expect(page.getByLabel('CVV')).toHaveValue('');
  await page.getByLabel('CVV').fill(cvv);
}

test.describe('Pagamento com cartão', () => {
  test('do link ao comprovante, que pode ser reaberto', async ({ page }) => {
    const pj = await criarPj(page);
    const cartao = await criarPfComCartao(page);
    const { codigo } = await emitirCobranca(page, pj, 299.9, 'Kit 3 Pacotes');
    await abrirPeloLink(page, cartao, `/c/${codigo.replace('-', '').toLowerCase()}`);

    await expect(page.getByRole('region', { name: 'Cobrança' })).toContainText('Cafeteria Grão Ltda');
    await preencher(page, cartao);
    await page.getByRole('button', { name: /^Pagar R\$\s299,90$/ }).click();
    await expect(page.getByRole('heading', { name: 'Pagamento aprovado' })).toBeFocused();
    await expect(page.getByRole('region', { name: 'Comprovante' })).toContainText(`Visa final ${cartao.dados.numero.slice(-4)}`);
    expect(await limiteDisponivel(page, cartao.pf)).toBe(4700.1);

    await page.goto(`/c/${codigo}`);
    await expect(page.getByRole('heading', { name: 'Pagamento aprovado' })).toBeVisible();
    await page.getByRole('link', { name: 'Voltar ao cartão' }).last().click();
    await expect(page.getByRole('region', { name: 'Limite', exact: true })).toContainText('R$ 4.700,10');
  });

  test('CVV errado, dados inválidos e limite insuficiente', async ({ page }) => {
    const pj = await criarPj(page);
    const cartao = await criarPfComCartao(page);
    const { codigo } = await emitirCobranca(page, pj, 5000.01, 'Máquina de café');
    await abrirPeloLink(page, cartao, `/c/${codigo}`);

    await page.getByRole('button', { name: /^Pagar R\$/ }).click();
    await expect(page.getByText('Confira o número do cartão')).toBeVisible();

    await preencher(page, cartao, cartao.dados.cvv === '000' ? '001' : '000');
    await page.getByRole('button', { name: /^Pagar R\$/ }).click();
    await expect(page.getByRole('alert')).toContainText('Cartão não autorizado');
    await expect(page.getByLabel('CVV')).toHaveValue('');
    await expect(page.getByLabel('CVV')).toBeFocused();

    await page.getByLabel('CVV').fill(cartao.dados.cvv);
    await page.getByRole('button', { name: /^Pagar R\$/ }).click();
    await expect(page.getByRole('alert')).toContainText('Você tem R$ 5.000,00 de limite disponível.');
    await page.getByRole('link', { name: 'Ver limite' }).click();
    await expect(page).toHaveURL(/\/cartao$/);
  });

  test('resposta perdida, recarga e novo envio: uma só compra', async ({ page }) => {
    const pj = await criarPj(page);
    const cartao = await criarPfComCartao(page);
    const { codigo } = await emitirCobranca(page, pj, 50);
    await abrirPeloLink(page, cartao, `/c/${codigo}`);

    const chaves: string[] = [];
    let perder = true;
    await page.route('**/pagar', async (rota) => {
      chaves.push(rota.request().headers()['idempotency-key'] ?? '');
      if (perder) {
        await rota.fetch(); // chega à API, mas a resposta se perde.
        await rota.abort('connectionreset');
      } else {
        await rota.continue();
      }
    });

    await preencher(page, cartao);
    await page.getByRole('button', { name: /^Pagar R\$/ }).click();
    await expect(page.getByRole('alert')).toContainText('Não foi possível conectar');
    perder = false;
    await page.getByRole('button', { name: 'Tentar de novo' }).click();
    await expect(page.getByRole('heading', { name: 'Pagamento aprovado' })).toBeVisible();
    expect(new Set(chaves).size).toBe(1);
    expect(chaves).toHaveLength(2);
    expect(await limiteDisponivel(page, cartao.pf)).toBe(4950);

    // Depois de uma resposta perdida, recarregar mostra o que o Bank registrou.
    await page.reload();
    await expect(page.getByRole('heading', { name: 'Pagamento aprovado' })).toBeVisible();
  });

  test('sem cartão, já paga por outra pessoa, outra PJ e código inexistente', async ({ page }) => {
    const pj = await criarPj(page);
    const semCartao = { pf: await criarPf(page, 'Maria Sem Cartão') };
    const pendente = await emitirCobranca(page, pj);
    const paga = await comprar(page, await criarPfComCartao(page), pj);

    await abrirPeloLink(page, semCartao, `/c/${pendente.codigo}`);
    await expect(page.getByText('Você ainda não tem um cartão')).toBeVisible();
    await expect(page.getByRole('link', { name: 'Solicitar cartão' })).toHaveAttribute('href', '/cartao');

    await page.goto(`/c/${paga.codigo}`);
    await expect(page.getByText('Esta cobrança não está mais disponível')).toBeVisible();
    await page.goto('/c/2222-2222');
    await expect(page.getByText('Cobrança não encontrada')).toBeVisible();

    await encerrarSessaoDoNavegador(page);
    const outra = await criarPj(page, 'Outra Loja Ltda');
    await abrirPeloLink(page, { pf: outra }, `/c/${pendente.codigo}`);
    await expect(page.getByText('Só contas Pessoa Física pagam cobranças')).toBeVisible();
  });
});
