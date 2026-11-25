import { expect, test } from '@playwright/test';

// Issue #41 — a CSP do nginx (font-src 'self') não aceita fontes embutidas como data:.
test.describe('CSP', () => {
  test('carregar a página não gera violação e as fontes vêm da mesma origem', async ({ page }) => {
    const violacoes: string[] = [];
    page.on('console', (mensagem) => {
      if (mensagem.text().includes('Content Security Policy')) {
        violacoes.push(mensagem.text());
      }
    });

    const resposta = await page.goto('/entrar');
    expect(resposta?.headers()['content-security-policy']).toContain("font-src 'self';");
    await expect(page.getByRole('button', { name: 'Entrar' })).toBeVisible();

    // Força todos os subconjuntos (inclusive cirílico e vietnamita), não só os usados na tela.
    const fontes = await page.evaluate(async () => {
      const faces: FontFace[] = [];
      document.fonts.forEach((face) => faces.push(face));
      await Promise.allSettled(faces.map((face) => face.load()));
      return faces.map((face) => `${face.family} ${face.unicodeRange}: ${face.status}`);
    });

    expect(fontes.length).toBeGreaterThan(0);
    expect(fontes.filter((fonte) => !fonte.endsWith(': loaded'))).toEqual([]);
    expect(violacoes).toEqual([]);
  });
});
