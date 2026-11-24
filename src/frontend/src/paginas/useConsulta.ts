import { useCallback, useEffect, useRef, useState } from 'react';
import type { Resultado } from '../api/cliente';
import type { MensagemErro } from '../api/erros';

export type EstadoConsulta<T> =
  | { estado: 'carregando' }
  | { estado: 'pronto'; dados: T }
  | { estado: 'erro'; erro: MensagemErro };

export type Consulta<T> = EstadoConsulta<T> & {
  /** Volta a "carregando" e busca de novo. */
  recarregar: () => void;
  /** Busca sem mudar a tela; uma falha mantém o que já era mostrado. */
  atualizar: () => Promise<T | null>;
  /** Troca os dados pelo que uma operação devolveu. */
  definir: (dados: T) => void;
};

/**
 * Leitura da API ao exibir a tela (specs/015-frontend-cartao-credito, FR-023; research R7). `chave` identifica o que é
 * lido: ao mudar, volta a "carregando". 401 já encerra a sessão no cliente.
 */
export function useConsulta<T>(chave: string, buscar: () => Promise<Resultado<T>>): Consulta<T> {
  const [versao, definirVersao] = useState(0);
  const completa = `${chave}|${versao}`;
  const [resposta, definirResposta] = useState<{ chave: string; estado: EstadoConsulta<T> } | null>(null);
  const buscarRef = useRef(buscar);
  // Uma resposta silenciosa só vale se nada mudou desde que foi pedida.
  const sequencia = useRef(0);
  const completaRef = useRef(completa);

  useEffect(() => {
    buscarRef.current = buscar;
    completaRef.current = completa;
  });

  useEffect(() => {
    const minha = ++sequencia.current;
    let ativo = true;
    void buscarRef.current().then((resultado) => {
      if (!ativo || minha !== sequencia.current) return;
      if (resultado.ok) {
        definirResposta({ chave: completa, estado: { estado: 'pronto', dados: resultado.dados } });
      } else if (!resultado.erro.encerrarSessao) {
        definirResposta({ chave: completa, estado: { estado: 'erro', erro: resultado.erro } });
      }
    });

    return () => {
      ativo = false;
    };
  }, [completa]);

  const recarregar = useCallback(() => definirVersao((v) => v + 1), []);

  const definir = useCallback((dados: T) => {
    sequencia.current++;
    definirResposta({ chave: completaRef.current, estado: { estado: 'pronto', dados } });
  }, []);

  const atualizar = useCallback(async (): Promise<T | null> => {
    const minha = sequencia.current;
    const resultado = await buscarRef.current();
    if (!resultado.ok || minha !== sequencia.current) {
      return null;
    }

    definir(resultado.dados);
    return resultado.dados;
  }, [definir]);

  const estado: EstadoConsulta<T> = resposta?.chave === completa ? resposta.estado : { estado: 'carregando' };
  return { ...estado, recarregar, atualizar, definir };
}
