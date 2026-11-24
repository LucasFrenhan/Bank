import { useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router';
import { api, type CartaoCredito } from '../../api/cliente';
import type { MensagemErro } from '../../api/erros';
import { CartaoDesenhado } from '../../cartao/CartaoDesenhado';
import { useDadosCartao } from '../../cartao/useDadosCartao';
import { caminhoDaCobranca, extrairCodigo } from '../../cobrancas/codigo';
import { Botao } from '../../design/componentes/Botao';
import { CampoTexto } from '../../design/componentes/Campos';
import { AvisoErro, Carregando, Cartao as Quadro } from '../../design/componentes/Feedback';
import { SeloSituacao } from '../../design/componentes/SeloSituacao';
import { formatarDiaCurto, nomeMes } from '../../formatacao/dia';
import { formatarCentavos, saldoEmCentavos } from '../../formatacao/valor';
import { useTitulo } from '../comum';
import { SomentePerfil } from '../SomentePerfil';
import { TextoAvisoCopia } from '../TextoAvisoCopia';
import { useConsulta } from '../useConsulta';
import { useCopia } from '../useCopia';
import estilos from './Cartao.module.css';
import { SolicitarCartao } from './SolicitarCartao';

/** Cartão da PF, ou a solicitação se ainda não houver (specs/015-frontend-cartao-credito, US1; FR-005 a FR-009). */
export function Cartao() {
  useTitulo('Cartão');

  return (
    <SomentePerfil tipo="PessoaFisica" titulo="Cartão">
      <ConteudoCartao />
    </SomentePerfil>
  );
}

function ConteudoCartao() {
  const consulta = useConsulta('cartao', api.obterMeuCartao);
  const [aviso, definirAviso] = useState<MensagemErro | null>(null);

  if (consulta.estado === 'carregando') {
    return <Carregando texto="Carregando seu cartão…" />;
  }

  if (consulta.estado === 'erro' && consulta.erro.codigo === 'cartao-nao-encontrado') {
    return (
      <SolicitarCartao
        aoEmitir={consulta.definir}
        aoJaExistir={(erro) => {
          definirAviso(erro);
          consulta.recarregar();
        }}
      />
    );
  }

  return (
    <div className={estilos.pagina}>
      <div className={estilos.cabecalho}>
        <h1>Cartão</h1>
        {consulta.estado === 'pronto' ? (
          <Link to="/cartao/faturas" className={estilos.link}>
            Ver faturas
          </Link>
        ) : null}
      </div>
      {consulta.estado === 'erro' ? <AvisoErro erro={consulta.erro} aoTentarDeNovo={consulta.recarregar} /> : null}
      {consulta.estado === 'pronto' ? (
        <>
          {aviso ? <AvisoErro erro={aviso} /> : null}
          <div className={estilos.grade}>
            <MeuCartao cartao={consulta.dados} />
            <Limite cartao={consulta.dados} />
          </div>
          <div className={estilos.grade}>
            <FaturaAtual />
            <AbrirCobranca />
          </div>
        </>
      ) : null}
    </div>
  );
}

const MENSAGENS_COPIA = {
  sucesso: 'Número copiado',
  falha: 'Não foi possível copiar. Selecione o número e copie manualmente.',
};

function MeuCartao({ cartao }: { cartao: CartaoCredito }) {
  const { estado, exibir, ocultar } = useDadosCartao();
  const { aviso, copiar } = useCopia();
  const [anuncio, definirAnuncio] = useState('');
  const dados = estado.estado === 'exibido' ? estado.dados : null;

  // Anuncia as mudanças, inclusive as automáticas (tempo, aba escondida), e não o estado inicial.
  const [anterior, definirAnterior] = useState(estado.estado);
  if (anterior !== estado.estado) {
    definirAnterior(estado.estado);
    if (estado.estado === 'exibido') definirAnuncio('Dados do cartão exibidos');
    else if (anterior === 'exibido') definirAnuncio('Dados do cartão ocultos');
  }

  return (
    <section className={estilos.coluna} aria-label="Meu cartão">
      <CartaoDesenhado
        cartao={cartao}
        dados={dados}
        buscando={estado.estado === 'buscando'}
        aoAlternar={() => (dados || estado.estado === 'buscando' ? ocultar() : void exibir())}
      />
      <p className="apenas-leitor-de-tela" role="status" aria-live="polite">
        {anuncio}
      </p>
      {estado.estado === 'erro' ? <AvisoErro erro={estado.erro} aoTentarDeNovo={() => void exibir()} /> : null}
      {dados ? (
        <div className={estilos.acoes}>
          <Botao variante="secundario" onClick={() => void copiar(dados.numero, MENSAGENS_COPIA)}>
            Copiar número
          </Botao>
          <TextoAvisoCopia aviso={aviso} />
        </div>
      ) : null}
      <p className={estilos.nota}>Os dados somem sozinhos em 1 minuto, ao sair da tela ou ao trocar de aba.</p>
    </section>
  );
}

function Limite({ cartao }: { cartao: CartaoCredito }) {
  const total = saldoEmCentavos(cartao.limiteTotal);
  const disponivel = saldoEmCentavos(cartao.limiteDisponivel);
  const usado = total - disponivel;

  return (
    <Quadro rotulo="Limite" className={estilos.quadro}>
      <div>
        <p className={estilos.rotulo}>Limite disponível</p>
        <p className={estilos.valorGrande}>{formatarCentavos(disponivel)}</p>
      </div>
      <div className={estilos.barra} aria-hidden="true">
        <div className={estilos.barraUsada} style={{ width: `${total > 0 ? (usado / total) * 100 : 0}%` }} />
      </div>
      <dl className={estilos.linhas}>
        <Linha rotulo="Usado" valor={formatarCentavos(usado)} />
        <Linha rotulo="Limite total" valor={formatarCentavos(total)} />
        <Linha rotulo="Compras que ainda vão entrar na fatura" valor={formatarCentavos(saldoEmCentavos(cartao.comprasALancar))} />
      </dl>
    </Quadro>
  );
}

function Linha({ rotulo, valor }: { rotulo: string; valor: string }) {
  return (
    <div className={estilos.linha}>
      <dt>{rotulo}</dt>
      <dd>{valor}</dd>
    </div>
  );
}

/** Fatura do ciclo atual: a primeira da lista (research R11). Um erro fica só nesta região (FR-006). */
function FaturaAtual() {
  const consulta = useConsulta('fatura-atual', () => api.listarFaturas({ tamanhoPagina: 1 }));
  const fatura = consulta.estado === 'pronto' ? consulta.dados.itens[0] : undefined;

  return (
    <Quadro rotulo="Fatura atual" className={estilos.quadro}>
      {consulta.estado === 'carregando' ? <Carregando texto="Carregando a fatura…" /> : null}
      {consulta.estado === 'erro' ? <AvisoErro erro={consulta.erro} aoTentarDeNovo={consulta.recarregar} /> : null}
      {fatura ? (
        <>
          <div className={estilos.cabecalho}>
            <h2>Fatura de {nomeMes(fatura.competencia)}</h2>
            <SeloSituacao situacao={fatura.situacao} />
          </div>
          <p className={estilos.valorMedio}>{formatarCentavos(saldoEmCentavos(fatura.valorAPagar ?? fatura.total))}</p>
          <p className={estilos.rotulo}>
            Fecha em {formatarDiaCurto(fatura.fechamento)} · Vence em {formatarDiaCurto(fatura.vencimento)}
          </p>
          <Link to={`/cartao/faturas/${fatura.competencia}`} className={estilos.link}>
            Abrir fatura
          </Link>
        </>
      ) : null}
    </Quadro>
  );
}

/** Abre a cobrança pelo link ou pelo código (FR-009). */
function AbrirCobranca() {
  const navegar = useNavigate();
  const [texto, definirTexto] = useState('');
  const [erro, definirErro] = useState(false);

  function abrir(evento: FormEvent) {
    evento.preventDefault();
    const codigo = extrairCodigo(texto);
    if (codigo) {
      void navegar(caminhoDaCobranca(codigo));
    } else {
      definirErro(true);
    }
  }

  return (
    <Quadro rotulo="Pagar uma cobrança" className={estilos.quadro}>
      <h2>Pagar uma cobrança</h2>
      <form className={estilos.formularioLinha} onSubmit={abrir} noValidate>
        <CampoTexto
          rotulo="Link ou código da cobrança"
          value={texto}
          onChange={(e) => {
            definirTexto(e.target.value);
            definirErro(false);
          }}
          erros={erro ? ['Confira o link ou o código'] : undefined}
          placeholder="Ex.: 7K3F-9QXM"
          autoComplete="off"
          autoCapitalize="characters"
          spellCheck={false}
        />
        <Botao type="submit" variante="secundario">
          Abrir
        </Botao>
      </form>
    </Quadro>
  );
}
