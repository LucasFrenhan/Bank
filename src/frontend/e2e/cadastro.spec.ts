import { expect, test } from '@playwright/test';
import { cnpj, cpf, email, mascararCnpj, mascararCpf, SENHA, semViolacoesA11y } from './apoio';

// US1 — cadastro pelo navegador, contra a API real (desktop e mobile).
test.describe('Criar conta', () => {
  test('PF com CPF mascarado entra direto no início com R$ 10,00', async ({ page }) => {
    await page.goto('/criar-conta');
    await semViolacoesA11y(page);

    await page.getByLabel('Nome completo').fill('Lucas Silva');
    await page.getByLabel('CPF', { exact: true }).fill(mascararCpf(cpf()));
    await page.getByLabel('E-mail').fill(email());
    await page.getByLabel('Senha', { exact: true }).fill(SENHA);
    await page.getByRole('button', { name: 'Criar conta' }).click();

    await expect(page).toHaveURL(/\/$/);
    await expect(page.getByRole('heading', { name: 'Olá, Lucas' })).toBeVisible();
    await expect(page.getByRole('region', { name: 'Saldo disponível' }).getByText(/R\$\s10,00/)).toBeVisible();
  });

  test('PJ com CNPJ alfanumérico minúsculo mostra maiúsculas e início com R$ 0,00', async ({ page }) => {
    const documento = cnpj();
    await page.goto('/criar-conta');
    await page.getByText('Pessoa Jurídica', { exact: true }).click();

    await page.getByLabel('Razão social').fill('Empresa Exemplo Ltda');
    await page.getByLabel('CNPJ', { exact: true }).pressSequentially(documento.toLowerCase());
    await expect(page.getByLabel('CNPJ', { exact: true })).toHaveValue(mascararCnpj(documento));
    await page.getByLabel('E-mail').fill(email());
    await page.getByLabel('Senha', { exact: true }).fill(SENHA);
    await page.getByRole('button', { name: 'Criar conta' }).click();

    await expect(page.getByRole('heading', { name: 'Empresa Exemplo Ltda' })).toBeVisible();
    await expect(page.getByText(/R\$\s0,00/)).toBeVisible();
    await expect(page.getByText(/Contas Pessoa Jurídica não enviam transferências/)).toBeVisible();
  });

  test('e-mail repetido mostra "Este e-mail já tem conta" com link para o login', async ({ page }) => {
    const repetido = email();
    const resposta = await page.request.post('/api/v1/autenticacao/registrar', {
      data: { nomeCompleto: 'Primeira', email: repetido, documento: cpf(), tipoConta: 'PessoaFisica', senha: SENHA },
    });
    expect(resposta.status()).toBe(201);

    await page.goto('/criar-conta');
    await page.getByLabel('Nome completo').fill('Segunda');
    await page.getByLabel('CPF', { exact: true }).fill(cpf());
    await page.getByLabel('E-mail').fill(repetido);
    await page.getByLabel('Senha', { exact: true }).fill(SENHA);
    await page.getByRole('button', { name: 'Criar conta' }).click();

    await expect(page.getByRole('alert')).toContainText('Este e-mail já tem conta');
    await expect(page.getByRole('link', { name: 'Ir para o login' })).toBeVisible();
  });

  test('CNPJ com dígito verificador inválido mostra erro no campo e passa no axe', async ({ page }) => {
    await page.goto('/criar-conta');
    await page.getByText('Pessoa Jurídica', { exact: true }).click();
    await page.getByLabel('Razão social').fill('Empresa');
    await page.getByLabel('CNPJ', { exact: true }).fill('12ABC34501DE36');
    await page.getByLabel('E-mail').fill(email());
    await page.getByLabel('Senha', { exact: true }).fill(SENHA);
    await page.getByRole('button', { name: 'Criar conta' }).click();

    await expect(page.getByText('CNPJ inválido.')).toBeVisible();
    await expect(page.getByLabel('CNPJ', { exact: true })).toHaveAttribute('aria-invalid', 'true');
    await semViolacoesA11y(page);
  });
});
