import { Link } from 'react-router';
import { api, type Fatura } from '../../api/cliente';
import { Aviso } from '../../design/componentes/Feedback';
import { formatarDiaCurto, nomeMes } from '../../formatacao/dia';
import { formatarCentavos, saldoEmCentavos } from '../../formatacao/valor';
import { useConsulta } from '../useConsulta';

/** A mais antiga a pagar; vencida ou disponível (specs/015, data-model "Aviso de fatura"). */
export function faturaAPagar(faturas: Fatura[]): Fatura | null {
  return faturas.filter((f) => f.situacao === 'Vencida' || f.situacao === 'Disponivel').sort((a, b) => a.competencia.localeCompare(b.competencia))[0] ?? null;
}

/**
 * Aviso no início da PF (specs/015-frontend-cartao-credito, FR-019a; research R11). Carregando, erro, sem cartão ou
 * nada a pagar: o início fica como antes.
 */
export function AvisoFatura() {
  const consulta = useConsulta('aviso-fatura', () => api.listarFaturas({ tamanhoPagina: 100 }));
  const fatura = consulta.estado === 'pronto' ? faturaAPagar(consulta.dados.itens) : null;
  if (!fatura) return null;

  const valor = formatarCentavos(saldoEmCentavos(fatura.valorAPagar ?? fatura.total));
  const vencida = fatura.situacao === 'Vencida';

  return (
    <section aria-label="Aviso de fatura">
      <Aviso
        variante={vencida ? 'atencao' : 'info'}
        titulo={vencida ? `Fatura vencida — ${valor} hoje.` : `Fatura de ${nomeMes(fatura.competencia)} — ${valor}, vence em ${formatarDiaCurto(fatura.vencimento)}.`}
      >
        {vencida ? <p>O cartão está bloqueado.</p> : null}
        <p>
          <Link to={`/cartao/faturas/${fatura.competencia}`}>Ver fatura</Link>
        </p>
      </Aviso>
    </section>
  );
}
