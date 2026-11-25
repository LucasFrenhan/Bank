import { useCallback, useEffect, useRef, useState } from 'react';
import { Link, useSearchParams } from 'react-router';
import { api, type DirecaoExtrato, type ItemExtrato } from '../api/cliente';
import type { MensagemErro } from '../api/erros';
import { Botao } from '../design/componentes/Botao';
import { AvisoErro, Cartao, Carregando, EstadoVazio } from '../design/componentes/Feedback';
import { Filtros } from '../design/componentes/Filtros';
import { IconeEntrada, IconeSaida } from '../design/componentes/Icones';
import { ListaLancamentos } from '../design/componentes/ListaLancamentos';
import { chaveDia, formatarDataCurta, formatarHora, rotuloDia } from '../formatacao/data';
import { formatarMoeda } from '../formatacao/moeda';
import { useTitulo } from './comum';
import estilos from './Extrato.module.css';
import { VAZIO_MOVIMENTACOES } from './Inicio';
import { descricaoItem, ehEntrada, ehTransferencia, linkComprovante, nomeContraparte, rotuloDirecao, valorComSinal } from './lancamentos';
import { useCarteira } from './useCarteira';
import { useExtrato } from './useExtrato';
import { useLarguraDesktop } from './useLarguraDesktop';

/**
 * Extrato da carteira, PF e PJ (specs/007-frontend-transferencias: US4; FR-015 a FR-020;
 * research R7). Filtro e página ficam no endereço; desktop em tabela paginada, mobile em lista
 * agrupada por dia com "Carregar mais".
 */

export const TAMANHO_PAGINA = 20;
const MAXIMO_POR_REQUISICAO = 100;

type Filtro = 'entradas' | 'saidas' | null;

const FILTROS: { rotulo: string; valor: Filtro }[] = [
  { rotulo: 'Todas', valor: null },
  { rotulo: 'Entradas', valor: 'entradas' },
  { rotulo: 'Saídas', valor: 'saidas' },
];

const DIRECAO_API: Record<'entradas' | 'saidas', DirecaoExtrato> = { entradas: 'Entrada', saidas: 'Saida' };

function lerFiltro(valor: string | null): Filtro {
  return valor === 'entradas' || valor === 'saidas' ? valor : null;
}

function lerPagina(valor: string | null): number {
  const numero = Number(valor);
  return Number.isInteger(numero) && numero >= 1 ? numero : 1;
}

export function Extrato() {
  useTitulo('Extrato');
  const [parametros, definirParametros] = useSearchParams();
  const filtro = lerFiltro(parametros.get('direcao'));
  const pagina = lerPagina(parametros.get('pagina'));
  const desktop = useLarguraDesktop();
  const carteira = useCarteira();
  const direcao = filtro ? DIRECAO_API[filtro] : undefined;

  const irPara = useCallback(
    (novoFiltro: Filtro, novaPagina: number, substituir = false) => {
      const proximos = new URLSearchParams();
      if (novoFiltro) proximos.set('direcao', novoFiltro);
      if (novaPagina > 1) proximos.set('pagina', String(novaPagina));
      definirParametros(proximos, { replace: substituir });
    },
    [definirParametros],
  );

  return (
    <div className={estilos.pagina}>
      <div className={estilos.cabecalho}>
        <div>
          <h1>Extrato</h1>
          {carteira.estado === 'pronta' ? <p className={estilos.saldo}>Saldo atual {formatarMoeda(carteira.carteira.saldo)}</p> : null}
        </div>
        <Filtros rotulo="Filtrar lançamentos" opcoes={FILTROS} atual={filtro} aoEscolher={(valor) => irPara(valor, 1)} />
      </div>

      {desktop ? (
        <ExtratoDesktop direcao={direcao} pagina={pagina} irParaPagina={(p, substituir) => irPara(filtro, p, substituir)} />
      ) : (
        <ExtratoMobile
          key={filtro ?? 'todas'}
          direcao={direcao}
          paginaInicial={pagina}
          aoCarregar={(paginas) => irPara(filtro, paginas, true)}
        />
      )}
    </div>
  );
}

function Vazio() {
  return <EstadoVazio titulo={VAZIO_MOVIMENTACOES.titulo} descricao={VAZIO_MOVIMENTACOES.descricao} />;
}

