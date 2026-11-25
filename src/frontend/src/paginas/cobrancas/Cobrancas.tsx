import { useCallback } from 'react';
import { Link } from 'react-router';
import { api } from '../../api/cliente';
import { caminhoDaCobranca } from '../../cobrancas/codigo';
import { BotaoLink } from '../../design/componentes/Botao';
import { Filtros } from '../../design/componentes/Filtros';
import { SeloSituacao } from '../../design/componentes/SeloSituacao';
import { formatarDataRelativa } from '../../formatacao/data';
import { formatarCentavos, saldoEmCentavos } from '../../formatacao/valor';
import { useTitulo } from '../comum';
import { ListaPaginada } from '../ListaPaginada';
import linhas from '../ListaPaginada.module.css';
import { SomentePerfil } from '../SomentePerfil';
import { useFiltroPagina } from '../useFiltroPagina';
import estilos from './Cobrancas.module.css';
import { SecoesCobrancas } from './SecoesCobrancas';

const FILTROS = [
  { rotulo: 'Todas', valor: null },
  { rotulo: 'Pendentes', valor: 'pendentes' },
  { rotulo: 'Pagas', valor: 'pagas' },
  { rotulo: 'Canceladas', valor: 'canceladas' },
] as const;

const STATUS_API = { pendentes: 'Pendente', pagas: 'Paga', canceladas: 'Cancelada' } as const;
const VALORES = ['pendentes', 'pagas', 'canceladas'] as const;

/** Cobranças emitidas pela PJ (specs/015-frontend-cartao-credito, US2; FR-011). */
export function Cobrancas() {
  useTitulo('Cobranças');

  return (
    <SomentePerfil tipo="PessoaJuridica" titulo="Cobranças">
      <Conteudo />
    </SomentePerfil>
  );
}

function Conteudo() {
  const { filtro, pagina, irPara } = useFiltroPagina(VALORES);
  const irParaPagina = useCallback((p: number, substituir?: boolean) => irPara(filtro, p, substituir), [filtro, irPara]);

  return (
    <div className={estilos.pagina}>
      <div className={estilos.cabecalho}>
        <h1>Cobranças</h1>
        <BotaoLink para="/cobrancas/nova">Nova cobrança</BotaoLink>
      </div>
      <SecoesCobrancas />
      <Filtros rotulo="Filtrar cobranças" opcoes={FILTROS} atual={filtro} aoEscolher={(valor) => irPara(valor, 1)} />
      <ListaPaginada
        rotulo="Lista de cobranças"
        chave={filtro ?? 'todas'}
        buscar={(p, tamanhoPagina) => api.listarCobrancas({ pagina: p, tamanhoPagina, ...(filtro ? { status: STATUS_API[filtro] } : {}) })}
        idDe={(c) => c.cobrancaId}
        pagina={pagina}
        irParaPagina={irParaPagina}
        carregando="Carregando cobranças…"
        vazio={
          filtro
            ? { titulo: 'Nenhuma cobrança nesta situação' }
            : {
                titulo: 'Nenhuma cobrança ainda',
                descricao: 'Emita uma cobrança e envie o link para o seu cliente pagar com o cartão.',
                acao: <BotaoLink para="/cobrancas/nova">Nova cobrança</BotaoLink>,
              }
        }
      >
        {(itens) => (
          <ul className={linhas.linhas}>
            {itens.map((c) => (
              <li key={c.cobrancaId}>
                <Link to={caminhoDaCobranca(c.codigo)} className={linhas.linha}>
                  <span className={linhas.texto}>
                    <span className={linhas.nome}>{c.descricao}</span>
                    <span className={linhas.detalhe}>{formatarDataRelativa(c.criadoEm)}</span>
                  </span>
                  <SeloSituacao situacao={c.status} />
                  <span className={linhas.valor}>{formatarCentavos(saldoEmCentavos(c.valor))}</span>
                </Link>
              </li>
            ))}
          </ul>
        )}
      </ListaPaginada>
    </div>
  );
}
