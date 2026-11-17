import { expect, test } from '@playwright/test';
import { criarContaPelaApi, entrar, semRolagemHorizontal } from './apoio';

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
