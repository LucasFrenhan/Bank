import { screen, waitFor, within } from '@testing-library/react';
import { guardarComprovante } from '../transferencia/armazenamento';
import { http, HttpResponse } from 'msw';
import { comprovante, consultaDe, ID_TRANSFERENCIA, problema, TRACE_ID } from '../testes/handlers';
import { servidor } from '../testes/setup';
import { renderizarApp } from '../testes/renderizar';

/** Comprovante (specs/007: US1; FR-011, FR-013, FR-014; research R5, R10; consulta da spec 008). */

const ROTA_COMPROVANTE = '/transferencias/' + ID_TRANSFERENCIA;

function abrir(extra: object = {}) {
  guardarComprovante({ ...comprovante({ valor: 45.9, descricao: 'Café e pães' }), ...extra });
  return renderizarApp(ROTA_COMPROVANTE, { sessao: 'PessoaFisica' });
}

describe('Comprovante reaberto pela API (spec 007, fase 10; spec 008)', () => {
  it('sem comprovante na aba, carrega pela consulta por identificador', async () => {
    renderizarApp(ROTA_COMPROVANTE, { sessao: 'PessoaFisica' });

    expect(screen.getByText('Carregando comprovante…')).toBeInTheDocument();
    expect(await screen.findByRole('heading', { name: 'Transferência concluída' })).toBeInTheDocument();
    expect(screen.getByText('Lucas Silva · CPF •••.982.247-••')).toBeInTheDocument();
  });

  it('recebedor PJ reabre pela API e vê "Transferência recebida"', async () => {
    servidor.use(
      consultaDe({
        [ID_TRANSFERENCIA]: comprovante({
          chaveIdempotencia: null,
          recebedor: { usuarioId: 'usuario-1', nome: 'Empresa Exemplo Ltda', documento: '12ABC34501DE35', tipoConta: 'PessoaJuridica' },
          pagador: { usuarioId: 'outro', nome: 'Mariana Costa', documento: '•••.444.777-••' },
        }),
      }),
    );
    renderizarApp(ROTA_COMPROVANTE, { sessao: 'PessoaJuridica' });

    expect(await screen.findByRole('heading', { name: 'Transferência recebida' })).toBeInTheDocument();
    expect(screen.getByText('Mariana Costa · CPF •••.444.777-••')).toBeInTheDocument();
  });

  it('404 da API → "Comprovante não encontrado" com link para o extrato', async () => {
    renderizarApp('/transferencias/019a8f55-9999-7890-abcd-ef1234567890', { sessao: 'PessoaFisica' });

    expect(await screen.findByText('Comprovante não encontrado')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Ir para o extrato' })).toHaveAttribute('href', '/extrato');
  });

  it('erro inesperado mostra o código de suporte e permite tentar de novo', async () => {
    let falhar = true;
    servidor.use(
      http.get('*/api/v1/transferencias/:transferenciaId', () => {
        if (falhar) {
          falhar = false;
          return problema('erro-interno', 500);
        }
        return HttpResponse.json(comprovante());
      }),
    );
    const { usuario } = renderizarApp(ROTA_COMPROVANTE, { sessao: 'PessoaFisica' });

    expect(await screen.findByText('Algo deu errado')).toBeInTheDocument();
    expect(screen.getByText(TRACE_ID)).toBeInTheDocument();
    await usuario.click(screen.getByRole('button', { name: 'Tentar de novo' }));
    expect(await screen.findByRole('heading', { name: 'Transferência concluída' })).toBeInTheDocument();
  });
});

describe('Comprovante (US1)', () => {
  it('pagador vê "Transferência concluída" com todos os dados', async () => {
    abrir();

    expect(await screen.findByRole('heading', { level: 1, name: 'Transferência concluída' })).toBeInTheDocument();
    expect(screen.getByText(/R\$\s45,90 enviados para Padaria Boa Massa Ltda\./)).toBeInTheDocument();

    const dados = screen.getByRole('list', { name: 'Dados da transferência' });
    expect(within(dados).getByText(/R\$\s45,90/)).toBeInTheDocument();
    expect(await within(dados).findByText('Lucas Silva · CPF •••.982.247-••')).toBeInTheDocument();
    expect(within(dados).getByText('Padaria Boa Massa Ltda · CNPJ ••.•••.345/01DE-••')).toBeInTheDocument();
    expect(within(dados).getByText('Café e pães')).toBeInTheDocument();
    expect(within(dados).getByText('17 nov 2026, 08:10')).toBeInTheDocument();
    expect(within(dados).getByText('Concluída')).toBeInTheDocument();
    expect(within(dados).getByText(ID_TRANSFERENCIA)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Voltar ao início' })).toHaveAttribute('href', '/');
    expect(screen.queryByText(/019a8f50-1111/)).not.toBeInTheDocument();
  });

  it('recebedor vê "Transferência recebida" com o CPF do pagador mascarado (FR-011)', async () => {
    abrir({
      chaveIdempotencia: null,
      pagador: { usuarioId: 'outro', nome: 'Mariana Costa', documento: '•••.444.777-••' },
      recebedor: { usuarioId: 'usuario-1', nome: 'Lucas Silva', documento: '52998224725', tipoConta: 'PessoaFisica' },
    });

    expect(await screen.findByRole('heading', { name: 'Transferência recebida' })).toBeInTheDocument();
    expect(screen.getByText(/R\$\s45,90 recebidos de Mariana Costa\./)).toBeInTheDocument();
    expect(screen.getByText('Mariana Costa · CPF •••.444.777-••')).toBeInTheDocument();
  });

  it('sem descrição, a linha é omitida', async () => {
    abrir({ descricao: null });

    await screen.findByRole('heading', { name: 'Transferência concluída' });
    expect(screen.queryByText('Descrição')).not.toBeInTheDocument();
  });

  it('comprovante fora da aba → "Comprovante não encontrado" com link para o extrato', async () => {
    renderizarApp('/transferencias/inexistente', { sessao: 'PessoaFisica' });

    expect(await screen.findByText('Comprovante não encontrado')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Ir para o extrato' })).toHaveAttribute('href', '/extrato');
  });

  it('"Compartilhar" usa o compartilhamento nativo quando existe', async () => {
    const compartilhar = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'share', { value: compartilhar, configurable: true });
    try {
      const { usuario } = abrir();
      await usuario.click(await screen.findByRole('button', { name: 'Compartilhar' }));

      await waitFor(() => expect(compartilhar).toHaveBeenCalledTimes(1));
      const { text } = compartilhar.mock.calls[0]?.[0] as { text: string };
      expect(text).toContain('Padaria Boa Massa Ltda');
      expect(text).toContain('••.•••.345/01DE-••');
      expect(text).toContain(ID_TRANSFERENCIA);
      expect(text).not.toContain('12ABC34501DE35');
      expect(text).not.toContain('019a8f50-1111');
    } finally {
      Object.defineProperty(navigator, 'share', { value: undefined, configurable: true });
    }
  });

  it('"Compartilhar" sem compartilhamento nativo copia o resumo e confirma', async () => {
    const { usuario } = abrir();
    await usuario.click(await screen.findByRole('button', { name: 'Compartilhar' }));

    expect(await screen.findByText('Comprovante copiado')).toBeInTheDocument();
    expect(await navigator.clipboard.readText()).toContain(ID_TRANSFERENCIA);
  });
});
