import { Link } from 'react-router';
import type { Cobranca } from '../../api/cliente';
import { MENSAGENS, traduzirErro } from '../../api/erros';
import { Aviso, AvisoErro } from '../../design/componentes/Feedback';
import { useTitulo } from '../comum';
import estilos from './Cobrancas.module.css';
import { ResumoCobranca } from './ResumoCobranca';

/** Cobrança que a pessoa vê mas não paga: outra PJ, ou já paga por outra pessoa ou cancelada (specs/015, US3). */
export function CobrancaSemAcao({ cobranca, motivo }: { cobranca: Cobranca; motivo: 'pj' | 'indisponivel' }) {
  useTitulo('Cobrança');

  return (
    <div className={estilos.pagina}>
      <h1>Cobrança</h1>
      <ResumoCobranca cobranca={cobranca} />
      {motivo === 'pj' ? (
        <Aviso variante="neutro" titulo={MENSAGENS.pagamentoSoPf}>
          <p>{MENSAGENS.pagamentoSoPfDetalhe}</p>
          <p>
            <Link to="/">Ir para o início</Link>
          </p>
        </Aviso>
      ) : (
        <AvisoErro erro={traduzirErro(409, { type: 'urn:bank:problema:cobranca-nao-pendente' })} />
      )}
    </div>
  );
}
