import { useCallback, useEffect, useState } from 'react';
import { Link, useParams } from 'react-router';
import { api, type Comprovante as DadosComprovante } from '../api/cliente';
import { MENSAGENS, type MensagemErro } from '../api/erros';
import { Botao } from '../design/componentes/Botao';
import { Aviso, AvisoErro, Carregando, Cartao } from '../design/componentes/Feedback';
import { IconeConfirmado } from '../design/componentes/Icones';
import { LayoutEtapas } from '../design/componentes/LayoutEtapas';
import { formatarDataHora } from '../formatacao/data';
import { formatarCentavos, saldoEmCentavos } from '../formatacao/valor';
import { useSessaoAtiva } from '../sessao/ContextoSessao';
import { lerComprovante } from '../transferencia/armazenamento';
import { useTitulo } from './comum';
import { NOME_BANCO } from '../marca';
import estilos from './Transferencia.module.css';
import { descreverDocumento } from './Transferir';

type EstadoComprovante =
  | { estado: 'carregando' }
  | { estado: 'pronto'; comprovante: DadosComprovante }
  | { estado: 'nao-encontrado' }
  | { estado: 'erro'; erro: MensagemErro };

/**
 * Comprovante da aba, se houver (logo após o envio); senão, pela consulta por identificador
 * (spec 008) — vale para pagador e recebedor, em qualquer sessão (FR-014).
 */
function useComprovante(transferenciaId: string): EstadoComprovante & { recarregar: () => void } {
  const [versao, definirVersao] = useState(0);
  const daAba = lerComprovante(transferenciaId);
  const chave = `${transferenciaId}|${versao}`;
  const [resposta, definirResposta] = useState<{ chave: string; estado: EstadoComprovante } | null>(null);

  useEffect(() => {
    if (daAba) return undefined;
    let ativo = true;

    void api.obterTransferencia(transferenciaId).then((resultado) => {
      if (!ativo) return;
      if (resultado.ok) {
        definirResposta({ chave, estado: { estado: 'pronto', comprovante: resultado.dados } });
      } else if (resultado.erro.codigo === 'transferencia-nao-encontrada' || resultado.erro.codigo === 'validacao') {
        definirResposta({ chave, estado: { estado: 'nao-encontrado' } });
      } else if (!resultado.erro.encerrarSessao) {
        definirResposta({ chave, estado: { estado: 'erro', erro: resultado.erro } });
      }
    });

    return () => {
      ativo = false;
    };
  }, [chave, daAba, transferenciaId]);

  const recarregar = useCallback(() => definirVersao((v) => v + 1), []);
  if (daAba) return { estado: 'pronto', comprovante: daAba, recarregar };
  return { ...(resposta?.chave === chave ? resposta.estado : { estado: 'carregando' as const }), recarregar };
}

/**
 * Comprovante de uma transferência (specs/007: US1; FR-011, FR-013, FR-014; research R5, R10;
 * consulta da spec 008).
 */
export function Comprovante() {
  useTitulo('Comprovante');
  const { transferenciaId = '' } = useParams();
  const comprovante = useComprovante(transferenciaId);

  return (
    <LayoutEtapas>
      {comprovante.estado === 'carregando' ? <Carregando texto="Carregando comprovante…" /> : null}
      {comprovante.estado === 'pronto' ? <ComprovanteEncontrado comprovante={comprovante.comprovante} /> : null}
      {comprovante.estado === 'erro' ? (
        <div className={estilos.pagina}>
          <h1>Comprovante</h1>
          <AvisoErro erro={comprovante.erro} aoTentarDeNovo={comprovante.recarregar} />
        </div>
      ) : null}
      {comprovante.estado === 'nao-encontrado' ? (
        <div className={estilos.pagina}>
          <h1>Comprovante</h1>
          <Aviso variante="neutro" titulo={MENSAGENS.comprovanteNaoEncontrado}>
            <p>{MENSAGENS.comprovanteNaoEncontradoDetalhe}</p>
            <p>
              <Link to="/extrato">Ir para o extrato</Link>
            </p>
          </Aviso>
        </div>
      ) : null}
    </LayoutEtapas>
  );
}

