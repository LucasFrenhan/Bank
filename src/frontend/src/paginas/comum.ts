import { useEffect } from 'react';

/** Título da página por rota (WCAG 2.4.2). */
export function useTitulo(titulo: string): void {
  useEffect(() => {
    document.title = `${titulo} · Bank`;
  }, [titulo]);
}

/** Primeiro nome para a saudação ("Olá, Lucas"). */
export function primeiroNome(nome: string): string {
  return nome.trim().split(/\s+/)[0] ?? nome;
}