/* ---------- Desktop: tabela paginada ---------- */

function ExtratoDesktop({
  direcao,
  pagina,
  irParaPagina,
}: {
  direcao: DirecaoExtrato | undefined;
  pagina: number;
  irParaPagina: (pagina: number, substituir?: boolean) => void;
}) {
  const extrato = useExtrato({ pagina, tamanhoPagina: TAMANHO_PAGINA, ...(direcao ? { direcao } : {}) });
  const total = extrato.estado === 'pronto' ? Number(extrato.extrato.totalItens) : 0;
  const totalPaginas = Math.max(1, Math.ceil(total / TAMANHO_PAGINA));
  const alemDaUltima = extrato.estado === 'pronto' && total > 0 && pagina > totalPaginas;

  // Página além da última (por exemplo, lançamentos mudaram): mostra a última válida.
  useEffect(() => {
    if (alemDaUltima) irParaPagina(totalPaginas, true);
  }, [alemDaUltima, totalPaginas, irParaPagina]);

  if (extrato.estado === 'carregando' || alemDaUltima) {
    return <Carregando texto="Carregando lançamentos…" />;
  }

  if (extrato.estado === 'erro') {
    return <AvisoErro erro={extrato.erro} aoTentarDeNovo={extrato.recarregar} />;
  }

  return (
    <Cartao className={estilos.cartao}>
      {extrato.extrato.itens.length === 0 ? (
        <Vazio />
      ) : (
        <div className={estilos.rolagemTabela}>
          <table className={estilos.tabela}>
            <caption className="apenas-leitor-de-tela">Lançamentos</caption>
            <thead>
              <tr>
                <th scope="col">Data</th>
                <th scope="col">Contraparte</th>
                <th scope="col">Descrição</th>
                <th scope="col">Status</th>
                <th scope="col" className={estilos.colunaValor}>
                  Valor
                </th>
              </tr>
            </thead>
            <tbody>
              {extrato.extrato.itens.map((item) => (
                <LinhaTabela key={item.lancamentoId} item={item} />
              ))}
            </tbody>
          </table>
        </div>
      )}

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
    </Cartao>
  );
}

function LinhaTabela({ item }: { item: ItemExtrato }) {
  const entrada = ehEntrada(item);
  const para = linkComprovante(item);
  const nome = nomeContraparte(item);

  return (
    <tr>
      <td className={estilos.data}>{formatarDataCurta(item.criadoEm)}</td>
      <td>
        <span className={estilos.contraparte}>
          <span className={[estilos.iconeDirecao, entrada ? estilos.iconeEntrada : ''].join(' ')} aria-hidden="true">
            {entrada ? <IconeEntrada /> : <IconeSaida />}
          </span>
          <span className={estilos.textoContraparte}>
            {para ? <Link to={para}>{nome}</Link> : <span className={estilos.nome}>{nome}</span>}
            <span className={estilos.direcao}>{rotuloDirecao(item)}</span>
          </span>
        </span>
      </td>
      <td className={estilos.descricao}>{descricaoItem(item)}</td>
      <td>
        <span className={estilos.seloConcluida}>Concluída</span>
      </td>
      <td className={[estilos.colunaValor, estilos.valor, entrada ? estilos.valorEntrada : ''].join(' ')}>{valorComSinal(item)}</td>
    </tr>
  );
}

/* ---------- Mobile: lista agrupada por dia com "Carregar mais" ---------- */

type EstadoMobile =
  | { estado: 'carregando' }
  | { estado: 'pronto'; itens: ItemExtrato[]; total: number }
  | { estado: 'erro'; erro: MensagemErro };

function juntarSemRepetir(atuais: ItemExtrato[], novos: ItemExtrato[]): ItemExtrato[] {
  const vistos = new Set(atuais.map((i) => i.lancamentoId));
  return [...atuais, ...novos.filter((i) => !vistos.has(i.lancamentoId))];
}

