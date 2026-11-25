import type { Cobranca } from '../../api/cliente';
import { Cartao } from '../../design/componentes/Feedback';
import { mascararCnpj } from '../../formatacao/mascaras';
import { formatarCentavos, saldoEmCentavos } from '../../formatacao/valor';
import estilos from './Cobrancas.module.css';

/** Valor, lojista e descrição, antes de qualquer ação (specs/015, FR-013). */
export function ResumoCobranca({ cobranca }: { cobranca: Cobranca }) {
  return (
    <Cartao className={estilos.quadro} rotulo="Cobrança">
      <dl className={estilos.resumo}>
        <div>
          <dt>Valor</dt>
          <dd className={estilos.valorGrande}>{formatarCentavos(saldoEmCentavos(cobranca.valor))}</dd>
        </div>
        <div>
          <dt>Para</dt>
          <dd>
            <span className={estilos.forte}>{cobranca.lojista.nome}</span>
            <span className={estilos.apoio}>CNPJ {mascararCnpj(cobranca.lojista.documento)}</span>
          </dd>
        </div>
        <div>
          <dt>Descrição</dt>
          <dd>{cobranca.descricao}</dd>
        </div>
      </dl>
    </Cartao>
  );
}
