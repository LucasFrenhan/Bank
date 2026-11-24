import { useCallback, useState } from 'react';
import { api, type Recebivel } from '../../api/cliente';
import { MENSAGENS, type MensagemErro } from '../../api/erros';
import { Botao } from '../../design/componentes/Botao';
import { AcoesConfirmacao, Confirmacao, LinhaConfirmacao, LinhasConfirmacao } from '../../design/componentes/Confirmacao';
import { AvisoErro } from '../../design/componentes/Feedback';
import { Filtros } from '../../design/componentes/Filtros';
import { SeloSituacao } from '../../design/componentes/SeloSituacao';
import { useConfirmacao } from '../../design/componentes/useConfirmacao';
import { formatarData } from '../../formatacao/data';
import { formatarDia, hojeBrasilia } from '../../formatacao/dia';
import { formatarCentavos, saldoEmCentavos, TAXA_ANTECIPACAO_PERCENTUAL, taxaAntecipacao } from '../../formatacao/valor';
import { useOperacao } from '../../operacoes/useOperacao';
import { SecoesCobrancas } from '../cobrancas/SecoesCobrancas';
import { useTitulo } from '../comum';
import { ListaPaginada } from '../ListaPaginada';
import linhas from '../ListaPaginada.module.css';
import { SomentePerfil } from '../SomentePerfil';
import { useFiltroPagina } from '../useFiltroPagina';
import estilos from './Recebiveis.module.css';

const FILTROS = [
  { rotulo: 'Todos', valor: null },
  { rotulo: 'Pendentes', valor: 'pendentes' },
  { rotulo: 'Antecipados', valor: 'antecipados' },
  { rotulo: 'Liquidados', valor: 'liquidados' },
] as const;

const SITUACAO_API = { pendentes: 'Pendente', antecipados: 'Antecipado', liquidados: 'Liquidado' } as const;
const VALORES = ['pendentes', 'antecipados', 'liquidados'] as const;

/** Recebíveis da PJ e antecipação por item (specs/015-frontend-cartao-credito, US6; FR-021 e FR-022). */
export function Recebiveis() {
  useTitulo('Recebíveis');

  return (
    <SomentePerfil tipo="PessoaJuridica" titulo="Recebíveis">
      <Conteudo />
    </SomentePerfil>
  );
}

function Conteudo() {
  const { filtro, pagina, irPara } = useFiltroPagina(VALORES);
  const irParaPagina = useCallback((p: number, substituir?: boolean) => irPara(filtro, p, substituir), [filtro, irPara]);
  const [aviso, definirAviso] = useState<MensagemErro | null>(null);
  const [anuncio, definirAnuncio] = useState('');

  return (
    <div className={estilos.pagina}>
      <h1>Cobranças</h1>
      <SecoesCobrancas />
      <Filtros
        rotulo="Filtrar recebíveis"
        opcoes={FILTROS}
        atual={filtro}
        aoEscolher={(valor) => {
          definirAviso(null);
          irPara(valor, 1);
        }}
      />
      <p className="apenas-leitor-de-tela" role="status" aria-live="polite">
        {anuncio}
      </p>
      {aviso ? <AvisoErro erro={aviso} aoAgir={() => definirAviso(null)} /> : null}
      <ListaPaginada
        rotulo="Lista de recebíveis"
        chave={filtro ?? 'todos'}
        buscar={(p, tamanhoPagina) => api.listarRecebiveis({ pagina: p, tamanhoPagina, ...(filtro ? { situacao: SITUACAO_API[filtro] } : {}) })}
        idDe={(r) => r.recebivelId}
        pagina={pagina}
        irParaPagina={irParaPagina}
        carregando="Carregando recebíveis…"
        vazio={
          filtro
            ? { titulo: 'Nenhum recebível nesta situação' }
            : { titulo: 'Nenhum recebível ainda', descricao: 'Quando uma cobrança for paga, o valor a receber aparece aqui.' }
        }
      >
        {(itens, acoes) => (
          <ul className={linhas.linhas}>
            {itens.map((r) => (
              <Item
                key={r.recebivelId}
                recebivel={r}
                aoAntecipar={(atual) => {
                  definirAviso(null);
                  definirAnuncio(`Recebível antecipado: ${formatarCentavos(saldoEmCentavos(atual.valorLiquido))}`);
                  acoes.alterar((i) => (i.recebivelId === atual.recebivelId ? atual : i));
                }}
                aoMudar={(erro) => {
                  definirAviso(erro);
                  acoes.recarregar();
                }}
              />
            ))}
          </ul>
        )}
      </ListaPaginada>
    </div>
  );
}

