import { screen, waitFor, within } from '@testing-library/react';
import { guardarComprovante } from '../transferencia/armazenamento';
import { comprovante, ID_TRANSFERENCIA } from '../testes/handlers';
import { renderizarApp } from '../testes/renderizar';

/** Comprovante (specs/007: US1; FR-011, FR-013, FR-014; research R5, R10). */

function abrir(extra: object = {}) {
  guardarComprovante({ ...comprovante({ valor: 45.9, descricao: 'Café e pães' }), ...extra });
  return renderizarApp(`/transferencias/${ID_TRANSFERENCIA}`, { sessao: 'PessoaFisica' });
}

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

  it('recebedor vê "Transferência recebida" sem o CPF do pagador', async () => {
    abrir({ pagador: { usuarioId: 'outro', nome: 'Mariana Costa' }, recebedor: { usuarioId: 'usuario-1', nome: 'Lucas Silva', documento: '52998224725', tipoConta: 'PessoaFisica' } });

    expect(await screen.findByRole('heading', { name: 'Transferência recebida' })).toBeInTheDocument();
    expect(screen.getByText(/R\$\s45,90 recebidos de Mariana Costa\./)).toBeInTheDocument();
    expect(screen.getByText('Mariana Costa')).toBeInTheDocument();
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
