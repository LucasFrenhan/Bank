import type { ReactNode } from 'react';
import { Navigate, useLocation } from 'react-router';
import { AvisoExpiracao } from './AvisoExpiracao';
import { ROTA_ENTRAR, useSessao } from './ContextoSessao';
import componentes from '../design/componentes/componentes.module.css';
import estilos from './Rotas.module.css';

/**
 * Enquanto a sessão é recuperada ao abrir ou recarregar a página (specs/016-sessao-refresh-token, FR-017), nenhuma
 * tela protegida nem o login aparecem: só este carregamento, anunciado a leitores de tela.
 */
export function CarregandoSessao() {
  return (
    <div className={estilos.carregando} role="status" aria-live="polite">
      <span className={componentes.giro} aria-hidden="true" />
      <p>Carregando sua sessão…</p>
    </div>
  );
}

/**
 * Sem sessão → login, guardando a rota pedida (FR-010) e, se a sessão foi encerrada sem a
 * pessoa sair, o motivo (`?expirada=1` ou `?conta=1` — FR-011).
 */
export function RotaProtegida({ children }: { children: ReactNode }) {
  const { sessao, verificando, motivoEncerramento } = useSessao();
  const local = useLocation();

  if (verificando) {
    return <CarregandoSessao />;
  }

  if (!sessao) {
    const destino = motivoEncerramento ? `${ROTA_ENTRAR}?${motivoEncerramento}=1` : ROTA_ENTRAR;
    return <Navigate to={destino} replace state={{ retorno: local.pathname + local.search }} />;
  }

  return (
    <>
      {children}
      <AvisoExpiracao />
    </>
  );
}

/**
 * Com sessão → início (US2, cenário 6) ou, logo após o login, a rota pedida antes dele
 * (`state.retorno`, guardado por `RotaProtegida`).
 */
export function RotaPublica({ children }: { children: ReactNode }) {
  const { sessao, verificando } = useSessao();
  const local = useLocation();
  const retorno = (local.state as { retorno?: string } | null)?.retorno;

  if (verificando) {
    return <CarregandoSessao />;
  }

  return sessao ? <Navigate to={retorno ?? '/'} replace /> : children;
}
