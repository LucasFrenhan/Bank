import type { ReactNode } from 'react';
import { useParams } from 'react-router';
import { api, type Cobranca as DadosCobranca } from '../../api/cliente';
import { traduzirErro } from '../../api/erros';
import { extrairCodigo } from '../../cobrancas/codigo';
import { AvisoErro, Carregando } from '../../design/componentes/Feedback';
import { Layout } from '../../design/componentes/Layout';
import { LayoutEtapas } from '../../design/componentes/LayoutEtapas';
import { useSessaoAtiva } from '../../sessao/ContextoSessao';
import { useTitulo } from '../comum';
import { useConsulta, type Consulta } from '../useConsulta';
import estilos from './Cobrancas.module.css';
import { CobrancaSemAcao } from './CobrancaSemAcao';
import { ComprovanteCartao } from './ComprovanteCartao';
import { DetalheCobranca } from './DetalheCobranca';
import { PagarCobranca } from './PagarCobranca';

/** Consulta pronta, com as ações de atualização. */
export type CobrancaPronta = Extract<Consulta<DadosCobranca>, { estado: 'pronto' }>;

/**
 * Endereço único da cobrança (specs/015-frontend-cartao-credito, FR-003; research R4): a emissora vê o detalhe; a PF, o
 * pagamento ou o comprovante; outra PJ, os dados sem ação.
 */
export function Cobranca() {
  const { codigo: texto = '' } = useParams();
  const codigo = extrairCodigo(texto);

  return codigo ? <CobrancaPorCodigo key={codigo} codigo={codigo} /> : <NaoEncontrada />;
}

function NaoEncontrada() {
  useTitulo('Cobrança');
  return (
    <Moldura>
      <div className={estilos.pagina}>
        <h1>Cobrança</h1>
        <AvisoErro erro={traduzirErro(404, { type: 'urn:bank:problema:cobranca-nao-encontrada' })} />
      </div>
    </Moldura>
  );
}

/** PJ com o menu; PF sem menu, como no pagamento do protótipo. */
function Moldura({ children }: { children: ReactNode }) {
  const pj = useSessaoAtiva().tipoConta === 'PessoaJuridica';
  return pj ? <Layout>{children}</Layout> : <LayoutEtapas voltar={{ rotulo: 'Voltar ao cartão', para: '/cartao' }}>{children}</LayoutEtapas>;
}

function CobrancaPorCodigo({ codigo }: { codigo: string }) {
  const sessao = useSessaoAtiva();
  const consulta = useConsulta(codigo, () => api.consultarCobranca(codigo));

  if (consulta.estado === 'carregando') {
    return (
      <Moldura>
        <Carregando texto="Carregando a cobrança…" />
      </Moldura>
    );
  }

  if (consulta.estado === 'erro') {
    return consulta.erro.codigo === 'cobranca-nao-encontrada' ? (
      <NaoEncontrada />
    ) : (
      <Moldura>
        <div className={estilos.pagina}>
          <h1>Cobrança</h1>
          <AvisoErro erro={consulta.erro} aoTentarDeNovo={consulta.recarregar} />
        </div>
      </Moldura>
    );
  }

  return <Moldura>{visao(consulta, sessao.usuarioId, sessao.tipoConta === 'PessoaJuridica')}</Moldura>;
}

function visao(consulta: CobrancaPronta, usuarioId: string, pj: boolean): ReactNode {
  const cobranca = consulta.dados;
  if (cobranca.lojista.usuarioId === usuarioId) return <DetalheCobranca consulta={consulta} />;
  if (pj) return <CobrancaSemAcao cobranca={cobranca} motivo="pj" />;
  if (cobranca.status === 'Pendente') return <PagarCobranca consulta={consulta} />;
  return cobranca.pagamento ? <ComprovanteCartao cobranca={cobranca} /> : <CobrancaSemAcao cobranca={cobranca} motivo="indisponivel" />;
}
