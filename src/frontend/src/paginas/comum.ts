import { useEffect } from 'react';
import { NOME_BANCO } from '../marca';

/** Título da página por rota (WCAG 2.4.2). */
export function useTitulo(titulo: string): void {
  useEffect(() => {
    document.title = `${titulo} · ${NOME_BANCO}`;
  }, [titulo]);
}

/** Primeiro nome para a saudação ("Olá, Lucas"). */
export function primeiroNome(nome: string): string {
  return nome.trim().split(/\s+/)[0] ?? nome;
}
