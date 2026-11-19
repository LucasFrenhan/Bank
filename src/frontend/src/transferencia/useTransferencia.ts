import { useCallback, useEffect, useRef, useState } from 'react';
import { api, CODIGO_CANCELADA } from '../api/cliente';
import { traduzirErro, type MensagemErro } from '../api/erros';
import { cpfValido, documentoCompleto, mascararDocumentoDigitado, normalizarDocumento, tipoDocumento } from '../formatacao/mascaras';
import { centavosParaNumero, interpretarValor } from '../formatacao/valor';
import {
  guardarComprovante,
  lerTransferencia,
  limparTransferencia,
  salvarTransferencia,
  TRANSFERENCIA_VAZIA,
  type TransferenciaEmAndamento,
} from './armazenamento';
import type { ResumoLimites } from '../paginas/useLimites';
import { assinatura, prepararConfirmacao } from './chave';

/**
 * Estado e ações da transferência (specs/007-frontend-transferencias: US1–US3; data-model
 * "Transferência em andamento"; research R2, R4, R6). As telas Dados e Confirmação usam este
 * hook; os dados ficam na aba (`sessionStorage`) para sobreviver a recarregar a página.
 */

export const MENSAGEM_DOCUMENTO_INCOMPLETO = 'Informe o CPF ou CNPJ completo';
const ESPERA_CPF_MS = 500;

export type CampoTransferencia = 'documento' | 'valor' | 'descricao';
export type ErrosCampo = Partial<Record<CampoTransferencia, string[]>>;

export type ResultadoEnvio =
  | { tipo: 'concluida'; transferenciaId: string }
  | { tipo: 'repetida'; transferenciaId: string }
  | { tipo: 'recusada'; erro: MensagemErro };

/** O titular foi confirmado para o documento que está no campo agora. */
export function destinatarioConfirmado(dados: TransferenciaEmAndamento): boolean {
  return !!dados.destinatario && dados.destinatario.documento === normalizarDocumento(dados.documento);
}

/** Assinatura dos dados atuais, ou null se ainda não são enviáveis. */
function assinaturaDe(dados: TransferenciaEmAndamento): string | null {
  const valor = interpretarValor(dados.valorTexto);
  if (!valor.ok || !destinatarioConfirmado(dados)) {
    return null;
  }

  return assinatura(normalizarDocumento(dados.documento), valor.centavos, dados.descricao.trim());
}

/** Os dados permitem abrir a confirmação (titular confirmado e valor válido). */
export function prontaParaConfirmar(dados: TransferenciaEmAndamento): boolean {
  return assinaturaDe(dados) !== null;
}

const CAMPOS_API: Record<string, CampoTransferencia> = {
  documento: 'documento',
  documentoDestino: 'documento',
  valor: 'valor',
  descricao: 'descricao',
};

/** Erros por campo da API mapeados para os campos da tela; o restante vai para a mensagem geral. */
function separarCampos(erro: MensagemErro): { campos: ErrosCampo; restantes: string[] } {
  const campos: ErrosCampo = {};
  const restantes: string[] = [];

  for (const [nome, mensagens] of Object.entries(erro.campos ?? {})) {
    const campo = CAMPOS_API[nome] ?? CAMPOS_API[nome.charAt(0).toLowerCase() + nome.slice(1)];
    if (campo) {
      campos[campo] = [...(campos[campo] ?? []), ...mensagens];
    } else {
      restantes.push(...mensagens);
    }
  }

  return { campos, restantes };
}

function semCampo(erros: ErrosCampo, campo: CampoTransferencia): ErrosCampo {
  const copia = { ...erros };
  delete copia[campo];
  return copia;
}

/**
 * Recusa local equivalente ao 422 `limite-excedido` da API (specs/009, contracts/frontend.md):
 * por operação antes do diário; valor igual ao limite é permitido.
 */
export function verificarLimites(centavos: number, limites: ResumoLimites | null): MensagemErro | null {
  if (!limites) {
    return null;
  }

  const { porOperacaoCentavos, diarioCentavos, disponivelHojeCentavos } = limites;
  if (porOperacaoCentavos !== null && centavos > porOperacaoCentavos) {
    return erroDeLimite('PorOperacao', porOperacaoCentavos, porOperacaoCentavos);
  }

  if (diarioCentavos !== null && disponivelHojeCentavos !== null && centavos > disponivelHojeCentavos) {
    return erroDeLimite('Diario', diarioCentavos, disponivelHojeCentavos);
  }

  return null;
}

