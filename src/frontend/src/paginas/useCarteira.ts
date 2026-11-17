import { useCallback, useEffect, useState } from 'react';
import { api, type MinhaCarteira } from '../api/cliente';
import type { MensagemErro } from '../api/erros';
import { useSessao } from '../sessao/ContextoSessao';

export type EstadoCarteira =
  | { estado: 'carregando' }
  | { estado: 'pronta'; carteira: MinhaCarteira }
  | { estado: 'erro'; erro: MensagemErro };

/** Carteira sempre lida da API ao exibir a tela (FR-014), com recarga sob demanda. */
export function useCarteira(): EstadoCarteira & { recarregar: () => void } {
  const { expirar } = useSessao();
  const [estado, definirEstado] = useState<EstadoCarteira>({ estado: 'carregando' });
  const [versao, definirVersao] = useState(0);

  useEffect(() => {
    let ativo = true;

    void api.obterMinhaCarteira().then((resultado) => {
      if (!ativo) return;
      if (resultado.ok) {
        definirEstado({ estado: 'pronta', carteira: resultado.dados });
      } else if (resultado.erro.codigo === 'carteira-nao-encontrada') {
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
