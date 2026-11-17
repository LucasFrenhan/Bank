import { useState, type ReactNode } from 'react';
import { AcaoEmBreve, Botao } from '../design/componentes/Botao';
import { mascararCnpj } from '../formatacao/mascaras';
import { useTitular } from './useTitular';
import { AvisoErro, Cartao, Carregando, EstadoVazio, Selo } from '../design/componentes/Feedback';
import { IconeExtrato, IconeTransferir } from '../design/componentes/Icones';
import { formatarDataHora } from '../formatacao/data';
import { formatarMoeda } from '../formatacao/moeda';
import type { MinhaCarteira } from '../api/cliente';
import { useSessaoAtiva } from '../sessao/ContextoSessao';
import { NOME_TIPO_CONTA } from '../sessao/permissoes';
import estilos from './Autenticado.module.css';
import { primeiroNome, useTitulo } from './comum';
import { useCarteira } from './useCarteira';

/** Tela inicial PF/PJ (US3; FR-014 a FR-016). */
export function Inicio() {
  useTitulo('Início');
  const sessao = useSessaoAtiva();
  const carteira = useCarteira();
  const pj = sessao.tipoConta === 'PessoaJuridica';

  if (carteira.estado === 'carregando') {
    return <Carregando texto="Carregando sua carteira…" />;
  }

  if (carteira.estado === 'erro') {
    return (
      <div className={estilos.pagina}>
        <h1>Início</h1>
        <AvisoErro erro={carteira.erro} aoTentarDeNovo={carteira.recarregar} />
      </div>
    );
  }

  return pj ? <InicioPj carteira={carteira.carteira} /> : <InicioPf carteira={carteira.carteira} />;
}

function Saldo({ carteira, children }: { carteira: MinhaCarteira; children?: ReactNode }) {
  return (
    <section className={estilos.saldo} aria-labelledby="rotulo-saldo">
      <p id="rotulo-saldo" className={estilos.rotuloSaldo}>
        Saldo disponível
      </p>
      <p className={estilos.valorSaldo}>{formatarMoeda(carteira.saldo)}</p>
      {children}
      <p className={estilos.atualizado}>Atualizado em {formatarDataHora(carteira.atualizadoEm)}.</p>
    </section>
  );
}

function InicioPf({ carteira }: { carteira: MinhaCarteira }) {
  return (
    <div className={estilos.pagina}>
      <div className={estilos.saudacao}>
        <h1>Olá, {primeiroNome(carteira.nomeTitular)}</h1>
        <Selo>{NOME_TIPO_CONTA.PessoaFisica}</Selo>
      </div>

      <div className={estilos.resumo}>
        <Saldo carteira={carteira}>
          <div className={estilos.acoes}>
            <AcaoEmBreve rotulo="Transferir" variante="claro" icone={<IconeTransferir />} />
            <AcaoEmBreve rotulo="Ver extrato" variante="contornoClaro" icone={<IconeExtrato />} />
          </div>
        </Saldo>
      </div>

      <Cartao className={estilos.secao}>
        <div className={estilos.cabecalhoSecao}>
          <h2>Últimas movimentações</h2>
        </div>
        <EstadoVazio
          titulo="Suas movimentações aparecerão aqui"
          descricao="Transferências enviadas e recebidas ficam disponíveis em breve."
        />
      </Cartao>
    </div>
  );
}

function InicioPj({ carteira }: { carteira: MinhaCarteira }) {
  return (
    <div className={estilos.pagina}>
      <div className={estilos.saudacao}>
        <h1>{carteira.nomeTitular}</h1>
        <Selo>{NOME_TIPO_CONTA.PessoaJuridica}</Selo>
      </div>

      <div className={estilos.resumo}>
        <Saldo carteira={carteira} />
        <Cartao className={estilos.secao}>
          <div className={estilos.cabecalhoSecao}>
            <h2>Receber transferências</h2>
          </div>
          <p className={estilos.avisoPj}>
            Clientes com conta Bank transferem para você informando o seu CNPJ. Contas Pessoa Jurídica não enviam
            transferências.
          </p>
          <CnpjParaReceber />
        </Cartao>
      </div>

      <Cartao className={estilos.secao}>
        <div className={estilos.cabecalhoSecao}>
          <h2>Últimos recebimentos</h2>
        </div>
        <EstadoVazio titulo="Suas movimentações aparecerão aqui" descricao="Os recebimentos ficam disponíveis em breve." />
      </Cartao>
    </div>
  );
}

/**
 * CNPJ da empresa para quem vai transferir, com "Copiar" (FR-017a). O valor copiado vai sem
 * máscara; a confirmação é anunciada por leitores de tela.
 */
function CnpjParaReceber() {
  const titular = useTitular();
  const [aviso, definirAviso] = useState('');

  if (titular.estado !== 'pronto') {
    return null;
  }

  const { documento } = titular.titular;

  async function copiar() {
    try {
      await navigator.clipboard.writeText(documento);
      definirAviso('CNPJ copiado');
    } catch {
      definirAviso('Não foi possível copiar. Selecione o CNPJ e copie manualmente.');
    }
  }

  return (
    <div className={estilos.cnpj}>
      <p className={estilos.rotuloCnpj}>
        Seu CNPJ
      </p>
      <div className={estilos.linhaCnpj}>
        <span className={estilos.valorCnpj}>
          {mascararCnpj(documento)}
        </span>
        <Botao variante="secundario" onClick={() => void copiar()} aria-label="Copiar CNPJ">
          Copiar
        </Botao>
      </div>
      <p className={estilos.avisoCopia} role="status" aria-live="polite">
        {aviso}
      </p>
    </div>
  );
}
