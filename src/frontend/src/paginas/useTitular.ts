import { useCallback, useEffect, useState } from 'react';
import { api, type Titular } from '../api/cliente';
import type { MensagemErro } from '../api/erros';
import { useSessao } from '../sessao/ContextoSessao';

export type EstadoTitular =
  | { estado: 'carregando' }
  | { estado: 'pronto'; titular: Titular }
  | { estado: 'erro'; erro: MensagemErro };

/**
 * Dados do titular lidos da API ao exibir a tela (`GET /api/v1/usuarios/me` — spec 005;
 * FR-017, FR-017a). Conta inexistente encerra a sessão.
 */
export function useTitular(): EstadoTitular & { recarregar: () => void } {
  const { expirar } = useSessao();
  const [estado, definirEstado] = useState<EstadoTitular>({ estado: 'carregando' });
  const [versao, definirVersao] = useState(0);

  useEffect(() => {
    let ativo = true;

    void api.obterTitular().then((resultado) => {
      if (!ativo) return;
      if (resultado.ok) {
        definirEstado({ estado: 'pronto', titular: resultado.dados });
      } else if (resultado.erro.codigo === 'usuario-nao-encontrado') {
        expirar('conta');
      } else if (!resultado.erro.encerrarSessao) {
        definirEstado({ estado: 'erro', erro: resultado.erro });
      }
      // 401: o cliente já encerrou a sessão e levou ao login.
    });

    return () => {
      ativo = false;
    };
  }, [versao, expirar]);

  const recarregar = useCallback(() => {
    definirEstado({ estado: 'carregando' });
    definirVersao((v) => v + 1);
  }, []);

  return { ...estado, recarregar };
}
