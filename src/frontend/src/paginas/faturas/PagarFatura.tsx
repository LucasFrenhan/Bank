import { api, type FaturaDetalhada } from '../../api/cliente';
import { traduzirErro, type MensagemErro } from '../../api/erros';
import { Botao } from '../../design/componentes/Botao';
import { AcoesConfirmacao, Confirmacao, LinhaConfirmacao, LinhasConfirmacao } from '../../design/componentes/Confirmacao';
import { AvisoErro, Carregando } from '../../design/componentes/Feedback';
import { formatarCentavos, saldoEmCentavos } from '../../formatacao/valor';
import { useOperacao } from '../../operacoes/useOperacao';
import { useConsulta } from '../useConsulta';

/** A situação mudou: a fatura é recarregada em vez de oferecer o pagamento de novo. */
const MUDOU = new Set(['fatura-nao-disponivel', 'fatura-nao-encontrada', 'idempotencia-divergente']);

/** Confirmação e pagamento do total da fatura com o saldo (specs/015-frontend-cartao-credito, US5; FR-020). */
export function PagarFatura({
  fatura,
  aoFechar,
  aoPagar,
  aoMudar,
}: {
  fatura: FaturaDetalhada;
  aoFechar: () => void;
  aoPagar: (fatura: FaturaDetalhada) => void;
  aoMudar: (erro: MensagemErro) => void;
}) {
  const carteira = useConsulta('carteira', api.obterMinhaCarteira);
  const { enviando, erro, executar } = useOperacao(`fatura:${fatura.competencia}`);
  const valor = saldoEmCentavos(fatura.valorAPagar ?? fatura.total);
  const juros = saldoEmCentavos(fatura.juros);
  const saldo = carteira.estado === 'pronto' ? saldoEmCentavos(carteira.dados.saldo) : null;
  const insuficiente = (saldo !== null && saldo < valor) || erro?.codigo === 'saldo-insuficiente';

  async function pagar() {
    const resultado = await executar((chave) => api.pagarFatura(fatura.competencia, chave));
    if (!resultado) return;
    if (resultado.ok) {
      aoPagar(resultado.dados);
    } else if (MUDOU.has(resultado.erro.codigo)) {
      aoMudar(resultado.erro);
    } else if (resultado.erro.codigo === 'saldo-insuficiente') {
      void carteira.atualizar();
    }
  }

  const avisoDeSaldo: MensagemErro | null =
    insuficiente && saldo !== null
      ? {
          ...traduzirErro(422, { type: 'urn:bank:problema:saldo-insuficiente' }, 'fatura'),
          detalhe: `Você tem ${formatarCentavos(saldo)} e a fatura é de ${formatarCentavos(valor)}.`,
        }
      : null;

  return (
    <Confirmacao titulo="Confira antes de pagar" aoFechar={() => (enviando ? undefined : aoFechar())}>
      {carteira.estado === 'carregando' ? <Carregando texto="Consultando o saldo…" /> : null}
      {carteira.estado === 'erro' ? <AvisoErro erro={carteira.erro} aoTentarDeNovo={carteira.recarregar} /> : null}
      {saldo !== null ? (
        <LinhasConfirmacao>
          {juros > 0 ? (
            <>
              <LinhaConfirmacao rotulo="Total das compras" valor={formatarCentavos(saldoEmCentavos(fatura.total))} />
              <LinhaConfirmacao rotulo="Juros" valor={formatarCentavos(juros)} />
            </>
          ) : null}
          <LinhaConfirmacao rotulo="Valor a pagar" valor={formatarCentavos(valor)} destaque={juros > 0} />
          <LinhaConfirmacao rotulo="Seu saldo" valor={formatarCentavos(saldo)} />
          {insuficiente ? null : <LinhaConfirmacao rotulo="Saldo depois" valor={formatarCentavos(saldo - valor)} />}
        </LinhasConfirmacao>
      ) : null}
      {avisoDeSaldo ? <AvisoErro erro={avisoDeSaldo} aoAgir={aoFechar} /> : null}
      {erro && !MUDOU.has(erro.codigo) && !avisoDeSaldo ? <AvisoErro erro={erro} aoTentarDeNovo={() => void pagar()} aoAgir={aoFechar} /> : null}
      <AcoesConfirmacao>
        <Botao carregando={enviando} disabled={enviando || saldo === null || insuficiente} onClick={() => void pagar()}>
          Pagar {formatarCentavos(valor)}
        </Botao>
        <Botao variante="secundario" disabled={enviando} onClick={aoFechar}>
          Voltar
        </Botao>
      </AcoesConfirmacao>
    </Confirmacao>
  );
}
