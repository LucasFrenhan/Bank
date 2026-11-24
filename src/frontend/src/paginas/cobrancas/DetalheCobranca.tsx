import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router';
import { api, type Cobranca } from '../../api/cliente';
import { MENSAGENS, type MensagemErro } from '../../api/erros';
import { formatarCodigo, linkDaCobranca, textoDeCompartilhamento } from '../../cobrancas/codigo';
import { Botao } from '../../design/componentes/Botao';
import { AcoesConfirmacao, Confirmacao } from '../../design/componentes/Confirmacao';
import { Aviso, AvisoErro, Cartao } from '../../design/componentes/Feedback';
import { SeloSituacao } from '../../design/componentes/SeloSituacao';
import { useConfirmacao } from '../../design/componentes/useConfirmacao';
import { formatarDataHora, formatarDataRelativa } from '../../formatacao/data';
import { formatarCentavos, saldoEmCentavos } from '../../formatacao/valor';
import { useTitulo } from '../comum';
import { TextoAvisoCopia } from '../TextoAvisoCopia';
import { useCopia } from '../useCopia';
import type { CobrancaPronta } from './Cobranca';
import estilos from './Cobrancas.module.css';
import { Linha } from './ComprovanteCartao';
import { useAtualizacaoAutomatica } from './useAtualizacaoAutomatica';

const MOTIVOS = new Map([
  ['CartaoNaoAutorizado', 'Cartão não autorizado'],
  ['FaturaVencida', 'Fatura vencida'],
  ['LimiteInsuficiente', 'Limite insuficiente'],
]);

const ANUNCIOS: Record<string, string> = { Paga: 'Cobrança paga', Cancelada: 'Cobrança cancelada' };

/** Cobrança vista por quem a emitiu (specs/015-frontend-cartao-credito, US2; FR-012). */
export function DetalheCobranca({ consulta }: { consulta: CobrancaPronta }) {
  useTitulo('Cobrança');
  const cobranca = consulta.dados;
  const pendente = cobranca.status === 'Pendente';
  const { aberta, abrir, fechar, gatilho } = useConfirmacao();
  const [aviso, definirAviso] = useState<MensagemErro | null>(null);
  const [anuncio, definirAnuncio] = useState('');
  const [cancelando, definirCancelando] = useState(false);
  const titulo = useRef<HTMLHeadingElement>(null);
  const cancelandoRef = useRef(false);

  useAtualizacaoAutomatica(pendente, async () => {
    const resultado = await api.consultarCobranca(cobranca.codigo, { silenciosa: true });
    // Enquanto um cancelamento está em andamento, quem decide é a resposta dele.
    if (resultado.ok && !cancelandoRef.current) consulta.definir(resultado.dados);
  });

  // A mudança de situação é anunciada; se a confirmação estava aberta, o foco vai para o título.
  const [statusAnterior, definirStatusAnterior] = useState(cobranca.status);
  if (statusAnterior !== cobranca.status) {
    definirStatusAnterior(cobranca.status);
    definirAnuncio(ANUNCIOS[cobranca.status] ?? '');
  }
  const confirmando = pendente && aberta;
  const estavaConfirmando = useRef(false);
  useEffect(() => {
    if (estavaConfirmando.current && !pendente) titulo.current?.focus();
    estavaConfirmando.current = confirmando;
  }, [confirmando, pendente]);

  async function cancelar() {
    if (cancelandoRef.current) return;
    cancelandoRef.current = true;
    definirCancelando(true);
    definirAviso(null);
    const resultado = await api.cancelarCobranca(cobranca.cobrancaId);
    cancelandoRef.current = false;
    definirCancelando(false);

    if (resultado.ok) {
      consulta.definir(resultado.dados);
    } else if (resultado.erro.codigo === 'cobranca-nao-pendente') {
      const atual = await consulta.atualizar();
      definirAviso(avisoCancelamento(resultado.erro, atual?.status));
    } else if (!resultado.erro.encerrarSessao) {
      definirAviso(resultado.erro);
    }
  }

  return (
    <div className={estilos.pagina}>
      <div>
        <Link to="/cobrancas" className={estilos.link}>
          Voltar às cobranças
        </Link>
        <div className={estilos.cabecalhoDetalhe}>
          <h1 ref={titulo} tabIndex={-1} className={estilos.valorGrande}>
            {formatarCentavos(saldoEmCentavos(cobranca.valor))}
          </h1>
          <SeloSituacao situacao={cobranca.status} />
        </div>
        <p className={estilos.apoio}>{cobranca.descricao}</p>
      </div>
      <p className="apenas-leitor-de-tela" role="status" aria-live="polite">
        {anuncio}
      </p>
      {aviso ? <AvisoErro erro={aviso} aoAgir={() => titulo.current?.focus()} aoTentarDeNovo={() => void cancelar()} /> : null}

      <div className={estilos.grade}>
        {pendente ? <LinkDePagamento cobranca={cobranca} /> : null}
        <Cartao className={estilos.quadro} rotulo="Detalhes">
          <dl className={[estilos.linhas, estilos.semMargem].join(' ')}>
            <Linha rotulo="Código" valor={formatarCodigo(cobranca.codigo)} />
            <Linha rotulo="Emitida em" valor={formatarDataHora(cobranca.criadoEm)} />
            {cobranca.pagaEm ? <Linha rotulo="Paga em" valor={formatarDataHora(cobranca.pagaEm)} /> : null}
            {cobranca.canceladaEm ? <Linha rotulo="Cancelada em" valor={formatarDataHora(cobranca.canceladaEm)} /> : null}
            {cobranca.pagamento ? (
              <>
                <Linha rotulo="Cartão" valor={`${cobranca.pagamento.bandeira.nome} final ${cobranca.pagamento.ultimosDigitosCartao}`} />
                <Linha rotulo="Código de autorização" valor={cobranca.pagamento.codigoAutorizacao} />
              </>
            ) : null}
            {cobranca.ultimaRecusa ? (
              <Linha
                rotulo="Última recusa"
                valor={`${MOTIVOS.get(cobranca.ultimaRecusa.motivo) ?? 'Pagamento recusado'} · ${formatarDataRelativa(cobranca.ultimaRecusa.em)}`}
              />
            ) : null}
          </dl>
          {cobranca.status === 'Paga' ? (
            <Link to="/cobrancas/recebiveis" className={estilos.link}>
              Ver recebíveis
            </Link>
          ) : null}
          {confirmando ? (
            <Confirmacao titulo="Cancelar esta cobrança?" aoFechar={fechar}>
              <p className={estilos.apoio}>O link para de funcionar e não dá para desfazer.</p>
              <AcoesConfirmacao>
                <Botao carregando={cancelando} onClick={() => void cancelar()}>
                  Cancelar cobrança
                </Botao>
                <Botao variante="secundario" disabled={cancelando} onClick={fechar}>
                  Voltar
                </Botao>
              </AcoesConfirmacao>
            </Confirmacao>
          ) : null}
          {pendente && !aberta ? (
            <Botao variante="secundario" ref={gatilho} onClick={abrir}>
              Cancelar cobrança
            </Botao>
          ) : null}
        </Cartao>
      </div>
    </div>
  );
}

