import { useEffect, useState } from 'react';
import { api, type Limites } from '../api/cliente';
import { saldoEmCentavos } from '../formatacao/valor';

/**
 * Limites da PF para a tela Transferir (specs/009-limite-transferencia: US4, FR-013;
 * contracts/frontend.md "Transferir — Dados"). Informativo: a API verifica de novo no envio.
 */

/** Limites em centavos; `null` = sem limite. */
export interface ResumoLimites {
  porOperacaoCentavos: number | null;
  diarioCentavos: number | null;
  disponivelHojeCentavos: number | null;
}

export type TipoLimite = 'PorOperacao' | 'Diario';

export interface MaximoAgora {
  centavos: number;
  /** Limite que define o máximo (no empate, por operação, na mesma ordem da API). */
  tipo: TipoLimite;
}

const centavosOuNulo = (valor: number | string | null): number | null => (valor === null ? null : saldoEmCentavos(valor));

export function resumirLimites(limites: Limites): ResumoLimites {
  return {
    porOperacaoCentavos: centavosOuNulo(limites.porOperacao),
    diarioCentavos: centavosOuNulo(limites.diario),
    disponivelHojeCentavos: centavosOuNulo(limites.disponivelHoje),
  };
}

/** Menor entre o limite por operação e o disponível hoje, ignorando os nulos. */
export function maximoAgora(resumo: ResumoLimites | null): MaximoAgora | null {
  if (!resumo) {
    return null;
  }

  const { porOperacaoCentavos: porOperacao, disponivelHojeCentavos: disponivel } = resumo;
  if (porOperacao !== null && (disponivel === null || porOperacao <= disponivel)) {
    return { centavos: porOperacao, tipo: 'PorOperacao' };
  }

  return disponivel !== null ? { centavos: disponivel, tipo: 'Diario' } : null;
}

/**
 * Lê `GET /limites` ao abrir a tela. Falha de rede, 403 ou 5xx → `null` (a dica fica sem o
 * limite e a API continua barrando no envio); 401 o cliente já trata encerrando a sessão.
 */
export function useLimites(): ResumoLimites | null {
  const [resumo, definirResumo] = useState<ResumoLimites | null>(null);

  useEffect(() => {
    let ativo = true;

    void api.obterLimites().then((resultado) => {
      if (ativo && resultado.ok) {
        definirResumo(resumirLimites(resultado.dados));
      }
    });

    return () => {
      ativo = false;
    };
  }, []);

  return resumo;
}
