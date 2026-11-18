import { expect, test } from '@playwright/test';
import { criarContaPelaApi, entrar, semRolagemHorizontal, semViolacoesA11y } from './apoio';

// US5 — larguras de 320 a 1920 px sem rolagem horizontal e alvos ≥ 44 px (SC-002, SC-006).
const LARGURAS = [320, 390, 768, 1440, 1920];

test.describe('Responsividade', () => {
  // As larguras são controladas aqui; basta um projeto.
  test.skip(({ isMobile }) => isMobile, 'executado só no projeto desktop');

  test('telas públicas e autenticadas em todas as larguras', async ({ page }) => {
    const nomeLongo = 'Maria '.repeat(25).trim().slice(0, 150);
    const pf = await criarContaPelaApi(page, 'PessoaFisica', nomeLongo);
    const pj = await criarContaPelaApi(page, 'PessoaJuridica');

    const rotasPublicas = ['/entrar', '/criar-conta', '/nao-existe'];
    for (const largura of LARGURAS) {
      await page.setViewportSize({ width: largura, height: 900 });
      for (const rota of rotasPublicas) {
        await page.goto(rota);
        await semRolagemHorizontal(page);
      }
    }

    for (const conta of [pf, pj]) {
      await page.setViewportSize({ width: 1440, height: 900 });
      await entrar(page, conta);
      await expect(page.getByRole('main').getByRole('heading', { level: 1 })).toBeVisible();

      for (const largura of LARGURAS) {
        await page.setViewportSize({ width: largura, height: 900 });
        for (const rota of ['/', '/perfil']) {
          await page.goto(rota);
          await expect(page.getByRole('main').getByRole('heading', { level: 1 })).toBeVisible();
          await semRolagemHorizontal(page);
          await alvosDeToqueAdequados(page);
        }

        const menuVisivel = largura >= 1024 ? 'Menu principal' : 'Menu principal (celular)';
        await expect(page.getByRole('navigation', { name: menuVisivel, exact: true })).toBeVisible();
      }

      await page.evaluate(() => sessionStorage.clear());
    }
  });
});

