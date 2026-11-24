import { screen, within } from '@testing-library/react';
import { renderizarApp } from './testes/renderizar';

describe('rotas (contracts/frontend.md; FR-010)', () => {
  it.each(['/', '/perfil'])('sem sessão, %s leva ao login', async (rota) => {
    renderizarApp(rota);

    expect(await screen.findByRole('heading', { name: 'Entrar' })).toBeInTheDocument();
    expect(screen.getByTestId('rota-atual')).toHaveTextContent('/entrar');
  });

  it.each(['/entrar', '/criar-conta'])('com sessão, %s leva ao início', async (rota) => {
    renderizarApp(rota, { sessao: 'PessoaFisica' });

    expect(await screen.findByText(/Olá, Lucas/)).toBeInTheDocument();
    expect(screen.getByTestId('rota-atual')).toHaveTextContent(/^\/$/);
  });

  it('rota desconhecida mostra "Página não encontrada" com link para o início', async () => {
    renderizarApp('/transferencias');

    expect(await screen.findByRole('heading', { name: 'Página não encontrada' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Ir para o início' })).toHaveAttribute('href', '/');
    expect(screen.getByText('O endereço acessado não existe no One Bank.')).toBeInTheDocument();
  });

  it('a marca se chama "One Bank" e o ícone "1B" é decorativo (specs/013, FR-009 e FR-010)', async () => {
    renderizarApp('/transferencias');

    const marca = await screen.findByRole('link', { name: 'One Bank' });
    expect(within(marca).getByText('1B')).toHaveAttribute('aria-hidden', 'true');
  });

  it.each(['/cartao', '/cartao/faturas', '/cartao/faturas/2026-12', '/cobrancas', '/cobrancas/nova', '/cobrancas/recebiveis', '/c/7K3F-9QXM'])(
    'sem sessão, %s leva ao login (specs/015)',
    async (rota) => {
      renderizarApp(rota);
      expect(await screen.findByRole('heading', { name: 'Entrar' })).toBeInTheDocument();
    },
  );

  it.each([
    ['PessoaFisica', ['Início', 'Transferir', 'Extrato', 'Cartão', 'Perfil'], ['Início', 'Extrato', 'Cartão', 'Perfil']],
    ['PessoaJuridica', ['Início', 'Recebimentos', 'Cobranças', 'Perfil'], ['Início', 'Recebimentos', 'Cobranças', 'Perfil']],
  ] as const)('menu de %s, com 4 itens na barra do celular (specs/015, FR-001)', async (perfil, menu, barra) => {
    renderizarApp('/perfil', { sessao: perfil });
    const nomes = (nome: string) => within(screen.getByRole('navigation', { name: nome })).getAllByRole('link').map((l) => l.textContent);

    await screen.findByRole('navigation', { name: 'Menu principal' });
    expect(nomes('Menu principal').slice(1)).toEqual(menu);
    expect(nomes('Menu principal (celular)')).toEqual(barra);
  });

  it.each([
    ['PessoaFisica', '/cartao', 'Cartão'],
    ['PessoaFisica', '/cartao/faturas', 'Cartão'],
    ['PessoaFisica', '/cartao/faturas/2026-12', 'Cartão'],
    ['PessoaJuridica', '/cobrancas', 'Cobranças'],
    ['PessoaJuridica', '/cobrancas/recebiveis', 'Cobranças'],
    ['PessoaJuridica', '/c/7K3F-9QXM', 'Cobranças'],
    ['PessoaFisica', '/extrato', 'Extrato'],
  ] as const)('%s em %s: item "%s" ativo', async (perfil, rota, item) => {
    renderizarApp(rota, { sessao: perfil });
    const menu = await screen.findByRole('navigation', { name: 'Menu principal' });
    const ativos = within(menu).getAllByRole('link').filter((l) => l.getAttribute('aria-current') === 'page');
    expect(ativos.map((l) => l.textContent)).toEqual([item]);
  });

  it('define o título da página por rota', async () => {
    renderizarApp('/criar-conta');

    await screen.findByRole('heading', { name: 'Criar conta' });
    expect(document.title).toBe('Criar conta · One Bank');
  });
});
