import { useCallback, useEffect, useRef, useState } from 'react';

/** Abre e fecha uma confirmação na própria tela, devolvendo o foco ao botão que a abriu (specs/015, research R12). */
export function useConfirmacao() {
  const [aberta, definirAberta] = useState(false);
  const gatilho = useRef<HTMLButtonElement>(null);
  const devolver = useRef(false);

  useEffect(() => {
    if (!aberta && devolver.current) {
      devolver.current = false;
      gatilho.current?.focus();
    }
  }, [aberta]);

  const abrir = useCallback(() => definirAberta(true), []);
  const fechar = useCallback(() => {
    devolver.current = true;
    definirAberta(false);
  }, []);

  return { aberta, abrir, fechar, gatilho };
}
