import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import { MemoryRouter } from 'react-router';
import type { Resultado } from '../api/cliente';
import { Confirmacao } from '../design/componentes/Confirmacao';
import { Filtros } from '../design/componentes/Filtros';
import { SeloSituacao } from '../design/componentes/SeloSituacao';
import { useConfirmacao } from '../design/componentes/useConfirmacao';
import { ListaPaginada, type PaginaApi } from './ListaPaginada';

/** Componentes comuns das telas da specs/015 (research R7 e R12). */

function simularLargura(desktop: boolean) {
  Object.defineProperty(window, 'matchMedia', {
    configurable: true,
    value: (media: string) => ({ matches: desktop, media, addEventListener: () => undefined, removeEventListener: () => undefined }),
  });
}

afterEach(() => {
  Reflect.deleteProperty(window, 'matchMedia');
});

interface Item {
  id: string;
}
const itens = (n: number): Item[] => Array.from({ length: n }, (_, i) => ({ id: `item-${i + 1}` }));

function Lista({ buscar, paginaInicial = 1 }: { buscar: (pagina: number, tamanho: number) => Promise<Resultado<PaginaApi<Item>>>; paginaInicial?: number }) {
  const [pagina, definirPagina] = useState(paginaInicial);
  return (
    <MemoryRouter>
      <ListaPaginada<Item>
        rotulo="Lista"
        chave="todas"
        buscar={buscar}
        idDe={(i) => i.id}
        pagina={pagina}
        irParaPagina={(p) => definirPagina(p)}
        carregando="Carregando…"
        vazio={{ titulo: 'Nada aqui', acao: <a href="/nova">Nova</a> }}
      >
        {(lista, acoes) => (
          <ul>
            {lista.map((i) => (
              <li key={i.id}>{i.id}</li>
            ))}
            <li>
              <button type="button" onClick={() => acoes.alterar((i) => (i.id === 'item-1' ? { id: 'mudou' } : i))}>
                mudar
              </button>
              <button type="button" onClick={acoes.recarregar}>
                recarregar
              </button>
            </li>
          </ul>
        )}
      </ListaPaginada>
      <output>{pagina}</output>
    </MemoryRouter>
  );
}

const de = (todos: Item[]) =>
  vi.fn((pagina: number, tamanho: number) =>
    Promise.resolve<Resultado<PaginaApi<Item>>>({
      ok: true,
      status: 200,
      dados: { itens: todos.slice((pagina - 1) * tamanho, pagina * tamanho), totalItens: todos.length },
    }),
  );