function ComprovanteEncontrado({ comprovante }: { comprovante: DadosComprovante }) {
  const sessao = useSessaoAtiva();
  const [statusCopia, definirStatusCopia] = useState('');
  const souPagador = comprovante.pagador.usuarioId === sessao.usuarioId;
  const valor = formatarCentavos(saldoEmCentavos(comprovante.valor));
  const recebedor = `${comprovante.recebedor.nome} · ${descreverDocumento(comprovante.recebedor.documento)}`;
  // O documento do pagador já chega mascarado da API (spec 008, FR-002a); o pagador é sempre PF.
  const pagador = `${comprovante.pagador.nome} · CPF ${comprovante.pagador.documento}`;
  const descricao = comprovante.descricao?.trim();
  const data = formatarDataHora(comprovante.criadoEm);

  async function compartilhar() {
    const linhas = [
      `Comprovante de transferência — ${NOME_BANCO}`,
      `Valor: ${valor}`,
      `De: ${pagador}`,
      `Para: ${recebedor}`,
      ...(descricao ? [`Descrição: ${descricao}`] : []),
      `Data: ${data}`,
      `Status: Concluída`,
      `ID: ${comprovante.transferenciaId}`,
    ];
    const texto = linhas.join('\n');

    try {
      if (typeof navigator.share === 'function') {
        await navigator.share({ title: 'Comprovante de transferência', text: texto });
        return;
      }

      await navigator.clipboard.writeText(texto);
      definirStatusCopia('Comprovante copiado');
    } catch (erro) {
      if (erro instanceof DOMException && erro.name === 'AbortError') return; // compartilhamento cancelado
      definirStatusCopia('Não foi possível compartilhar. Tente de novo.');
    }
  }

  return (
    <div className={estilos.pagina}>
      <div className={estilos.sucesso}>
        <span className={estilos.iconeSucesso} aria-hidden="true">
          <IconeConfirmado />
        </span>
        <h1>{souPagador ? 'Transferência concluída' : 'Transferência recebida'}</h1>
        <p className={estilos.resumoSucesso}>
          {souPagador
            ? `${valor} enviados para ${comprovante.recebedor.nome}.`
            : `${valor} recebidos de ${comprovante.pagador.nome}.`}
        </p>
      </div>

      <Cartao>
        <ul className={estilos.linhas} aria-label="Dados da transferência">
          <li className={estilos.linha}>
            <span className={estilos.rotuloLinha}>Valor</span>
            <span className={[estilos.valorLinha, estilos.valorLinhaForte].join(' ')}>{valor}</span>
          </li>
          <li className={estilos.linha}>
            <span className={estilos.rotuloLinha}>De</span>
            <span className={estilos.valorLinha}>
              {pagador}
            </span>
          </li>
          <li className={estilos.linha}>
            <span className={estilos.rotuloLinha}>Para</span>
            <span className={estilos.valorLinha}>{recebedor}</span>
          </li>
          {descricao ? (
            <li className={estilos.linha}>
              <span className={estilos.rotuloLinha}>Descrição</span>
              <span className={estilos.valorLinha}>{descricao}</span>
            </li>
          ) : null}
          <li className={estilos.linha}>
            <span className={estilos.rotuloLinha}>Data</span>
            <span className={estilos.valorLinha}>{data}</span>
          </li>
          <li className={estilos.linha}>
            <span className={estilos.rotuloLinha}>Status</span>
            <span className={estilos.valorLinha}>
              <span className={estilos.seloConcluida}>Concluída</span>
            </span>
          </li>
          <li className={estilos.linha}>
            <span className={estilos.rotuloLinha}>ID</span>
            <span className={[estilos.valorLinha, estilos.identificador].join(' ')}>{comprovante.transferenciaId}</span>
          </li>
        </ul>
      </Cartao>

      <div className={estilos.acoes}>
        <Botao variante="secundario" onClick={() => void compartilhar()}>
          Compartilhar
        </Botao>
        <Link to="/" className={estilos.botaoPrincipal}>
          Voltar ao início
        </Link>
      </div>
      <p className={estilos.statusCopia} role="status" aria-live="polite">
        {statusCopia}
      </p>
    </div>
  );
}