// Spec 007 US6 — telas de transferência, comprovante e extrato (FR-028; SC-006).
test.describe('Responsividade e acessibilidade — transferências e extrato', () => {
  test.skip(({ isMobile }) => isMobile, 'executado só no projeto desktop');

  test('cada tela e estado de aviso em todas as larguras, sem violações A/AA', async ({ page }) => {
    // Nome longo do pagador (aparece no extrato da PJ); nome curto do recebedor, para que o link de
    // uma linha no extrato da PF seja medido como alvo de toque.
    const nomeLongo = `Lucas Larguras ${'Silva '.repeat(25)}`.trim().slice(0, 150);
    const pf = await criarContaPelaApi(page, 'PessoaFisica', nomeLongo);
    const pj = await criarContaPelaApi(page, 'PessoaJuridica', 'Padaria Curta Ltda');
    await page.setViewportSize({ width: 1440, height: 900 });
    await entrar(page, pf);

    // Transferência concluída: deixa um comprovante na aba e um lançamento no extrato.
    await page.goto('/transferir');
    await page.getByLabel('CPF ou CNPJ de quem vai receber').fill(pj.documento);
    await expect(page.getByRole('group', { name: 'Destinatário' })).toBeVisible();
    await page.getByLabel('Valor').fill('1,00');
    await page.getByRole('button', { name: 'Continuar' }).click();
    await page.getByRole('button', { name: /^Transferir R\$/ }).click();
    await expect(page.getByRole('heading', { name: 'Transferência concluída' })).toBeVisible();
    const comprovante = new URL(page.url()).pathname;

    // Etapas Dados e Confirmação preenchidas, com aviso de saldo insuficiente na primeira.
    await page.goto('/transferir');
    await page.getByLabel('CPF ou CNPJ de quem vai receber').fill(pj.documento);
    await expect(page.getByRole('group', { name: 'Destinatário' })).toBeVisible();
    await page.getByLabel('Valor').fill('99,00');
    await page.getByRole('button', { name: 'Continuar' }).click();
    await expect(page.getByRole('alert')).toContainText('Saldo insuficiente');
    await semViolacoesA11y(page);
    await page.getByLabel('Valor').fill('2,00');
    await page.getByRole('button', { name: 'Continuar' }).click();
    await expect(page).toHaveURL(/\/transferir\/confirmar$/);

    // `pronto`: conteúdo que só aparece depois de os dados chegarem (evita medir a tela carregando).
    const telas: { rota: string; titulo: RegExp; pronto: RegExp }[] = [
      { rota: '/transferir', titulo: /Transferir dinheiro/, pronto: /Disponível:/ },
      { rota: '/transferir/confirmar', titulo: /Confira antes de enviar/, pronto: /Saldo depois/ },
      { rota: comprovante, titulo: /Transferência concluída/, pronto: /CPF •••\./ },
      { rota: '/extrato', titulo: /Extrato/, pronto: /^Saída/ },
      { rota: '/', titulo: /Olá, Lucas/, pronto: /^Transferência enviada/ },
    ];

    for (const largura of LARGURAS) {
      await page.setViewportSize({ width: largura, height: 900 });
      for (const { rota, titulo, pronto } of telas) {
        await page.goto(rota);
        await expect(page.getByRole('heading', { level: 1, name: titulo })).toBeVisible();
        await expect(page.getByText(pronto).first()).toBeVisible();
        await expect(page.getByText(/Carregando/)).toHaveCount(0);
        await semRolagemHorizontal(page);
        await alvosDeToqueAdequados(page);
        if (largura === 390 || largura === 1440) await semViolacoesA11y(page);
      }
    }

    // Reenvio: aviso informativo na confirmação.
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto('/transferir/confirmar');
    await page.getByRole('button', { name: /^Transferir R\$/ }).click();
    await expect(page.getByRole('heading', { name: 'Transferência concluída' })).toBeVisible();
    await page.goBack();
    await page.getByRole('button', { name: /^Transferir R\$/ }).click();
    await expect(page.getByText('Essa transferência já foi feita')).toBeVisible();
    await semViolacoesA11y(page);

    // PJ: aviso de indisponível e extrato com nome longo de contraparte.
    await page.evaluate(() => sessionStorage.clear());
    await page.setViewportSize({ width: 1440, height: 900 });
    await entrar(page, pj);
    for (const largura of [320, 1440]) {
      await page.setViewportSize({ width: largura, height: 900 });
      await page.goto('/transferir');
      await expect(page.getByText('Indisponível para Pessoa Jurídica')).toBeVisible();
      await semRolagemHorizontal(page);
      await semViolacoesA11y(page);
      await page.goto('/extrato?direcao=entradas');
      await expect(page.getByText(/^Lucas Larguras/).first()).toBeVisible();
      await semRolagemHorizontal(page);
      await semViolacoesA11y(page);
    }
  });
});

async function alvosDeToqueAdequados(page: import('@playwright/test').Page) {
  const pequenos = await page.evaluate(() =>
    Array.from(document.querySelectorAll<HTMLElement>('a, button, input, [role="button"]'))
      .filter((el) => el.offsetParent !== null && !el.classList.contains('apenas-leitor-de-tela'))
      .filter((el) => !(el.tagName === 'A' && el.closest('p'))) // links em texto corrido: exceção da WCAG 2.5.8
      .filter((el) => el.getAttribute('href') !== '#conteudo')
      .map((el) => ({ el, r: el.getBoundingClientRect() }))
      .filter(({ r }) => r.width < 44 || r.height < 44)
      .map(({ el, r }) => `${el.tagName} "${(el.textContent ?? '').trim().slice(0, 30)}" ${Math.round(r.width)}x${Math.round(r.height)}`),
  );
  expect(pequenos).toEqual([]);
}
