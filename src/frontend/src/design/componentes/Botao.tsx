import type { ButtonHTMLAttributes, ReactNode } from 'react';
import { Link } from 'react-router';
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

/** Navegação com aparência de botão (ex.: "Transferir" e "Ver extrato" no cartão de saldo). */
export function BotaoLink({ para, variante = 'primario', largo, icone, children }: { para: string; variante?: Variante; largo?: boolean; icone?: ReactNode; children: ReactNode }) {
  return (
    <Link to={para} className={[estilos.botao, estilos[variante], largo ? estilos.botaoLargo : ''].join(' ')}>
      {icone}
      {children}
    </Link>
  );
}
