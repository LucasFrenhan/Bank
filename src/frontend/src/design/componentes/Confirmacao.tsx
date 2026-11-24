import { useEffect, useId, useRef, type ReactNode } from 'react';
import estilos from './Confirmacao.module.css';

/** Confirmação na própria tela: foco no título ao abrir; `Esc` fecha (specs/015, FR-020, FR-022; research R12). */
export function Confirmacao({ titulo, aoFechar, children }: { titulo: string; aoFechar: () => void; children: ReactNode }) {
  const id = useId();
  const tituloRef = useRef<HTMLHeadingElement>(null);
  const fecharRef = useRef(aoFechar);

  useEffect(() => {
    fecharRef.current = aoFechar;
  });

  useEffect(() => {
    tituloRef.current?.focus();
    const aoTeclar = (evento: KeyboardEvent) => {
      if (evento.key === 'Escape') fecharRef.current();
    };
    document.addEventListener('keydown', aoTeclar);
    return () => document.removeEventListener('keydown', aoTeclar);
  }, []);

  return (
    <section className={estilos.confirmacao} aria-labelledby={id}>
      <h2 id={id} ref={tituloRef} tabIndex={-1} className={estilos.titulo}>
        {titulo}
      </h2>
      {children}
    </section>
  );
}

/** Linha "rótulo — valor" das confirmações. */
export function LinhaConfirmacao({ rotulo, valor, destaque }: { rotulo: string; valor: string; destaque?: boolean }) {
  return (
    <div className={[estilos.linha, destaque ? estilos.destaque : ''].join(' ')}>
      <dt>{rotulo}</dt>
      <dd>{valor}</dd>
    </div>
  );
}

export function LinhasConfirmacao({ children }: { children: ReactNode }) {
  return <dl className={estilos.linhas}>{children}</dl>;
}

export function AcoesConfirmacao({ children }: { children: ReactNode }) {
  return <div className={estilos.acoes}>{children}</div>;
}
