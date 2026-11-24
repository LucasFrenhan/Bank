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

  it('define o título da página por rota', async () => {
    renderizarApp('/criar-conta');

    await screen.findByRole('heading', { name: 'Criar conta' });
    expect(document.title).toBe('Criar conta · One Bank');
  });
});
