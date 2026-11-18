import { useState } from 'react';
import { Link, useParams } from 'react-router';
import type { Comprovante as DadosComprovante } from '../api/cliente';
import { MENSAGENS } from '../api/erros';
import { Botao } from '../design/componentes/Botao';
import { Aviso, Cartao } from '../design/componentes/Feedback';
import { IconeConfirmado } from '../design/componentes/Icones';
import { LayoutEtapas } from '../design/componentes/LayoutEtapas';
import { formatarDataHora } from '../formatacao/data';
import { formatarCentavos, saldoEmCentavos } from '../formatacao/valor';
import { useSessaoAtiva } from '../sessao/ContextoSessao';
import { lerComprovante } from '../transferencia/armazenamento';
import { useTitulo } from './comum';
import estilos from './Transferencia.module.css';
import { descreverDocumento } from './Transferir';
import { useTitular } from './useTitular';

/**
 * Comprovante de uma transferência (specs/007: US1; FR-011, FR-013, FR-014; research R5, R10).
 * Até a issue #36, os dados vêm dos comprovantes guardados nesta aba.
 */
export function Comprovante() {
  useTitulo('Comprovante');
  const { transferenciaId = '' } = useParams();
  const comprovante = lerComprovante(transferenciaId);

  return (
    <LayoutEtapas>
      {comprovante ? (
        <ComprovanteEncontrado comprovante={comprovante} />
      ) : (
        <div className={estilos.pagina}>
          <h1>Comprovante</h1>
          <Aviso variante="neutro" titulo={MENSAGENS.comprovanteNaoEncontrado}>
            <p>{MENSAGENS.comprovanteNaoEncontradoDetalhe}</p>
            <p>
              <Link to="/extrato">Ir para o extrato</Link>
            </p>
          </Aviso>
        </div>
      )}
    </LayoutEtapas>
  );
}

function ComprovanteEncontrado({ comprovante }: { comprovante: DadosComprovante }) {
  const sessao = useSessaoAtiva();
  const [statusCopia, definirStatusCopia] = useState('');
  const souPagador = comprovante.pagador.usuarioId === sessao.usuarioId;
  const valor = formatarCentavos(saldoEmCentavos(comprovante.valor));
  const recebedor = `${comprovante.recebedor.nome} · ${descreverDocumento(comprovante.recebedor.documento)}`;
  const descricao = comprovante.descricao?.trim();
  const data = formatarDataHora(comprovante.criadoEm);

  async function compartilhar() {
    const linhas = [
      `Comprovante de transferência — Bank`,
      `Valor: ${valor}`,
      `De: ${comprovante.pagador.nome}`,
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
              {souPagador ? <PagadorComDocumento nome={comprovante.pagador.nome} /> : comprovante.pagador.nome}
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

/** "Nome · CPF •••.982.247-••" — o documento do pagador só existe para o próprio pagador (FR-011). */
function PagadorComDocumento({ nome }: { nome: string }) {
  const titular = useTitular();
  return <>{titular.estado === 'pronto' ? `${nome} · ${descreverDocumento(titular.titular.documento)}` : nome}</>;
}
