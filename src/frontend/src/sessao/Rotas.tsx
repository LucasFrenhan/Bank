import type { ReactNode } from 'react';
import { Navigate, useLocation } from 'react-router';
import { ROTA_ENTRAR, useSessao } from './ContextoSessao';

/**
 * Sem sessão → login, guardando a rota pedida (FR-010) e, se a sessão foi encerrada sem a
 * pessoa sair, o motivo (`?expirada=1` ou `?conta=1` — FR-011).
 */
export function RotaProtegida({ children }: { children: ReactNode }) {
  const { sessao, motivoEncerramento } = useSessao();
  const local = useLocation();

  if (!sessao) {
    const destino = motivoEncerramento ? `${ROTA_ENTRAR}?${motivoEncerramento}=1` : ROTA_ENTRAR;
    return <Navigate to={destino} replace state={{ retorno: local.pathname + local.search }} />;
  }

  return children;
}

/**
 * Com sessão → início (US2, cenário 6) ou, logo após o login, a rota pedida antes dele
 * (`state.retorno`, guardado por `RotaProtegida`).
 */
export function RotaPublica({ children }: { children: ReactNode }) {
  const { sessao } = useSessao();
  const local = useLocation();
  const retorno = (local.state as { retorno?: string } | null)?.retorno;

  return sessao ? <Navigate to={retorno ?? '/'} replace /> : children;
}