/** Texto próprio de quem tentou cancelar (contracts/frontend.md, "Tradução de erros"). */
function avisoCancelamento(erro: MensagemErro, status: string | undefined): MensagemErro {
  return status === 'Cancelada'
    ? {
        ...erro,
        mensagem: MENSAGENS.cobrancaJaCancelada,
        detalhe: MENSAGENS.situacaoAtualizada,
        proximaAcao: { rotulo: 'Ver cobranças', para: '/cobrancas' },
      }
    : {
        ...erro,
        mensagem: MENSAGENS.cobrancaAcabouDeSerPaga,
        detalhe: MENSAGENS.cobrancaAcabouDeSerPagaDetalhe,
        proximaAcao: { rotulo: 'Ver pagamento' },
      };
}

const MENSAGENS_COPIA = {
  sucesso: 'Link copiado',
  falha: 'Não foi possível copiar. Selecione o link e copie manualmente.',
};

/** Código e link, só enquanto pendente (FR-012). */
function LinkDePagamento({ cobranca }: { cobranca: Cobranca }) {
  const { aviso, copiar } = useCopia();
  const [falhaCompartilhar, definirFalhaCompartilhar] = useState(false);
  const link = linkDaCobranca(cobranca.codigo);
  const podeCompartilhar = typeof navigator.share === 'function';

  async function compartilhar() {
    definirFalhaCompartilhar(false);
    try {
      await navigator.share({ text: textoDeCompartilhamento(cobranca) });
    } catch (erro) {
      if (!(erro instanceof DOMException && erro.name === 'AbortError')) definirFalhaCompartilhar(true);
    }
  }

  return (
    <Cartao className={estilos.quadro} rotulo="Link de pagamento">
      <h2>Link de pagamento</h2>
      <p className={estilos.apoio}>Envie o link ou diga o código para o cliente Pessoa Física que vai pagar.</p>
      <div className={estilos.caixaCodigo}>
        <span className={estilos.legenda}>Código da cobrança</span>
        <span className={estilos.codigo}>{formatarCodigo(cobranca.codigo)}</span>
        <span className={estilos.endereco}>{link}</span>
      </div>
      <div className={estilos.acoes}>
        <Botao onClick={() => void copiar(link, MENSAGENS_COPIA)}>Copiar link</Botao>
        {podeCompartilhar ? (
          <Botao variante="secundario" onClick={() => void compartilhar()}>
            Compartilhar
          </Botao>
        ) : null}
      </div>
      <TextoAvisoCopia aviso={aviso} />
      {falhaCompartilhar ? (
        <Aviso variante="neutro" titulo="Não foi possível compartilhar">
          <p>Copie o link e envie por onde preferir.</p>
        </Aviso>
      ) : null}
    </Cartao>
  );
}
