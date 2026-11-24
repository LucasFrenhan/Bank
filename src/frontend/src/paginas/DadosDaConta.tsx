import type { ReactNode } from 'react';
import type { ContaBancaria } from '../api/cliente';
import { Cartao } from '../design/componentes/Feedback';
import { contaComDigito, lerConta, lerDigitos, nomeBanco, nomeTipoContaBancaria } from '../formatacao/contaBancaria';
import { NOME_BANCO } from '../marca';
import estilos from './Autenticado.module.css';

/**
 * Dados bancários da própria conta (specs/013, US1). Os valores vêm da API como texto; agência e
 * conta são lidas dígito a dígito por leitores de tela (FR-004).
 */
export function DadosDaConta({ conta, documento }: { conta: ContaBancaria; documento: 'CPF' | 'CNPJ' }) {
  return (
    <Cartao className={estilos.secao} rotulo="Dados da conta">
      <div className={estilos.cabecalhoSecao}>
        <h2>Dados da conta</h2>
      </div>
      <dl className={estilos.dadosConta}>
        <Campo rotulo="Banco">{nomeBanco(conta)}</Campo>
        <Campo rotulo="Agência">
          <Digitos visivel={conta.agencia} leitura={lerDigitos(conta.agencia)} />
        </Campo>
        <Campo rotulo="Conta">
          <Digitos visivel={contaComDigito(conta)} leitura={lerConta(conta)} />
        </Campo>
        <Campo rotulo="Tipo">{nomeTipoContaBancaria(conta.tipo)}</Campo>
      </dl>
      <p className={estilos.apoioConta}>{`Para receber de clientes ${NOME_BANCO}, informe o seu ${documento}.`}</p>
    </Cartao>
  );
}

function Campo({ rotulo, children }: { rotulo: string; children: ReactNode }) {
  return (
    <div className={estilos.campoConta}>
      <dt>{rotulo}</dt>
      <dd>{children}</dd>
    </div>
  );
}

function Digitos({ visivel, leitura }: { visivel: string; leitura: string }) {
  return (
    <>
      <span aria-hidden="true">{visivel}</span>
      <span className="apenas-leitor-de-tela">{leitura}</span>
    </>
  );
}