function ExtratoMobile({
  direcao,
  paginaInicial,
  aoCarregar,
}: {
  direcao: DirecaoExtrato | undefined;
  paginaInicial: number;
  aoCarregar: (paginas: number) => void;
}) {
  // A primeira carga traz as N páginas do endereço numa só requisição (até 100 itens).
  const [paginasIniciais] = useState(() => Math.min(paginaInicial, MAXIMO_POR_REQUISICAO / TAMANHO_PAGINA));
  const [estado, definirEstado] = useState<EstadoMobile>({ estado: 'carregando' });
  const [versao, definirVersao] = useState(0);
  const [carregandoMais, definirCarregandoMais] = useState(false);
  const [erroMais, definirErroMais] = useState<MensagemErro | null>(null);
  const paginasCarregadas = useRef(0);

  useEffect(() => {
    let ativo = true;
    void api
      .obterExtrato({ pagina: 1, tamanhoPagina: paginasIniciais * TAMANHO_PAGINA, ...(direcao ? { direcao } : {}) })
      .then((resultado) => {
        if (!ativo) return;
        if (resultado.ok) {
          paginasCarregadas.current = paginasIniciais;
          definirEstado({ estado: 'pronto', itens: resultado.dados.itens, total: Number(resultado.dados.totalItens) });
        } else if (!resultado.erro.encerrarSessao) {
          definirEstado({ estado: 'erro', erro: resultado.erro });
        }
      });

    return () => {
      ativo = false;
    };
  }, [direcao, paginasIniciais, versao]);

  async function carregarMais() {
    if (estado.estado !== 'pronto' || carregandoMais) return;
    definirCarregandoMais(true);
    definirErroMais(null);
    const proxima = paginasCarregadas.current + 1;
    const resultado = await api.obterExtrato({ pagina: proxima, tamanhoPagina: TAMANHO_PAGINA, ...(direcao ? { direcao } : {}) });
    definirCarregandoMais(false);

    if (!resultado.ok) {
      if (!resultado.erro.encerrarSessao) definirErroMais(resultado.erro);
      return;
    }

    paginasCarregadas.current = proxima;
    definirEstado((atual) =>
      atual.estado === 'pronto'
        ? { estado: 'pronto', itens: juntarSemRepetir(atual.itens, resultado.dados.itens), total: Number(resultado.dados.totalItens) }
        : atual,
    );
    aoCarregar(proxima);
  }

  if (estado.estado === 'carregando') {
    return <Carregando texto="Carregando lançamentos…" />;
  }

  if (estado.estado === 'erro') {
    return (
      <AvisoErro
        erro={estado.erro}
        aoTentarDeNovo={() => {
          definirEstado({ estado: 'carregando' });
          definirVersao((v) => v + 1);
        }}
      />
    );
  }

  if (estado.itens.length === 0) {
    return (
      <Cartao>
        <Vazio />
      </Cartao>
    );
  }

  const grupos: { dia: string; rotulo: string; itens: ItemExtrato[] }[] = [];
  for (const item of estado.itens) {
    const dia = chaveDia(item.criadoEm);
    const ultimo = grupos.at(-1);
    if (ultimo?.dia === dia) {
      ultimo.itens.push(item);
    } else {
      grupos.push({ dia, rotulo: rotuloDia(item.criadoEm), itens: [item] });
    }
  }

  return (
    <div className={estilos.listaMobile}>
      {grupos.map((grupo) => (
        <section key={grupo.dia} aria-labelledby={`dia-${grupo.dia}`} className={estilos.grupo}>
          <h2 id={`dia-${grupo.dia}`} className={estilos.rotuloDia}>
            {grupo.rotulo}
          </h2>
          <ListaLancamentos
            linhas={grupo.itens.map((item) => ({
              id: item.lancamentoId,
              entrada: ehEntrada(item),
              nome: nomeContraparte(item),
              // Transferência mostra a direção; os demais tipos, a descrição (specs/007, FR-016; specs/014, FR-038).
              detalhe: `${ehTransferencia(item) ? rotuloDirecao(item) : descricaoItem(item)} · ${formatarHora(item.criadoEm)}`,
              valor: valorComSinal(item),
              para: linkComprovante(item),
            }))}
          />
        </section>
      ))}

      {erroMais ? <AvisoErro erro={erroMais} /> : null}

      {estado.itens.length < estado.total ? (
        <div className={estilos.carregarMais}>
          <Botao variante="secundario" carregando={carregandoMais} onClick={() => void carregarMais()}>
            Carregar mais
          </Botao>
        </div>
      ) : null}
    </div>
  );
}
