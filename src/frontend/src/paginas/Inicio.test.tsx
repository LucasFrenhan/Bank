import { screen, waitFor, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { carteira, problema, TRACE_ID } from '../testes/handlers';
import { renderizarApp } from '../testes/renderizar';
import { servidor } from '../testes/setup';

describe('Início (US3; FR-014 a FR-016)', () => {
  it('PF: saudação, saldo, atualização e ações "Em breve"', async () => {
    renderizarApp('/', { sessao: 'PessoaFisica' });

    expect(await screen.findByRole('heading', { name: 'Olá, Lucas' })).toBeInTheDocument();
    expect(screen.getAllByText('Pessoa Física').length).toBeGreaterThan(0);
    expect(screen.getByText(/R\$\s10,00/)).toBeInTheDocument();
    expect(screen.getByText('Atualizado em 17 nov 2026, 14:32.')).toBeInTheDocument();

    const saldo = screen.getByRole('region', { name: 'Saldo disponível' });
    for (const acao of [/Transferir/, /Ver extrato/]) {
      const botao = within(saldo).getByRole('button', { name: acao });
      expect(botao).toHaveAttribute('aria-disabled', 'true');
      expect(botao).toHaveTextContent('Em breve');
    }

    expect(screen.getByText('Suas movimentações aparecerão aqui')).toBeInTheDocument();
  });

  it('PJ: razão social, saldo zero e nenhuma ação de transferir (RN-06)', async () => {
    servidor.use(http.get('*/api/v1/carteiras/minha', () => HttpResponse.json(carteira('PessoaJuridica'))));
    renderizarApp('/', { sessao: 'PessoaJuridica' });

    expect(await screen.findByRole('heading', { name: 'Empresa Exemplo Ltda' })).toBeInTheDocument();
    expect(screen.getByText(/R\$\s0,00/)).toBeInTheDocument();
    expect(screen.getByText(/Contas Pessoa Jurídica não enviam transferências/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Transferir/ })).not.toBeInTheDocument();
    expect(screen.queryByText('Transferir')).not.toBeInTheDocument();
  });

  it('mostra carregamento sem saldo falso', async () => {
    servidor.use(
      http.get('*/api/v1/carteiras/minha', async () => {
        await new Promise((r) => setTimeout(r, 100));
        return HttpResponse.json(carteira());
      }),
    );
    renderizarApp('/', { sessao: 'PessoaFisica' });

    expect(screen.getByRole('status')).toHaveTextContent('Carregando sua carteira…');
    expect(screen.queryByText(/R\$/)).not.toBeInTheDocument();
    await screen.findByRole('heading', { name: 'Olá, Lucas' });
  });

  it('erro inesperado mostra "Algo deu errado" com traceId e permite tentar de novo', async () => {
    let chamadas = 0;
    servidor.use(
      http.get('*/api/v1/carteiras/minha', () => {
        chamadas++;
        return chamadas === 1 ? problema('qualquer', 500) : HttpResponse.json(carteira());
      }),
    );
    const { usuario } = renderizarApp('/', { sessao: 'PessoaFisica' });

    const alerta = await screen.findByRole('alert');
    expect(alerta).toHaveTextContent('Algo deu errado');
    expect(alerta).toHaveTextContent(TRACE_ID);

    await usuario.click(screen.getByRole('button', { name: 'Tentar de novo' }));

    expect(await screen.findByRole('heading', { name: 'Olá, Lucas' })).toBeInTheDocument();
    expect(chamadas).toBe(2);
  });

  it('conta inexistente (404) encerra a sessão e leva ao login', async () => {
    servidor.use(http.get('*/api/v1/carteiras/minha', () => problema('carteira-nao-encontrada', 404)));
    renderizarApp('/', { sessao: 'PessoaFisica' });

    expect(await screen.findByText('Não encontramos sua conta. Entre novamente.')).toBeInTheDocument();
    await waitFor(() => expect(screen.getByTestId('rota-atual')).toHaveTextContent('/entrar?conta=1'));
  });

  it('cada exibição busca o saldo atual na API', async () => {
    let saldo = 10;
    servidor.use(http.get('*/api/v1/carteiras/minha', () => HttpResponse.json({ ...carteira(), saldo })));
    const primeira = renderizarApp('/', { sessao: 'PessoaFisica' });
    expect(await screen.findByText(/R\$\s10,00/)).toBeInTheDocument();
    primeira.unmount();

    saldo = 123.45;
    renderizarApp('/', { sessao: 'PessoaFisica' });

    expect(await screen.findByText(/R\$\s123,45/)).toBeInTheDocument();
  });
});