function detalhe(r: Recebivel): string {
  const recebido = r.recebidoEm ? formatarData(r.recebidoEm) : '';
  switch (r.situacao) {
    case 'Pendente':
      return `Previsto para ${formatarDia(r.dataPrevista)}`;
    case 'Antecipado':
      return `${formatarCentavos(saldoEmCentavos(r.valorBruto))} menos taxa de ${formatarCentavos(saldoEmCentavos(r.taxa))} · Recebido em ${recebido}`;
    case 'Liquidado':
      return `Recebido em ${recebido}`;
    default:
      return `Previsto para ${formatarDia(r.dataPrevista)}`;
  }
}

/** Só antes do dia previsto, como a API (specs/014, FR-030). */
function antecipavel(r: Recebivel): boolean {
  return r.situacao === 'Pendente' && r.dataPrevista > hojeBrasilia();
}

function Item({
  recebivel,
  aoAntecipar,
  aoMudar,
}: {
  recebivel: Recebivel;
  aoAntecipar: (recebivel: Recebivel) => void;
  aoMudar: (erro: MensagemErro) => void;
}) {
  const { aberta, abrir, fechar, gatilho } = useConfirmacao();
  const pode = antecipavel(recebivel);
  const recebido = recebivel.situacao === 'Antecipado' ? recebivel.valorLiquido : recebivel.valorBruto;

  return (
    <li>
      <div className={linhas.linha}>
        <span className={linhas.texto}>
          <span className={linhas.nome}>{recebivel.descricao}</span>
          <span className={linhas.detalhe}>{detalhe(recebivel)}</span>
        </span>
        <SeloSituacao situacao={recebivel.situacao} />
        <span className={linhas.valor}>{formatarCentavos(saldoEmCentavos(recebido))}</span>
        {pode && !aberta ? (
          <Botao variante="secundario" ref={gatilho} onClick={abrir} aria-label={`Antecipar ${recebivel.descricao}`}>
            Antecipar
          </Botao>
        ) : null}
      </div>
      {pode && aberta ? <Antecipacao recebivel={recebivel} aoFechar={fechar} aoAntecipar={aoAntecipar} aoMudar={aoMudar} /> : null}
    </li>
  );
}

/** A situação mudou: a lista é recarregada. */
const MUDOU = new Set(['recebivel-nao-antecipavel', 'recebivel-nao-encontrado', 'idempotencia-divergente']);

function Antecipacao({
  recebivel,
  aoFechar,
  aoAntecipar,
  aoMudar,
}: {
  recebivel: Recebivel;
  aoFechar: () => void;
  aoAntecipar: (recebivel: Recebivel) => void;
  aoMudar: (erro: MensagemErro) => void;
}) {
  const { enviando, erro, executar } = useOperacao(`recebivel:${recebivel.recebivelId}`);
  const bruto = saldoEmCentavos(recebivel.valorBruto);
  const { taxa, liquido } = taxaAntecipacao(bruto);

  async function antecipar() {
    const resultado = await executar((chave) => api.anteciparRecebivel(recebivel.recebivelId, chave));
    if (!resultado) return;
    if (resultado.ok) {
      aoAntecipar(resultado.dados);
    } else if (MUDOU.has(resultado.erro.codigo)) {
      const hoje = resultado.erro.codigo === 'recebivel-nao-antecipavel' && recebivel.dataPrevista <= hojeBrasilia();
      aoMudar(hoje ? { ...resultado.erro, detalhe: MENSAGENS.previstoParaHoje } : resultado.erro);
    }
  }

  return (
    <div className={estilos.confirmacao}>
      <Confirmacao titulo="Antecipar este recebível" aoFechar={() => (enviando ? undefined : aoFechar())}>
        <LinhasConfirmacao>
          <LinhaConfirmacao rotulo="Valor" valor={formatarCentavos(bruto)} />
          <LinhaConfirmacao rotulo={`Taxa (${TAXA_ANTECIPACAO_PERCENTUAL}%)`} valor={`− ${formatarCentavos(taxa)}`} />
          <LinhaConfirmacao rotulo="Você recebe agora" valor={formatarCentavos(liquido)} destaque />
        </LinhasConfirmacao>
        <p className={estilos.nota}>
          Sem antecipar, o valor integral cai em {formatarDia(recebivel.dataPrevista)}. A antecipação não pode ser desfeita.
        </p>
        {erro && !MUDOU.has(erro.codigo) ? <AvisoErro erro={erro} aoTentarDeNovo={() => void antecipar()} /> : null}
        <AcoesConfirmacao>
          <Botao carregando={enviando} onClick={() => void antecipar()}>
            Receber {formatarCentavos(liquido)}
          </Botao>
          <Botao variante="secundario" disabled={enviando} onClick={aoFechar}>
            Voltar
          </Botao>
        </AcoesConfirmacao>
      </Confirmacao>
    </div>
  );
}
