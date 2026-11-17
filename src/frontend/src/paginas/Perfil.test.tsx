import { screen, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { problema, titular, TRACE_ID } from '../testes/handlers';
import { renderizarApp } from '../testes/renderizar';
import { servidor } from '../testes/setup';

describe('Perfil (US4; FR-017 — dados do titular via GET /api/v1/usuarios/me)', () => {
  it('PF: nome, e-mail, CPF mascarado, "Cliente desde", tipo não alterável e permissões', async () => {
    renderizarApp('/perfil', { sessao: 'PessoaFisica' });

    expect(await screen.findByRole('heading', { name: 'Lucas Silva' })).toBeInTheDocument();
    expect(screen.getByText('lucas@exemplo.com.br')).toBeInTheDocument();
    expect(screen.getByText('CPF')).toBeInTheDocument();
    expect(screen.getByText('•••.982.247-••')).toBeInTheDocument();
    expect(screen.queryByText('52998224725')).not.toBeInTheDocument();
    expect(screen.getByText('Cliente desde')).toBeInTheDocument();
    expect(screen.getByText('9 nov 2026')).toBeInTheDocument();
    expect(screen.getByText('Pessoa Física · não pode ser alterado')).toBeInTheDocument();
    for (const permissao of ['Enviar transferências para PF e PJ', 'Receber transferências', 'Pagar cobranças de cartão de crédito']) {
      expect(screen.getByText(permissao)).toBeInTheDocument();
    }
  });

  it('PJ: CNPJ alfanumérico mascarado e permissões de receber e gerar cobranças', async () => {
    servidor.use(http.get('*/api/v1/usuarios/me', () => HttpResponse.json(titular('PessoaJuridica'))));
    renderizarApp('/perfil', { sessao: 'PessoaJuridica' });

    expect(await screen.findByText('••.•••.345/01DE-••')).toBeInTheDocument();
    expect(screen.getByText('CNPJ')).toBeInTheDocument();
    expect(screen.getByText('Gerar cobranças de cartão de crédito')).toBeInTheDocument();
    expect(screen.queryByText('Enviar transferências para PF e PJ')).not.toBeInTheDocument();
  });

  it('conta inexistente (404 usuario-nao-encontrado) encerra a sessão', async () => {
    servidor.use(http.get('*/api/v1/usuarios/me', () => problema('usuario-nao-encontrado', 404)));
    renderizarApp('/perfil', { sessao: 'PessoaFisica' });

    expect(await screen.findByText('Não encontramos sua conta. Entre novamente.')).toBeInTheDocument();
    await waitFor(() => expect(screen.getByTestId('rota-atual')).toHaveTextContent('/entrar?conta=1'));
  });

  it('erro inesperado mostra "Algo deu errado" com traceId', async () => {
    servidor.use(http.get('*/api/v1/usuarios/me', () => problema('qualquer', 500)));
    renderizarApp('/perfil', { sessao: 'PessoaFisica' });

    const alerta = await screen.findByRole('alert');
    expect(alerta).toHaveTextContent('Algo deu errado');
    expect(alerta).toHaveTextContent(TRACE_ID);
  });

  it('"Sair" encerra a sessão', async () => {
    const { usuario } = renderizarApp('/perfil', { sessao: 'PessoaFisica' });
    await screen.findByRole('heading', { name: 'Lucas Silva' });

    const botoes = screen.getAllByRole('button', { name: 'Sair' });
    await usuario.click(botoes[botoes.length - 1]!);

    await waitFor(() => expect(screen.getByTestId('rota-atual')).toHaveTextContent('/entrar'));
  });
});
