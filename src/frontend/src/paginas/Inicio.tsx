import type { ReactNode } from 'react';
import { Botao, BotaoLink } from '../design/componentes/Botao';
import { mascararCnpj } from '../formatacao/mascaras';
import { useTitular } from './useTitular';
import { AvisoErro, Cartao, Carregando, EstadoVazio, Selo } from '../design/componentes/Feedback';
import { IconeExtrato, IconeTransferir } from '../design/componentes/Icones';
import { formatarDataHora } from '../formatacao/data';
import { formatarMoeda } from '../formatacao/moeda';
import type { ConsultaExtrato, ItemExtrato, MinhaCarteira } from '../api/cliente';
import { NOME_BANCO } from '../marca';
import { Link } from 'react-router';
import { ListaLancamentos } from '../design/componentes/ListaLancamentos';
import { formatarDataRelativa } from '../formatacao/data';
import { descricaoItem, ehEntrada, ehTransferencia, linkComprovante, nomeContraparte, valorComSinal } from './lancamentos';
import { useExtrato } from './useExtrato';
import { useSessaoAtiva } from '../sessao/ContextoSessao';
import { NOME_TIPO_CONTA } from '../sessao/permissoes';
import estilos from './Autenticado.module.css';
import { DadosDaConta } from './DadosDaConta';
import { primeiroNome, useTitulo } from './comum';
import { TextoAvisoCopia } from './TextoAvisoCopia';
import { useCarteira } from './useCarteira';
import { useCopia } from './useCopia';

/** Tela inicial PF/PJ (spec 004 US3; specs/007 US5, FR-022 a FR-024; specs/013). */
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
            <BotaoLink para="/transferir" variante="claro" icone={<IconeTransferir />}>
              Transferir
            </BotaoLink>
            <BotaoLink para="/extrato" variante="contornoClaro" icone={<IconeExtrato />}>
              Ver extrato
            </BotaoLink>
          </div>
        </Saldo>
        <DadosDaConta conta={carteira.contaBancaria} documento="CPF" />
      </div>

      <Movimentacoes titulo="Últimas movimentações" consulta={CONSULTA_PF} verTudo="/extrato" detalhe={detalhePf} />
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

      <div className={[estilos.resumo, estilos.resumoPj].join(' ')}>
        <Saldo carteira={carteira} />
        <div className={estilos.colunaCartoes}>
          <Cartao className={estilos.secao}>
            <div className={estilos.cabecalhoSecao}>
              <h2>Receber transferências</h2>
            </div>
            <p className={estilos.avisoPj}>
              {`Clientes com conta ${NOME_BANCO} transferem para você informando o seu CNPJ. `}
              Contas Pessoa Jurídica não enviam transferências.
            </p>
            <CnpjParaReceber />
          </Cartao>
          <DadosDaConta conta={carteira.contaBancaria} documento="CNPJ" />
        </div>
      </div>

      <Movimentacoes
        titulo="Últimos recebimentos"
        consulta={CONSULTA_PJ}
        verTudo="/extrato?direcao=entradas"
        detalhe={detalhePj}
      />
    </div>
  );
}

/**
 * CNPJ da empresa para quem vai transferir, com "Copiar" (FR-017a). O valor copiado vai sem
 * máscara; a confirmação é anunciada por leitores de tela.
 */
function CnpjParaReceber() {
  const titular = useTitular();
  const { aviso, copiar } = useCopia();

  if (titular.estado !== 'pronto') {
    return null;
  }

  const { documento } = titular.titular;

  const mensagens = {
    sucesso: 'CNPJ copiado',
    falha: 'Não foi possível copiar. Selecione o CNPJ e copie manualmente.',
  };

  return (
    <div className={estilos.cnpj}>
      <p className={estilos.rotuloCnpj}>
        Seu CNPJ
      </p>
      <div className={estilos.linhaCnpj}>
        <span className={estilos.valorCnpj}>
          {mascararCnpj(documento)}
        </span>
        <Botao variante="secundario" onClick={() => void copiar(documento, mensagens)} aria-label="Copiar CNPJ">
          Copiar
        </Botao>
      </div>
      <TextoAvisoCopia aviso={aviso} />
    </div>
  );
}

/* ---------- Movimentações reais (specs/007, US5; research R8) ---------- */

const CONSULTA_PF: ConsultaExtrato = { tamanhoPagina: 5 };
const CONSULTA_PJ: ConsultaExtrato = { tamanhoPagina: 5, direcao: 'Entrada' };

export const VAZIO_MOVIMENTACOES = {
  titulo: 'Nenhuma movimentação ainda',
  descricao: 'Quando você enviar ou receber uma transferência, ela aparece aqui.',
};

function detalhePf(item: ItemExtrato): string {
  const tipo = !ehTransferencia(item) ? descricaoItem(item) : ehEntrada(item) ? 'Transferência recebida' : 'Transferência enviada';
  return `${tipo} · ${formatarDataRelativa(item.criadoEm)}`;
}

function detalhePj(item: ItemExtrato): string {
  return `${descricaoItem(item)} · ${formatarDataRelativa(item.criadoEm)}`;
}

function Movimentacoes({
  titulo,
  consulta,
  verTudo,
  detalhe,
}: {
  titulo: string;
  consulta: ConsultaExtrato;
  verTudo: string;
  detalhe: (item: ItemExtrato) => string;
}) {
  const extrato = useExtrato(consulta);

  return (
    <Cartao className={estilos.secao} rotulo={titulo}>
      <div className={[estilos.cabecalhoSecao, estilos.cabecalhoComAcao].join(' ')}>
        <h2>{titulo}</h2>
        <Link to={verTudo} className={estilos.verTudo}>
          Ver tudo
        </Link>
      </div>
      {extrato.estado === 'carregando' ? <Carregando texto="Carregando movimentações…" /> : null}
      {extrato.estado === 'erro' ? (
        <div className={estilos.avisoPj}>
          <AvisoErro erro={extrato.erro} aoTentarDeNovo={extrato.recarregar} />
        </div>
      ) : null}
      {extrato.estado === 'pronto' && extrato.extrato.itens.length === 0 ? (
        <EstadoVazio titulo={VAZIO_MOVIMENTACOES.titulo} descricao={VAZIO_MOVIMENTACOES.descricao} />
      ) : null}
      {extrato.estado === 'pronto' && extrato.extrato.itens.length > 0 ? (
        <ListaLancamentos
          rotulo={titulo}
          linhas={extrato.extrato.itens.map((item) => ({
            id: item.lancamentoId,
            entrada: ehEntrada(item),
            nome: nomeContraparte(item),
            detalhe: detalhe(item),
            valor: valorComSinal(item),
            para: linkComprovante(item),
          }))}
        />
      ) : null}
    </Cartao>
  );
}
