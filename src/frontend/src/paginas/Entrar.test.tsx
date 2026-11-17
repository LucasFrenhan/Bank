import { screen, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { problema, tokenFalso } from '../testes/handlers';
import { renderizarApp } from '../testes/renderizar';
import { servidor } from '../testes/setup';

describe('Entrar (US2; FR-009, FR-010)', () => {
  it('mostra aviso de demonstração, link para criar conta e nenhum "Esqueci a senha"', async () => {
    renderizarApp('/entrar');

    expect(await screen.findByRole('heading', { name: 'Entrar' })).toBeInTheDocument();
    expect(screen.getAllByText('Ambiente de demonstração.').length).toBeGreaterThan(0);
    expect(screen.getByRole('link', { name: 'Criar conta' })).toHaveAttribute('href', '/criar-conta');
    expect(screen.queryByText(/Esqueci a senha/)).not.toBeInTheDocument();
  });

  it('e-mail e senha são obrigatórios', async () => {
    const { usuario } = renderizarApp('/entrar');

    await usuario.click(screen.getByRole('button', { name: 'Entrar' }));

    expect(screen.getByText('Informe o e-mail.')).toBeInTheDocument();
    expect(screen.getByLabelText('E-mail')).toHaveFocus();
  });

  it('credenciais inválidas mostram mensagem única sem limpar o e-mail', async () => {
    servidor.use(http.post('*/api/v1/autenticacao/login', () => problema('credenciais-invalidas', 401)));
    const { usuario } = renderizarApp('/entrar');

    await usuario.type(screen.getByLabelText('E-mail'), 'lucas@exemplo.com.br');
    await usuario.type(screen.getByLabelText('Senha'), 'Errada@123');
    await usuario.click(screen.getByRole('button', { name: 'Entrar' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('E-mail ou senha incorretos');
    expect(screen.getByLabelText('E-mail')).toHaveValue('lucas@exemplo.com.br');
  });

  it('sucesso volta para a rota pedida antes do login', async () => {
    const { usuario } = renderizarApp('/perfil');
    await screen.findByRole('heading', { name: 'Entrar' });

    await usuario.type(screen.getByLabelText('E-mail'), 'lucas@exemplo.com.br');
    await usuario.type(screen.getByLabelText('Senha'), 'SenhaForte@123');
    await usuario.click(screen.getByRole('button', { name: 'Entrar' }));

    await waitFor(() => expect(screen.getByTestId('rota-atual')).toHaveTextContent('/perfil'));
  });

  it('?expirada=1 mostra o aviso de sessão expirada', async () => {
    renderizarApp('/entrar?expirada=1');

    expect(await screen.findByText('Sua sessão expirou. Entre novamente.')).toBeInTheDocument();
  });

  it('rede indisponível mostra mensagem de conexão', async () => {
    servidor.use(http.post('*/api/v1/autenticacao/login', () => HttpResponse.error()));
    const { usuario } = renderizarApp('/entrar');

    await usuario.type(screen.getByLabelText('E-mail'), 'lucas@exemplo.com.br');
    await usuario.type(screen.getByLabelText('Senha'), 'SenhaForte@123');
    await usuario.click(screen.getByRole('button', { name: 'Entrar' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Não foi possível conectar');
  });

  it('duplo clique em "Entrar" envia uma única requisição', async () => {
    let chamadas = 0;
    servidor.use(
      http.post('*/api/v1/autenticacao/login', async () => {
        chamadas++;
        await new Promise((r) => setTimeout(r, 50));
        return HttpResponse.json({ tokenAcesso: tokenFalso(), tipoToken: 'Bearer', expiraEmSegundos: 3600 });
      }),
    );
    const { usuario } = renderizarApp('/entrar');

    await usuario.type(screen.getByLabelText('E-mail'), 'lucas@exemplo.com.br');
    await usuario.type(screen.getByLabelText('Senha'), 'SenhaForte@123');
    await usuario.dblClick(screen.getByRole('button', { name: 'Entrar' }));

    await waitFor(() => expect(screen.getByTestId('rota-atual')).toHaveTextContent(/^\/$/));
    expect(chamadas).toBe(1);
  });
});
