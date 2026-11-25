import type { CartaoCredito, DadosCartao } from '../api/cliente';
import { IconeOlhoAberto, IconeOlhoRiscado } from '../design/componentes/Icones';
import { lerDigitos } from '../formatacao/contaBancaria';
import { NOME_BANCO } from '../marca';
import estilos from './Cartao.module.css';
import { mascararNumero } from './validacao';

/**
 * Cartão desenhado na tela (specs/015-frontend-cartao-credito, FR-006 e FR-007; research R12). O olho indica o estado:
 * aberto = dados visíveis, riscado = ocultos. Número e CVV são lidos dígito a dígito.
 */
export function CartaoDesenhado({
  cartao,
  dados,
  buscando,
  aoAlternar,
}: {
  cartao: CartaoCredito;
  dados: DadosCartao | null;
  buscando: boolean;
  aoAlternar: () => void;
}) {
  const rotulo = dados ? 'Ocultar dados do cartão' : 'Exibir dados do cartão';

  return (
    <div className={estilos.cartao}>
      <div className={estilos.topo}>
        <span className={estilos.marca}>
          <span className={estilos.marcaIcone} aria-hidden="true">
            1B
          </span>
          {NOME_BANCO}
        </span>
        <span className={estilos.bandeira}>{cartao.bandeira.nome}</span>
      </div>

      <div className={estilos.linhaNumero}>
        <span className={estilos.numero}>
          <span className="apenas-leitor-de-tela">
            {dados ? `Número do cartão: ${lerDigitos(dados.numero)}` : `Número do cartão: oculto, final ${lerDigitos(cartao.ultimosDigitos)}`}
          </span>
          <span aria-hidden="true">{dados ? mascararNumero(dados.numero) : `•••• •••• •••• ${cartao.ultimosDigitos}`}</span>
        </span>
        <button
          type="button"
          className={estilos.olho}
          aria-label={rotulo}
          title={rotulo}
          aria-busy={buscando || undefined}
          onClick={aoAlternar}
        >
          {dados ? <IconeOlhoAberto /> : <IconeOlhoRiscado />}
        </button>
      </div>

      <dl className={estilos.base}>
        <div className={estilos.nome}>
          <dt>Nome</dt>
          <dd title={cartao.nomeImpresso}>{dados?.nomeImpresso ?? cartao.nomeImpresso}</dd>
        </div>
        <div>
          <dt>Validade</dt>
          <dd className={estilos.mono}>{dados?.validade ?? cartao.validade}</dd>
        </div>
        <div>
          <dt>CVV</dt>
          <dd className={estilos.mono}>
            <span className="apenas-leitor-de-tela">{dados ? lerDigitos(dados.cvv) : 'oculto'}</span>
            <span aria-hidden="true">{dados?.cvv ?? '•••'}</span>
          </dd>
        </div>
      </dl>
    </div>
  );
}
