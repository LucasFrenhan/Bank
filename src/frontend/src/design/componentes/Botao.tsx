import type { ButtonHTMLAttributes, ReactNode } from 'react';
import estilos from './componentes.module.css';

type Variante = 'primario' | 'secundario' | 'claro' | 'contornoClaro';

export interface BotaoProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variante?: Variante;
  largo?: boolean;
  /** Mostra progresso e bloqueia novos cliques (evita envio duplicado). */
  carregando?: boolean;
}

export function Botao({ variante = 'primario', largo, carregando, disabled, className, children, type = 'button', ...props }: BotaoProps) {
  const classes = [estilos.botao, estilos[variante], largo ? estilos.botaoLargo : '', className ?? ''].join(' ');

  return (
    <button {...props} type={type} className={classes} disabled={disabled ?? carregando} aria-busy={carregando || undefined}>
      {carregando ? <span className={estilos.giro} aria-hidden="true" /> : null}
      {children}
    </button>
  );
}

/** Ação ainda indisponível: visível, anunciada como desabilitada, sem efeito (FR-003). */
export function AcaoEmBreve({ rotulo, variante = 'secundario', icone }: { rotulo: string; variante?: Variante; icone?: ReactNode }) {
  return (
    <span className={[estilos.botao, estilos[variante]].join(' ')} role="button" aria-disabled="true" tabIndex={-1}>
      {icone}
      {rotulo}
      <span className={estilos.selo}>Em breve</span>
    </span>
  );
}
