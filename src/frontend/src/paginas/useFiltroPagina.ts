import { useCallback } from 'react';
import { useSearchParams } from 'react-router';

/** Filtro e página no endereço (specs/015-frontend-cartao-credito, FR-011 e FR-021); valores desconhecidos são ignorados. */
export function useFiltroPagina<F extends string>(valores: readonly F[]) {
  const [parametros, definirParametros] = useSearchParams();
  const situacao = parametros.get('situacao');
  const filtro = valores.find((v) => v === situacao) ?? null;
  const numero = Number(parametros.get('pagina'));
  const pagina = Number.isInteger(numero) && numero >= 1 ? numero : 1;

  const irPara = useCallback(
    (novoFiltro: F | null, novaPagina: number, substituir = false) => {
      const proximos = new URLSearchParams();
      if (novoFiltro) proximos.set('situacao', novoFiltro);
      if (novaPagina > 1) proximos.set('pagina', String(novaPagina));
      definirParametros(proximos, { replace: substituir });
    },
    [definirParametros],
  );

  return { filtro, pagina, irPara };
}
