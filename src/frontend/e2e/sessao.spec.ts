import { expect, test, type Page } from '@playwright/test';
import { criarContaPelaApi, entrar, extratoPelaApi, semViolacoesA11y } from './apoio';

/**
 * Sessão (spec 004, US2; specs/016-sessao-refresh-token, US1, US2, US4). O ambiente de e2e usa prazos curtos
 * (.env.e2e.example): token de acesso de 10 s, inatividade de 25 s (aviso na metade) e tolerância de 5 s.
 */

const ROTA_RENOVAR = '**/api/v1/autenticacao/renovar';

/** Abre a rota e espera a renovação da carga; devolve o instante da resposta. */
async function abrirComRenovacao(page: Page, rota: string): Promise<number> {
  const renovacao = page.waitForResponse(ROTA_RENOVAR);
  await page.goto(rota);
  expect((await renovacao).status()).toBe(200);
  return Date.now();
}

test.describe('Sessão', () => {
  test('senha errada mostra a mensagem única', async ({ page }) => {
    const conta = await criarContaPelaApi(page);
    await page.goto('/entrar');
    await semViolacoesA11y(page);

    await entrar(page, conta, 'Errada@123');

    await expect(page.getByRole('alert')).toHaveText(/E-mail ou senha incorretos/);
  });

  test('rota protegida sem sessão vai ao login e volta para ela depois', async ({ page }) => {
    const conta = await criarContaPelaApi(page);

    await page.goto('/perfil');
    await expect(page).toHaveURL(/\/entrar$/);
    await page.getByLabel('E-mail').fill(conta.email);
    await page.getByLabel('Senha', { exact: true }).fill('SenhaForte@123');
    await page.getByRole('button', { name: 'Entrar' }).click();

    await expect(page).toHaveURL(/\/perfil$/);
    await expect(page.getByRole('heading', { name: 'Perfil' })).toBeVisible();
  });

  test('recarregar mantém a sessão; nova aba já entra; nenhum token no armazenamento; cookie HttpOnly', async ({ page, context }) => {
    const mensagensConsole: string[] = [];
    page.on('console', (m) => mensagensConsole.push(m.text()));
    const conta = await criarContaPelaApi(page);
    const login = page.waitForResponse('**/api/v1/autenticacao/login');
    await entrar(page, conta);
    const token = ((await (await login).json()) as { tokenAcesso: string }).tokenAcesso;
    await expect(page.getByRole('heading', { name: 'Olá, Lucas' })).toBeVisible();

    await page.reload();
    await expect(page.getByRole('heading', { name: 'Olá, Lucas' })).toBeVisible();

    const armazenado = await page.evaluate(() => JSON.stringify([{ ...sessionStorage }, { ...localStorage }, document.cookie]));
    expect(armazenado).not.toContain(token.split('.')[1]);
    expect(armazenado).not.toContain('bank_renovacao');
    expect(page.url()).not.toContain(token);
    expect(mensagensConsole.some((m) => m.includes(token))).toBe(false);

    const [cookie] = (await context.cookies()).filter((c) => c.name === 'bank_renovacao');
    expect(cookie).toMatchObject({ httpOnly: true, secure: true, sameSite: 'Strict', path: '/api/v1/autenticacao' });

    const outraAba = await context.newPage();
    await outraAba.goto('/');
    await expect(outraAba.getByRole('heading', { name: 'Olá, Lucas' })).toBeVisible();
    await expect(outraAba).toHaveURL(/\/$/);
  });

  test('a tela de carregamento da sessão é acessível', async ({ page }) => {
    const conta = await criarContaPelaApi(page);
    await entrar(page, conta);
    await page.route(ROTA_RENOVAR, async (rota) => {
      await new Promise((resolve) => setTimeout(resolve, 1500));
      await rota.continue();
    });

    await page.goto('/perfil');

    await expect(page.getByRole('status')).toHaveText('Carregando sua sessão…');
    await semViolacoesA11y(page);
    await expect(page.getByRole('heading', { name: 'Perfil' })).toBeVisible();
  });

  test('com o token vencido, a próxima ação renova sem voltar ao login', async ({ page }) => {
    const conta = await criarContaPelaApi(page);
    await entrar(page, conta);
    const renovadaEm = await abrirComRenovacao(page, '/');
    await expect(page.getByRole('heading', { name: 'Olá, Lucas' })).toBeVisible();
    const renovacoes: number[] = [];
    page.on('response', (r) => {
      if (r.url().endsWith('/api/v1/autenticacao/renovar')) renovacoes.push(r.status());
    });

    // Token de 10 s vencido, antes do aviso de inatividade (12,5 s).
    await page.waitForTimeout(Math.max(10_500 - (Date.now() - renovadaEm), 0));
    await page.getByRole('link', { name: 'Ver extrato' }).first().click();

    await expect(page).toHaveURL(/\/extrato/);
    await expect(page.getByRole('heading', { level: 1, name: 'Extrato' })).toBeVisible();
    await expect.poll(() => renovacoes).toEqual([200]);
  });

  test('transferência em revisão conclui uma única vez quando o token vence no envio', async ({ page }) => {
    const pagador = await criarContaPelaApi(page, 'PessoaFisica', 'Lucas Sessao');
    const recebedor = await criarContaPelaApi(page, 'PessoaJuridica', 'Loja Sessao Ltda');
    await entrar(page, pagador);
    await page.goto('/transferir');
    await page.getByLabel('CPF ou CNPJ de quem vai receber').fill(recebedor.documento);
    await page.getByLabel('CPF ou CNPJ de quem vai receber').blur();
    await expect(page.getByRole('group', { name: 'Destinatário' }).getByText('Loja Sessao Ltda')).toBeVisible();
    await page.getByLabel('Valor').fill('1,25');
    await page.getByRole('button', { name: 'Continuar' }).click();
    await expect(page.getByRole('heading', { name: 'Confira antes de enviar' })).toBeVisible();

    // O primeiro envio é recusado como se o token tivesse vencido; o app renova e repete o mesmo pedido.
    const chaves: (string | null)[] = [];
    await page.route('**/api/v1/transferencias', async (rota) => {
      chaves.push(rota.request().headers()['idempotency-key'] ?? null);
      if (chaves.length === 1) {
        await rota.fulfill({
          status: 401,
          contentType: 'application/problem+json',
          body: JSON.stringify({ type: 'urn:bank:problema:nao-autenticado', title: 'Não autenticado', status: 401 }),
        });
        return;
      }

      await rota.continue();
    });
    await page.getByRole('button', { name: /^Transferir R\$\s1,25$/ }).click();

    await expect(page.getByRole('heading', { name: 'Transferência concluída' })).toBeVisible();
    expect(chaves).toHaveLength(2);
    expect(chaves[1]).toBe(chaves[0]);
    const extrato = await extratoPelaApi(page, recebedor, 'Entrada');
    expect(extrato.itens.map((i) => i.valor)).toEqual([1.25]);
  });

  test('renovação recusada leva ao login com "Sua sessão expirou"', async ({ page, context }) => {
    const conta = await criarContaPelaApi(page);
    await entrar(page, conta);
    await expect(page.getByRole('heading', { name: 'Olá, Lucas' })).toBeVisible();

    await context.clearCookies(); // a credencial de renovação some
    await page.route('**/api/v1/usuarios/me', (rota) =>
      rota.fulfill({
        status: 401,
        contentType: 'application/problem+json',
        body: JSON.stringify({ type: 'urn:bank:problema:nao-autenticado', title: 'Não autenticado', status: 401 }),
      }),
    );
    await page.getByRole('navigation', { name: /Menu principal/ }).filter({ visible: true }).getByRole('link', { name: 'Perfil' }).click();

    await expect(page).toHaveURL(/\/entrar\?expirada=1/);
    await expect(page.getByText('Sua sessão expirou. Entre novamente.')).toBeVisible();
  });

  test('"Sair" encerra a sessão no servidor e em todas as abas', async ({ page, context }) => {
    const conta = await criarContaPelaApi(page);
    await entrar(page, conta);
    const outraAba = await context.newPage();
    await outraAba.goto('/perfil');
    await expect(outraAba.getByRole('heading', { name: 'Perfil' })).toBeVisible();
    const cookieAntes = (await context.cookies()).find((c) => c.name === 'bank_renovacao')!.value;

    await outraAba.getByRole('main').getByRole('button', { name: 'Sair' }).click();

    await expect(outraAba).toHaveURL(/\/entrar$/);
    await expect(page).toHaveURL(/\/entrar$/);
    await expect(page.getByText('Sua sessão expirou. Entre novamente.')).not.toBeVisible();
    await page.goto('/');
    await expect(page).toHaveURL(/\/entrar$/);

    // A credencial de antes não renova mais (revogada no servidor).
    const resposta = await page.request.post('/api/v1/autenticacao/renovar', { headers: { Cookie: `bank_renovacao=${cookieAntes}` } });
    expect(resposta.status()).toBe(401);
  });

  test('aviso de inatividade aparece nas duas abas; "Continuar conectado" em uma fecha nas duas; sem resposta, expira', async ({ page, context }) => {
    test.setTimeout(90_000);
    const conta = await criarContaPelaApi(page);
    await entrar(page, conta);
    const outraAba = await context.newPage();
    await abrirComRenovacao(outraAba, '/perfil');
    await abrirComRenovacao(page, '/perfil');

    const avisoAqui = page.getByRole('alertdialog', { name: 'Sua sessão vai expirar' });
    const avisoLa = outraAba.getByRole('alertdialog', { name: 'Sua sessão vai expirar' });
    await expect(avisoAqui).toBeVisible({ timeout: 20_000 });
    await expect(avisoLa).toBeVisible();
    await semViolacoesA11y(page);

    await outraAba.getByRole('button', { name: 'Continuar conectado' }).click();
    await expect(avisoLa).toBeHidden();
    await expect(avisoAqui).toBeHidden();

    // Sem resposta, a sessão termina no fim do prazo (25 s).
    await expect(avisoAqui).toBeVisible({ timeout: 20_000 });
    await expect(page).toHaveURL(/\/entrar\?expirada=1/, { timeout: 20_000 });
    await expect(outraAba).toHaveURL(/\/entrar\?expirada=1/);
  });
});