function erroDeLimite(tipoLimite: string, limiteCentavos: number, disponivelCentavos: number): MensagemErro {
  return traduzirErro(422, {
    type: 'urn:bank:problema:limite-excedido',
    tipoLimite,
    limite: centavosParaNumero(limiteCentavos),
    valorDisponivel: centavosParaNumero(disponivelCentavos),
  });
}

export function useTransferencia(opcoes: { reiniciarSeConcluida?: boolean } = {}) {
  const [dados, definirDados] = useState<TransferenciaEmAndamento>(() => {
    const lidos = lerTransferencia();
    if (opcoes.reiniciarSeConcluida && lidos.concluida) {
      limparTransferencia();
      return { ...TRANSFERENCIA_VAZIA };
    }

    return lidos;
  });
  const [buscando, definirBuscando] = useState(false);
  const [errosCampo, definirErrosCampo] = useState<ErrosCampo>({});
  const [aviso, definirAviso] = useState<MensagemErro | null>(null);
  const [enviando, definirEnviando] = useState(false);

  const dadosRef = useRef(dados);
  const sequencia = useRef(0);
  const controlador = useRef<AbortController | null>(null);
  const temporizador = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  const buscaAtual = useRef<Promise<void> | null>(null);
  const enviandoRef = useRef(false);

  const atualizar = useCallback((alterar: (atual: TransferenciaEmAndamento) => TransferenciaEmAndamento) => {
    const novo = alterar(dadosRef.current);
    dadosRef.current = novo;
    salvarTransferencia(novo);
    definirDados(novo);
    return novo;
  }, []);

  const cancelarBusca = useCallback(() => {
    clearTimeout(temporizador.current);
    controlador.current?.abort();
    controlador.current = null;
    sequencia.current++;
    buscaAtual.current = null;
    definirBuscando(false);
  }, []);

  useEffect(() => cancelarBusca, [cancelarBusca]);

  /** Aplica uma recusa da API: erros por campo nos campos; o resto como aviso. */
  const aplicarErro = useCallback((erro: MensagemErro) => {
    if (erro.encerrarSessao) {
      return; // 401: o cliente já encerrou a sessão.
    }

    const { campos, restantes } = separarCampos(erro);
    if (Object.keys(campos).length > 0) {
      definirErrosCampo((atuais) => ({ ...atuais, ...campos }));
    }

    if (Object.keys(campos).length === 0 || restantes.length > 0) {
      definirAviso(restantes.length > 0 ? { ...erro, detalhe: restantes.join(' ') } : erro);
    }
  }, []);

  const consultar = useCallback(
    (documento: string): Promise<void> => {
      cancelarBusca();
      const minha = sequencia.current;
      const sinal = new AbortController();
      controlador.current = sinal;
      definirBuscando(true);

      const promessa = api.consultarDestinatario(documento, sinal.signal).then((resultado) => {
        if (minha !== sequencia.current) {
          return; // documento mudou: resposta descartada (research R6).
        }

        buscaAtual.current = null;
        controlador.current = null;
        definirBuscando(false);

        if (resultado.ok) {
          atualizar((atual) =>
            normalizarDocumento(atual.documento) === documento
              ? { ...atual, destinatario: { documento, nome: resultado.dados.nome, tipoConta: resultado.dados.tipoConta } }
              : atual,
          );
          return;
        }

        if (resultado.erro.codigo !== CODIGO_CANCELADA) {
          aplicarErro(resultado.erro);
        }
      });

      buscaAtual.current = promessa;
      return promessa;
    },
    [aplicarErro, atualizar, cancelarBusca],
  );

  const alterarDocumento = useCallback(
    (texto: string) => {
      const mascarado = mascararDocumentoDigitado(texto);
      const normalizado = normalizarDocumento(mascarado);
      if (mascarado === dadosRef.current.documento) {
        return;
      }

      cancelarBusca();
      const atual = atualizar((d) => ({
        ...d,
        documento: mascarado,
        destinatario: d.destinatario?.documento === normalizado ? d.destinatario : null,
      }));
      definirErrosCampo((atuais) => semCampo(atuais, 'documento'));
      definirAviso(null);

      if (!documentoCompleto(mascarado) || destinatarioConfirmado(atual)) {
        return;
      }

      if (tipoDocumento(mascarado) === 'cnpj') {
        void consultar(normalizado);
      } else if (cpfValido(normalizado)) {
        temporizador.current = setTimeout(() => void consultar(normalizado), ESPERA_CPF_MS);
      }
    },
    [atualizar, cancelarBusca, consultar],
  );

  /** Ao sair do campo com documento completo, consulta mesmo com dígito inválido (o erro vem da API). */
  const sairDoDocumento = useCallback(() => {
    const atual = dadosRef.current;
    if (documentoCompleto(atual.documento) && !destinatarioConfirmado(atual) && !buscaAtual.current) {
      void consultar(normalizarDocumento(atual.documento));
    }
  }, [consultar]);

  const alterarValor = useCallback(
    (texto: string) => {
      atualizar((d) => ({ ...d, valorTexto: texto }));
      definirErrosCampo((atuais) => semCampo(atuais, 'valor'));
      definirAviso((atual) => (atual?.codigo === 'saldo-insuficiente' || atual?.codigo === 'limite-excedido' ? null : atual));
    },
    [atualizar],
  );

  const alterarDescricao = useCallback(
    (texto: string) => {
      atualizar((d) => ({ ...d, descricao: texto }));
      definirErrosCampo((atuais) => semCampo(atuais, 'descricao'));
    },
    [atualizar],
  );

  /**
   * Valida e prepara a confirmação: a chave é mantida para os mesmos dados e trocada se algo
   * mudou (research R2). Devolve `true` quando a confirmação pode ser aberta.
   */
  const continuar = useCallback(
    async (saldoCentavos: number | null, limites: ResumoLimites | null = null): Promise<boolean> => {
      let atual = dadosRef.current;
      const erros: ErrosCampo = {};

      if (!documentoCompleto(atual.documento)) {
        erros.documento = [MENSAGEM_DOCUMENTO_INCOMPLETO];
      } else if (!destinatarioConfirmado(atual)) {
        await (buscaAtual.current ?? consultar(normalizarDocumento(atual.documento)));
        atual = dadosRef.current;
      }

      const valor = interpretarValor(atual.valorTexto);
      if (!valor.ok) {
        erros.valor = [valor.erro];
      }

      if (Object.keys(erros).length > 0) {
        definirErrosCampo((existentes) => ({ ...existentes, ...erros }));
        return false;
      }

      if (!valor.ok || !destinatarioConfirmado(atual)) {
        return false; // a recusa da consulta já está na tela.
      }

      // Limites antes do saldo, na mesma ordem da API (spec 009, FR-008).
      const recusaPorLimite = verificarLimites(valor.centavos, limites);
      if (recusaPorLimite) {
        definirAviso(recusaPorLimite);
        return false;
      }

      if (saldoCentavos !== null && valor.centavos > saldoCentavos) {
        definirAviso(traduzirErro(422, { type: 'urn:bank:problema:saldo-insuficiente' }));
        return false;
      }

      const assinaturaAtual = assinatura(normalizarDocumento(atual.documento), valor.centavos, atual.descricao.trim());
      atualizar((d) => {
        const preparada = prepararConfirmacao(d, assinaturaAtual);
        return preparada === d ? d : { ...preparada, concluida: null };
      });
      definirAviso(null);
      return true;
    },
    [atualizar, consultar],
  );

  /** Envia uma única vez por clique; reenvios usam a mesma chave (US3). */
  const enviar = useCallback(async (): Promise<ResultadoEnvio | null> => {
    if (enviandoRef.current) {
      return null;
    }

    const assinaturaAtual = assinaturaDe(dadosRef.current);
    const valor = interpretarValor(dadosRef.current.valorTexto);
    if (!assinaturaAtual || !valor.ok) {
      return null;
    }

    enviandoRef.current = true;
    definirEnviando(true);
    definirAviso(null);

    const atual = atualizar((d) => prepararConfirmacao(d, assinaturaAtual));
    const descricao = atual.descricao.trim();
    const resultado = await api.transferir(
      {
        documentoDestino: normalizarDocumento(atual.documento),
        valor: centavosParaNumero(valor.centavos),
        ...(descricao ? { descricao } : {}),
      },
      atual.chave ?? '',
    );

    enviandoRef.current = false;
    definirEnviando(false);

    if (resultado.ok) {
      const { comprovante, repetida } = resultado.dados;
      guardarComprovante(comprovante);
      atualizar((d) => ({ ...d, concluida: comprovante.transferenciaId }));
      return { tipo: repetida ? 'repetida' : 'concluida', transferenciaId: comprovante.transferenciaId };
    }

    if (resultado.erro.codigo === 'idempotencia-divergente') {
      atualizar((d) => ({ ...d, chave: null, assinatura: null }));
    }

    aplicarErro(resultado.erro);
    return { tipo: 'recusada', erro: resultado.erro };
  }, [aplicarErro, atualizar]);

  return {
    dados,
    buscando,
    errosCampo,
    aviso,
    enviando,
    confirmado: destinatarioConfirmado(dados),
    alterarDocumento,
    sairDoDocumento,
    alterarValor,
    alterarDescricao,
    continuar,
    enviar,
  };
}
