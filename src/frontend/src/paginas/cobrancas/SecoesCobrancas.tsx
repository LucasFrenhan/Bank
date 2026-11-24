import { NavLink } from 'react-router';
import estilos from './Cobrancas.module.css';

/** Abas da área de cobranças da PJ (specs/015, contracts/frontend.md). */
export function SecoesCobrancas() {
  return (
    <nav className={estilos.secoes} aria-label="Seções de cobranças">
      <NavLink to="/cobrancas" end className={estilos.secao}>
        Cobranças
      </NavLink>
      <NavLink to="/cobrancas/recebiveis" end className={estilos.secao}>
        Recebíveis
      </NavLink>
    </nav>
  );
}
