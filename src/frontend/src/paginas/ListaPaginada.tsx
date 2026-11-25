import { useEffect, useState, type ReactNode } from 'react';
import type { Resultado } from '../api/cliente';
import type { MensagemErro } from '../api/erros';
import { useLarguraDesktop } from './useLarguraDesktop';
import { useConsulta } from './useConsulta';
import { Botao } from '../design/componentes/Botao';
import { AvisoErro, Carregando, Cartao, EstadoVazio } from '../design/componentes/Feedback';
import estilos from './ListaPaginada.module.css';

export const TAMANHO_PAGINA = 20;
const MAXIMO_POR_REQUISICAO = 100;

export interface PaginaApi<T> {
  itens: T[];
  totalItens: number | string;
}

export interface AcoesLista<T> {
  recarregar: () => void;
  alterar: (mudar: (item: T) => T) => void;
}

interface Props<T> {
  rotulo: string;
  /** Identifica o filtro: ao mudar, a lista recomeça. */
  chave: string;
  buscar: (pagina: number, tamanho: number) => Promise<Resultado<PaginaApi<T>>>;
  idDe: (item: T) => string;
  pagina: number;
  irParaPagina: (pagina: number, substituir?: boolean) => void;
  carregando: string;
  vazio: { titulo: string; descricao?: string; acao?: ReactNode };
  children: (itens: T[], acoes: AcoesLista<T>) => ReactNode;
}

/**
 * Lista da API: páginas no desktop, "Carregar mais" no celular (specs/015-frontend-cartao-credito, FR-011 e FR-021;
 * research R7).
 */
export function ListaPaginada<T>(props: Props<T>) {
  const desktop = useLarguraDesktop();
  return desktop ? <ListaDesktop {...props} /> : <ListaCelular key={props.chave} {...props} />;
}

function Vazio({ vazio }: { vazio: Props<unknown>['vazio'] }) {
  return (
    <Cartao>
      <EstadoVazio titulo={vazio.titulo} {...(vazio.descricao ? { descricao: vazio.descricao } : {})} />
      {vazio.acao ? <div className={estilos.acaoVazio}>{vazio.acao}</div> : null}
    </Cartao>
  );
}

function ListaDesktop<T>({ rotulo, chave, buscar, pagina, irParaPagina, carregando, vazio, children }: Props<T>) {
  const consulta = useConsulta(`${chave}|${pagina}`, () => buscar(pagina, TAMANHO_PAGINA));
  const total = consulta.estado === 'pronto' ? Number(consulta.dados.totalItens) : 0;
  const totalPaginas = Math.max(1, Math.ceil(total / TAMANHO_PAGINA));
  const alemDaUltima = consulta.estado === 'pronto' && total > 0 && pagina > totalPaginas;

  useEffect(() => {
    if (alemDaUltima) irParaPagina(totalPaginas, true);
  }, [alemDaUltima, totalPaginas, irParaPagina]);

  if (consulta.estado === 'carregando' || alemDaUltima) return <Carregando texto={carregando} />;
  if (consulta.estado === 'erro') return <AvisoErro erro={consulta.erro} aoTentarDeNovo={consulta.recarregar} />;
  if (consulta.dados.itens.length === 0) return <Vazio vazio={vazio} />;

  const { dados, definir, recarregar } = consulta;
  return (
    <>
      <Cartao rotulo={rotulo} className={estilos.cartao}>
        {children(dados.itens, { recarregar, alterar: (mudar) => definir({ ...dados, itens: dados.itens.map(mudar) }) })}
      </Cartao>
      <div className={estilos.rodape}>
        <p>
          Página {pagina} de {totalPaginas} · {TAMANHO_PAGINA} por página
        </p>
        <div className={estilos.paginacao}>
          <Botao variante="secundario" disabled={pagina <= 1} onClick={() => irParaPagina(pagina - 1)}>
            Anterior
          </Botao>
          <Botao variante="secundario" disabled={pagina >= totalPaginas} onClick={() => irParaPagina(pagina + 1)}>
            Próxima
          </Botao>
        </div>
      </div>
    </>
  );
}

function ListaCelular<T>({ rotulo, buscar, idDe, pagina, irParaPagina, carregando, vazio, children }: Props<T>) {
  // A primeira carga traz as N páginas do endereço numa só requisição (até 100 itens).
  const [paginasIniciais] = useState(() => Math.min(pagina, MAXIMO_POR_REQUISICAO / TAMANHO_PAGINA));
  const consulta = useConsulta('celular', () => buscar(1, paginasIniciais * TAMANHO_PAGINA));
  const [carregandoMais, definirCarregandoMais] = useState(false);
  const [erroMais, definirErroMais] = useState<MensagemErro | null>(null);
  const [paginas, definirPaginas] = useState(paginasIniciais);

  if (consulta.estado === 'carregando') return <Carregando texto={carregando} />;
  if (consulta.estado === 'erro') return <AvisoErro erro={consulta.erro} aoTentarDeNovo={consulta.recarregar} />;
  if (consulta.dados.itens.length === 0) return <Vazio vazio={vazio} />;

  const { dados, definir } = consulta;

  async function carregarMais() {
    definirCarregandoMais(true);
    definirErroMais(null);
    const proxima = paginas + 1;
    const resultado = await buscar(proxima, TAMANHO_PAGINA);
    definirCarregandoMais(false);
    if (!resultado.ok) {
      if (!resultado.erro.encerrarSessao) definirErroMais(resultado.erro);
      return;
    }

    definirPaginas(proxima);
    const vistos = new Set(dados.itens.map(idDe));
    definir({ itens: [...dados.itens, ...resultado.dados.itens.filter((i) => !vistos.has(idDe(i)))], totalItens: resultado.dados.totalItens });
    irParaPagina(proxima, true);
  }

  return (
    <>
      <Cartao rotulo={rotulo} className={estilos.cartao}>
        {children(dados.itens, {
          recarregar: () => {
            definirPaginas(paginasIniciais);
            definirErroMais(null);
            consulta.recarregar();
          },
          alterar: (mudar) => definir({ ...dados, itens: dados.itens.map(mudar) }),
        })}
      </Cartao>
      {erroMais ? <AvisoErro erro={erroMais} aoTentarDeNovo={() => void carregarMais()} /> : null}
      {dados.itens.length < Number(dados.totalItens) ? (
        <div className={estilos.carregarMais}>
          <Botao variante="secundario" carregando={carregandoMais} onClick={() => void carregarMais()}>
            Carregar mais
          </Botao>
        </div>
      ) : null}
    </>
  );
}
