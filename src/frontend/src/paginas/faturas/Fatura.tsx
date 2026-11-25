import { useEffect, useRef, useState } from 'react';
import { Link, useParams } from 'react-router';
import { api, type FaturaDetalhada } from '../../api/cliente';
import { traduzirErro, type MensagemErro } from '../../api/erros';
import { Aviso, AvisoErro, Carregando, Cartao } from '../../design/componentes/Feedback';
import { Botao } from '../../design/componentes/Botao';
import { SeloSituacao } from '../../design/componentes/SeloSituacao';
import { useConfirmacao } from '../../design/componentes/useConfirmacao';
import { formatarDataHora } from '../../formatacao/data';
import { formatarCompetencia, formatarDia, formatarDiaCurto } from '../../formatacao/dia';
import { formatarCentavos, saldoEmCentavos } from '../../formatacao/valor';
import { useTitulo } from '../comum';
import { SomentePerfil } from '../SomentePerfil';
import { useConsulta } from '../useConsulta';
import { diasDeAtraso } from './Faturas';
import estilos from './Faturas.module.css';
import { PagarFatura } from './PagarFatura';

/** Fatura de um mês e o seu pagamento (specs/015-frontend-cartao-credito, US4 e US5; FR-019 e FR-020). */
export function Fatura() {
  useTitulo('Fatura');
  const { competencia = '' } = useParams();

  return (
    <SomentePerfil tipo="PessoaFisica" titulo="Fatura">
      <div className={estilos.pagina}>
        <Link to="/cartao/faturas" className={estilos.link}>
          Voltar às faturas
        </Link>
        <Conteudo key={competencia} competencia={competencia} />
      </div>
    </SomentePerfil>
  );
}

const NAO_ENCONTRADA = traduzirErro(404, { type: 'urn:bank:problema:fatura-nao-encontrada' });

function Conteudo({ competencia }: { competencia: string }) {
  const consulta = useConsulta(competencia, () => api.obterFatura(competencia));

  if (consulta.estado === 'carregando') {
    return <Carregando texto="Carregando a fatura…" />;
  }

  if (consulta.estado === 'erro') {
    const inexistente = consulta.erro.codigo === 'fatura-nao-encontrada' || consulta.erro.codigo === 'validacao';
    return (
      <>
        <h1>Fatura</h1>
        <AvisoErro erro={inexistente ? NAO_ENCONTRADA : consulta.erro} aoTentarDeNovo={consulta.recarregar} />
      </>
    );
  }

  return <Detalhe fatura={consulta.dados} definir={consulta.definir} atualizar={consulta.atualizar} />;
}

function rotuloValor(situacao: string): string {
  switch (situacao) {
    case 'Paga':
      return 'Valor pago';
    case 'Disponivel':
    case 'Vencida':
      return 'Valor a pagar hoje';
    case 'Aberta':
      return 'Total até agora';
    default:
      return 'Total';
  }
}

function Detalhe({
  fatura,
  definir,
  atualizar,
}: {
  fatura: FaturaDetalhada;
  definir: (fatura: FaturaDetalhada) => void;
  atualizar: () => Promise<FaturaDetalhada | null>;
}) {
  const { aberta, abrir, fechar, gatilho } = useConfirmacao();
  const [aviso, definirAviso] = useState<MensagemErro | null>(null);
  const [paga, definirPaga] = useState(false);
  const titulo = useRef<HTMLHeadingElement>(null);
  const pagavel = fatura.situacao === 'Disponivel' || fatura.situacao === 'Vencida';
  const juros = saldoEmCentavos(fatura.juros);

  // Depois de pagar ou de a situação mudar, o foco vai para o título: o botão que o tinha saiu da tela.
  const eraPagavel = useRef(pagavel);
  useEffect(() => {
    if (eraPagavel.current && !pagavel) titulo.current?.focus();
    eraPagavel.current = pagavel;
  }, [pagavel]);

  return (
    <>
      <div className={estilos.cabecalho}>
        <h1 ref={titulo} tabIndex={-1}>
          Fatura de {formatarCompetencia(fatura.competencia)}
        </h1>
        <SeloSituacao situacao={fatura.situacao} />
      </div>
      <p className="apenas-leitor-de-tela" role="status" aria-live="polite">
        {paga ? 'Fatura paga' : ''}
      </p>
      {fatura.situacao === 'Vencida' ? (
        <Aviso variante="atencao" titulo={`Fatura vencida há ${diasDeAtraso(fatura.diasAtraso).replace(' de atraso', '')}`}>
          <p>O cartão está bloqueado para novas compras até o pagamento.</p>
        </Aviso>
      ) : null}
      {aviso ? <AvisoErro erro={aviso} /> : null}

      <div className={estilos.grade}>
        <Cartao rotulo="Resumo" className={estilos.quadro}>
          <div>
            <p className={estilos.apoio}>{rotuloValor(fatura.situacao)}</p>
            <p className={estilos.valorGrande}>{formatarCentavos(saldoEmCentavos(fatura.valorPago ?? fatura.valorAPagar ?? fatura.total))}</p>
          </div>
          <dl className={estilos.linhas}>
            <Linha rotulo="Total das compras" valor={formatarCentavos(saldoEmCentavos(fatura.total))} />
            {juros > 0 ? <Linha rotulo={`Juros (${diasDeAtraso(fatura.diasAtraso)})`} valor={formatarCentavos(juros)} /> : null}
            <Linha rotulo="Período" valor={`${formatarDiaCurto(fatura.inicio)} a ${formatarDiaCurto(fatura.fechamento)}`} />
            <Linha rotulo="Fechamento" valor={formatarDia(fatura.fechamento)} />
            <Linha rotulo="Vencimento" valor={formatarDia(fatura.vencimento)} />
            {fatura.pagaEm ? <Linha rotulo="Paga em" valor={formatarDataHora(fatura.pagaEm)} /> : null}
            {fatura.situacao === 'Fechada' ? <Linha rotulo="Pagamento" valor="Liberado após o processamento" /> : null}
          </dl>
          {pagavel && aberta ? (
            <PagarFatura
              fatura={fatura}
              aoFechar={fechar}
              aoPagar={(atual) => {
                definirAviso(null);
                definirPaga(true);
                definir(atual);
              }}
              aoMudar={(erro) => {
                definirAviso(erro);
                fechar();
                void atualizar();
              }}
            />
          ) : null}
          {pagavel && !aberta ? (
            <Botao ref={gatilho} onClick={abrir}>
              Pagar fatura
            </Botao>
          ) : null}
        </Cartao>

        <Cartao rotulo="Lançamentos" className={estilos.lancamentos}>
          <h2>Lançamentos</h2>
          {fatura.lancamentos.length === 0 ? (
            <p className={estilos.apoio}>Nenhum lançamento ainda. As compras aparecem aqui no próximo dia útil.</p>
          ) : (
            <ul>
              {fatura.lancamentos.map((l, i) => (
                <li key={i}>
                  <span className={estilos.textoLancamento}>
                    <span className={estilos.forte}>{l.estabelecimento}</span>
                    <span className={estilos.legenda}>
                      {l.descricao} · {formatarDiaCurto(l.dataCompra)}
                    </span>
                  </span>
                  <span className={estilos.valor}>{formatarCentavos(saldoEmCentavos(l.valor))}</span>
                </li>
              ))}
            </ul>
          )}
        </Cartao>
      </div>
    </>
  );
}

function Linha({ rotulo, valor }: { rotulo: string; valor: string }) {
  return (
    <div className={estilos.linha}>
      <dt>{rotulo}</dt>
      <dd>{valor}</dd>
    </div>
  );
}
