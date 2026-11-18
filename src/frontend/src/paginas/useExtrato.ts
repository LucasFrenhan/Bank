import { useCallback, useEffect, useState } from 'react';
import { api, type ConsultaExtrato, type ExtratoPagina } from '../api/cliente';
import type { MensagemErro } from '../api/erros';

export type EstadoExtrato =
  | { estado: 'carregando' }
  | { estado: 'pronto'; extrato: ExtratoPagina }
  | { estado: 'erro'; erro: MensagemErro };

/**
 * Página do extrato lida da API a cada exibição (specs/007, research R8; FR-025), com recarga sob
 * demanda. 401 já encerra a sessão no cliente da API. Ao mudar a consulta, volta a "carregando"
 * até a nova resposta chegar (o estado guarda a consulta a que pertence).
 */
export function useExtrato(consulta: ConsultaExtrato): EstadoExtrato & { recarregar: () => void } {
  const [versao, definirVersao] = useState(0);
  const { pagina, tamanhoPagina, direcao } = consulta;
  const chave = `${pagina ?? ''}|${tamanhoPagina ?? ''}|${direcao ?? ''}|${versao}`;
  const [resposta, definirResposta] = useState<{ chave: string; estado: EstadoExtrato } | null>(null);

  useEffect(() => {
    let ativo = true;

    void api
      .obterExtrato({
        ...(pagina !== undefined ? { pagina } : {}),
        ...(tamanhoPagina !== undefined ? { tamanhoPagina } : {}),
        ...(direcao ? { direcao } : {}),
      })
      .then((resultado) => {
        if (!ativo) return;
        if (resultado.ok) {
          definirResposta({ chave, estado: { estado: 'pronto', extrato: resultado.dados } });
        } else if (!resultado.erro.encerrarSessao) {
          definirResposta({ chave, estado: { estado: 'erro', erro: resultado.erro } });
        }
      });

    return () => {
      ativo = false;
    };
  }, [chave, pagina, tamanhoPagina, direcao]);

  const recarregar = useCallback(() => definirVersao((v) => v + 1), []);
  const estado: EstadoExtrato = resposta?.chave === chave ? resposta.estado : { estado: 'carregando' };

  return { ...estado, recarregar };
}
