import { Link } from 'react-router';
import { MENSAGENS, type MensagemErro } from '../api/erros';
import { Aviso, AvisoErro } from '../design/componentes/Feedback';
import { formatarCentavos } from '../formatacao/valor';

/**
 * Avisos do fluxo de transferência (specs/007, data-model "Tradução de erros"): o de saldo
 * insuficiente inclui o saldo disponível da carteira.
 */
export function AvisoTransferencia({
  erro,
  saldoCentavos,
  id,
}: {
  erro: MensagemErro;
  saldoCentavos: number | null;
  id?: string;
}) {
  if (erro.codigo === 'saldo-insuficiente') {
    return (
      <Aviso variante="atencao" titulo={MENSAGENS.saldoInsuficiente} {...(id ? { id } : {})}>
        <p>
          {saldoCentavos !== null ? `Você tem ${formatarCentavos(saldoCentavos)} disponíveis. ` : ''}
          {MENSAGENS.saldoInsuficienteDetalhe}
        </p>
      </Aviso>
    );
  }

  return <AvisoErro erro={erro} {...(id ? { id } : {})} />;
}

/** Reenvio já processado (200): nada foi cobrado de novo (FR-012). */
export function AvisoRepetida({ transferenciaId, id }: { transferenciaId: string; id?: string }) {
  return (
    <Aviso variante="info" titulo={MENSAGENS.transferenciaRepetida} {...(id ? { id } : {})}>
      <p>
        {MENSAGENS.transferenciaRepetidaDetalhe} <Link to={`/transferencias/${transferenciaId}`}>Ver comprovante</Link>
      </p>
    </Aviso>
  );
}

/** Conta PJ: transferir não está disponível (FR-001). */
export function IndisponivelPj() {
  return (
    <Aviso variante="neutro" titulo={MENSAGENS.indisponivelPj}>
      <p>{MENSAGENS.indisponivelPjDetalhe}</p>
      <p>
        <Link to="/">Voltar ao início</Link>
      </p>
    </Aviso>
  );
}

/** Foca o primeiro campo inválido ou aviso após uma recusa (WCAG 3.3.1). */
export function focarPrimeiroErro(raiz: HTMLElement | null): void {
  setTimeout(() => {
    raiz?.querySelector<HTMLElement>('[aria-invalid="true"], [role="alert"], [role="status"][tabindex="-1"]')?.focus();
  });
}
