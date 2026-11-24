import type { ReactNode } from 'react';
import { Link } from 'react-router';
import { MENSAGENS } from '../api/erros';
import { Aviso } from '../design/componentes/Feedback';
import type { TipoConta } from '../sessao/claims';
import { useSessaoAtiva } from '../sessao/ContextoSessao';
import estilos from './Autenticado.module.css';

/** Área de um só tipo de conta (specs/015-frontend-cartao-credito, FR-002); a API também recusa. */
export function SomentePerfil({ tipo, titulo, children }: { tipo: TipoConta; titulo: string; children: ReactNode }) {
  const sessao = useSessaoAtiva();
  if (sessao.tipoConta === tipo) {
    return children;
  }

  return (
    <div className={estilos.pagina}>
      <h1>{titulo}</h1>
      <Aviso variante="neutro" titulo={MENSAGENS.areaIndisponivel}>
        <p>{tipo === 'PessoaFisica' ? MENSAGENS.areaSoPf : MENSAGENS.areaSoPj}</p>
        <p>
          <Link to="/">Ir para o início</Link>
        </p>
      </Aviso>
    </div>
  );
}
