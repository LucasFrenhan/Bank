import { expect, test } from '@playwright/test';
import { criarContaPelaApi, entrar, semViolacoesA11y } from './apoio';

// US2 — entrar, sessão por aba, expiração e saída (FR-009 a FR-013).
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

  test('recarregar mantém a sessão; nova aba exige login; o token não vaza', async ({ page, context }) => {
    const mensagensConsole: string[] = [];
    page.on('console', (m) => mensagensConsole.push(m.text()));
    const conta = await criarContaPelaApi(page);
    await entrar(page, conta);
    await expect(page.getByRole('heading', { name: 'Olá, Lucas' })).toBeVisible();

    await page.reload();
    await expect(page.getByRole('heading', { name: 'Olá, Lucas' })).toBeVisible();

    const token = await page.evaluate(() => (JSON.parse(sessionStorage.getItem('bank.sessao') ?? '{}') as { token?: string }).token ?? '');
    expect(token).not.toBe('');
    expect(page.url()).not.toContain(token);
    expect(mensagensConsole.some((m) => m.includes(token))).toBe(false);

    const outraAba = await context.newPage();
    await outraAba.goto('/');
    await expect(outraAba).toHaveURL(/\/entrar$/);
  });

  test('sessão vencida leva ao login com aviso na próxima ação', async ({ page }) => {
    const conta = await criarContaPelaApi(page);
    await entrar(page, conta);
    await expect(page.getByRole('heading', { name: 'Olá, Lucas' })).toBeVisible();

    await page.evaluate(() => {
      const sessao = JSON.parse(sessionStorage.getItem('bank.sessao')!) as { token: string; expiraEm: number };
      sessionStorage.setItem('bank.sessao', JSON.stringify({ ...sessao, expiraEm: Date.now() - 1000 }));
    });
    await page.reload();

    await expect(page).toHaveURL(/\/entrar/);
  });

  test('token recusado pela API (401) leva ao login com "Sua sessão expirou"', async ({ page }) => {
    const conta = await criarContaPelaApi(page);
    await entrar(page, conta);
    await expect(page.getByRole('heading', { name: 'Olá, Lucas' })).toBeVisible();

    await page.evaluate(() => {
      const sessao = JSON.parse(sessionStorage.getItem('bank.sessao')!) as { token: string; expiraEm: number };
      const [cabecalho, corpo] = sessao.token.split('.');
      sessionStorage.setItem('bank.sessao', JSON.stringify({ ...sessao, token: `${cabecalho}.${corpo}.assinatura-adulterada` }));
    });
    await page.goto('/perfil');

    await expect(page).toHaveURL(/\/entrar\?expirada=1/);
    await expect(page.getByText('Sua sessão expirou. Entre novamente.')).toBeVisible();
  });

  test('"Sair" encerra a sessão', async ({ page }) => {
    const conta = await criarContaPelaApi(page);
    await entrar(page, conta);
    await page.goto('/perfil');
    await page.getByRole('main').getByRole('button', { name: 'Sair' }).click();

    await expect(page).toHaveURL(/\/entrar$/);
    await page.goto('/');
    await expect(page).toHaveURL(/\/entrar$/);
  });
});
