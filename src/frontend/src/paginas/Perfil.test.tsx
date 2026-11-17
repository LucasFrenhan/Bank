import { screen, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { carteira } from '../testes/handlers';
import { renderizarApp } from '../testes/renderizar';
import { servidor } from '../testes/setup';

describe('Perfil (US4; FR-017 — parte independente da issue #18)', () => {
  it('PF: nome, e-mail, tipo não alterável e permissões da RN-06', async () => {
    renderizarApp('/perfil', { sessao: 'PessoaFisica' });

    expect(await screen.findByRole('heading', { name: 'Lucas Silva' })).toBeInTheDocument();
    expect(screen.getByText('lucas@exemplo.com.br')).toBeInTheDocument();
    expect(screen.getByText('Pessoa Física · não pode ser alterado')).toBeInTheDocument();
    for (const permissao of ['Enviar transferências para PF e PJ', 'Receber transferências', 'Pagar cobranças de cartão de crédito']) {
      expect(screen.getByText(permissao)).toBeInTheDocument();
    }
    expect(screen.queryByText('Gerar cobranças de cartão de crédito')).not.toBeInTheDocument();
  });

  it('PJ: permissões de receber e gerar cobranças, sem transferir', async () => {
    servidor.use(http.get('*/api/v1/carteiras/minha', () => HttpResponse.json(carteira('PessoaJuridica'))));
    renderizarApp('/perfil', { sessao: 'PessoaJuridica' });

    expect(await screen.findByText('Gerar cobranças de cartão de crédito')).toBeInTheDocument();
    expect(screen.getByText('Receber transferências')).toBeInTheDocument();
    expect(screen.queryByText('Enviar transferências para PF e PJ')).not.toBeInTheDocument();
  });

  it('não exibe documento nem data fictícios enquanto a issue #18 não existe', async () => {
    renderizarApp('/perfil', { sessao: 'PessoaFisica' });
    await screen.findByRole('heading', { name: 'Lucas Silva' });

    expect(screen.queryByText(/•••/)).not.toBeInTheDocument();
    expect(screen.queryByText(/Cliente desde/)).not.toBeInTheDocument();
  });

  it('"Sair" encerra a sessão', async () => {
    const { usuario } = renderizarApp('/perfil', { sessao: 'PessoaFisica' });
    await screen.findByRole('heading', { name: 'Lucas Silva' });

    const botoes = screen.getAllByRole('button', { name: 'Sair' });
    await usuario.click(botoes[botoes.length - 1]!);

    await waitFor(() => expect(screen.getByTestId('rota-atual')).toHaveTextContent('/entrar'));
  });
});
