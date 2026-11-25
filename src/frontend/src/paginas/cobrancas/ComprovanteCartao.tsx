import { useEffect, useRef } from 'react';
import type { Cobranca } from '../../api/cliente';
import { BotaoLink } from '../../design/componentes/Botao';
import { Cartao } from '../../design/componentes/Feedback';
import { IconeConfirmado } from '../../design/componentes/Icones';
import { formatarDataHora } from '../../formatacao/data';
import { mascararCnpj } from '../../formatacao/mascaras';
import { formatarCentavos, saldoEmCentavos } from '../../formatacao/valor';
import { useTitulo } from '../comum';
import estilos from './Cobrancas.module.css';

/** Comprovante do pagador, também ao reabrir o endereço da cobrança (specs/015-frontend-cartao-credito, FR-018). */
export function ComprovanteCartao({ cobranca }: { cobranca: Cobranca }) {
  useTitulo('Pagamento aprovado');
  const titulo = useRef<HTMLHeadingElement>(null);
  const { pagamento, pagaEm, lojista } = cobranca;

  // Anuncia a aprovação a leitores de tela: o formulário com o foco acabou de sair.
  useEffect(() => titulo.current?.focus(), []);

  return (
    <div className={estilos.pagina}>
      <div className={estilos.sucesso}>
        <span className={estilos.iconeSucesso} aria-hidden="true">
          <IconeConfirmado />
        </span>
        <h1 ref={titulo} tabIndex={-1}>
          Pagamento aprovado
        </h1>
        <p className={estilos.apoio}>
          {formatarCentavos(saldoEmCentavos(cobranca.valor))} para {lojista.nome}
        </p>
      </div>
      <Cartao rotulo="Comprovante">
        <dl className={estilos.linhas}>
          <Linha rotulo="Para" valor={lojista.nome} />
          <Linha rotulo="CNPJ" valor={mascararCnpj(lojista.documento)} />
          <Linha rotulo="Descrição" valor={cobranca.descricao} />
          <Linha rotulo="Valor" valor={formatarCentavos(saldoEmCentavos(cobranca.valor))} />
          {pagaEm ? <Linha rotulo="Data" valor={formatarDataHora(pagaEm)} /> : null}
          {pagamento ? (
            <>
              <Linha rotulo="Cartão" valor={`${pagamento.bandeira.nome} final ${pagamento.ultimosDigitosCartao}`} />
              <Linha rotulo="Código de autorização" valor={pagamento.codigoAutorizacao} />
            </>
          ) : null}
        </dl>
      </Cartao>
      <p className={[estilos.apoio, estilos.centro].join(' ')}>A compra aparece na fatura no próximo dia útil.</p>
      <div className={estilos.acoes}>
        <BotaoLink para="/cartao/faturas">Ver faturas</BotaoLink>
        <BotaoLink para="/cartao" variante="secundario">
          Voltar ao cartão
        </BotaoLink>
      </div>
    </div>
  );
}

export function Linha({ rotulo, valor }: { rotulo: string; valor: string }) {
  return (
    <div className={estilos.linha}>
      <dt>{rotulo}</dt>
      <dd>{valor}</dd>
    </div>
  );
}