describe('ListaPaginada', () => {
  it('desktop: páginas, alterar item e recarregar', async () => {
    simularLargura(true);
    const buscar = de(itens(25));
    render(<Lista buscar={buscar} />);

    expect(await screen.findByText('Página 1 de 2 · 20 por página')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Anterior' })).toBeDisabled();
    await userEvent.click(screen.getByRole('button', { name: 'mudar' }));
    expect(screen.getByText('mudou')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'recarregar' }));
    expect(await screen.findByText('item-1')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Próxima' }));
    expect(await screen.findByText('item-25')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Próxima' })).toBeDisabled();
  });

  it('desktop: página além da última vai para a última', async () => {
    simularLargura(true);
    render(<Lista buscar={de(itens(25))} paginaInicial={9} />);

    expect(await screen.findByText('item-25')).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('2');
  });

  it('celular: "Carregar mais" sem repetir, com falha e nova tentativa', async () => {
    simularLargura(false);
    const buscar = de(itens(25));
    render(<Lista buscar={buscar} />);
    await screen.findByText('item-20');

    buscar.mockResolvedValueOnce({ ok: false, erro: { codigo: 'rede', mensagem: 'Sem conexão', acao: 'tentar-de-novo' } });
    await userEvent.click(screen.getByRole('button', { name: 'Carregar mais' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Sem conexão');
    expect(screen.getByText('item-20')).toBeInTheDocument();

    buscar.mockResolvedValueOnce({ ok: true, status: 200, dados: { itens: [{ id: 'item-20' }, ...itens(25).slice(20)], totalItens: 25 } });
    await userEvent.click(screen.getByRole('button', { name: 'Tentar de novo' }));
    expect(await screen.findByText('item-25')).toBeInTheDocument();
    expect(screen.getAllByText('item-20')).toHaveLength(1);
    await waitFor(() => expect(screen.queryByRole('button', { name: 'Carregar mais' })).not.toBeInTheDocument());
  });

  it('celular: volta com as páginas do endereço numa requisição', async () => {
    simularLargura(false);
    const buscar = de(itens(50));
    render(<Lista buscar={buscar} paginaInicial={2} />);

    await screen.findByText('item-40');
    expect(buscar).toHaveBeenCalledWith(1, 40);
  });

  it.each([true, false])('vazio com ação e erro com nova tentativa (desktop: %s)', async (desktop) => {
    simularLargura(desktop);
    const { unmount } = render(<Lista buscar={de([])} />);
    expect(await screen.findByText('Nada aqui')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Nova' })).toBeInTheDocument();
    unmount();

    const buscar = de(itens(1));
    buscar.mockResolvedValueOnce({ ok: false, erro: { codigo: 'inesperado', mensagem: 'Algo deu errado', traceId: 'abc', acao: 'tentar-de-novo' } });
    render(<Lista buscar={buscar} />);
    const alerta = await screen.findByRole('alert');
    expect(alerta).toHaveTextContent('abc');
    await userEvent.click(within(alerta).getByRole('button', { name: 'Tentar de novo' }));
    expect(await screen.findByText('item-1')).toBeInTheDocument();
  });
});

describe('Filtros e situação', () => {
  it('um filtro ativo por vez', async () => {
    const escolher = vi.fn();
    render(<Filtros rotulo="Filtrar" opcoes={[{ rotulo: 'Todas', valor: null }, { rotulo: 'Pagas', valor: 'pagas' }]} atual={null} aoEscolher={escolher} />);

    expect(screen.getByRole('button', { name: 'Todas' })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('button', { name: 'Pagas' })).toHaveAttribute('aria-pressed', 'false');
    await userEvent.click(screen.getByRole('button', { name: 'Pagas' }));
    expect(escolher).toHaveBeenCalledWith('pagas');
  });

  it.each([
    ['Disponivel', 'Disponível'],
    ['Vencida', 'Vencida'],
    ['Antecipado', 'Antecipado'],
    ['AlgoNovo', 'AlgoNovo'],
  ])('%s → %s', (situacao, rotulo) => {
    render(<SeloSituacao situacao={situacao} />);
    expect(screen.getByText(rotulo)).toBeInTheDocument();
  });
});

describe('Confirmacao', () => {
  function Tela() {
    const { aberta, abrir, fechar, gatilho } = useConfirmacao();
    return aberta ? (
      <Confirmacao titulo="Confira" aoFechar={fechar}>
        <button type="button" onClick={fechar}>
          Voltar
        </button>
      </Confirmacao>
    ) : (
      <button type="button" ref={gatilho} onClick={abrir}>
        Abrir
      </button>
    );
  }

  it('foca o título; Esc e Voltar devolvem o foco ao botão', async () => {
    const usuario = userEvent.setup();
    render(<Tela />);
    await usuario.click(screen.getByRole('button', { name: 'Abrir' }));
    expect(screen.getByRole('heading', { name: 'Confira' })).toHaveFocus();
    expect(screen.getByRole('region', { name: 'Confira' })).toBeInTheDocument();

    await usuario.keyboard('{Escape}');
    expect(screen.getByRole('button', { name: 'Abrir' })).toHaveFocus();

    await usuario.click(screen.getByRole('button', { name: 'Abrir' }));
    await usuario.click(screen.getByRole('button', { name: 'Voltar' }));
    expect(screen.getByRole('button', { name: 'Abrir' })).toHaveFocus();
  });
});
