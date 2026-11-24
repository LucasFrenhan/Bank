import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react';
import { api, type CartaoDigitado } from '../../api/cliente';
import type { MensagemErro } from '../../api/erros';
import {
  CARTAO_VAZIO,
  mascararCvv,
  mascararNumero,
  mascararValidade,
  paraEnvio,
  separarErrosDaApi,
  validarCartao,
  type CampoCartao,
  type ErrosCartao,
} from '../../cartao/validacao';
import { Botao } from '../../design/componentes/Botao';
import { CampoTexto } from '../../design/componentes/Campos';
import { AvisoErro, Carregando, Cartao } from '../../design/componentes/Feedback';
import { formatarCentavos, saldoEmCentavos } from '../../formatacao/valor';
import { useOperacao } from '../../operacoes/useOperacao';
import { focarPrimeiroErro } from '../avisosTransferencia';
import { useTitulo } from '../comum';
import { useConsulta } from '../useConsulta';
import type { CobrancaPronta } from './Cobranca';
import estilos from './Cobrancas.module.css';
import { ResumoCobranca } from './ResumoCobranca';

/** Pagamento da cobrança com o cartão (specs/015-frontend-cartao-credito, US3; FR-013 a FR-017). */
export function PagarCobranca({ consulta }: { consulta: CobrancaPronta }) {
  useTitulo('Pagar cobrança');
  const cartao = useConsulta('cartao', api.obterMeuCartao);
  const semCartao = cartao.estado === 'erro' && cartao.erro.codigo === 'cartao-nao-encontrado' ? cartao.erro : null;

  return (
    <div className={estilos.pagina}>
      <h1>Pagar cobrança</h1>
      <ResumoCobranca cobranca={consulta.dados} />
      {cartao.estado === 'carregando' ? <Carregando texto="Carregando…" /> : null}
      {semCartao ? <AvisoErro erro={semCartao} /> : null}
      {cartao.estado !== 'carregando' && !semCartao ? <Formulario consulta={consulta} aoFicarSemCartao={cartao.recarregar} /> : null}
    </div>
  );
}

const RECUSAS = new Set(['cartao-nao-autorizado', 'cartao-bloqueado-fatura-vencida', 'limite-cartao-insuficiente']);

const MASCARAS: Partial<Record<CampoCartao, (texto: string) => string>> = {
  numero: mascararNumero,
  validade: mascararValidade,
  cvv: mascararCvv,
};

/** Os dados digitados vivem só neste componente (FR-008). */
function Formulario({ consulta, aoFicarSemCartao }: { consulta: CobrancaPronta; aoFicarSemCartao: () => void }) {
  const cobranca = consulta.dados;
  const [cartao, definirCartao] = useState<CartaoDigitado>(CARTAO_VAZIO);
  const [erros, definirErros] = useState<ErrosCartao>({});
  const [buscando, definirBuscando] = useState(false);
  const { enviando, erro, definirErro, executar } = useOperacao(`cobranca:${cobranca.codigo}`);
  const formulario = useRef<HTMLFormElement>(null);
  const cvv = useRef<HTMLInputElement>(null);
  const valor = formatarCentavos(saldoEmCentavos(cobranca.valor));

  // A página não pode voltar do cache do navegador com o cartão preenchido.
  useEffect(() => {
    const limpar = () => {
      definirCartao(CARTAO_VAZIO);
      definirErros({});
    };
    window.addEventListener('pagehide', limpar);
    return () => window.removeEventListener('pagehide', limpar);
  }, []);

  const alterar = useCallback((campo: CampoCartao, texto: string) => {
    definirCartao((atual) => ({ ...atual, [campo]: MASCARAS[campo]?.(texto) ?? texto }));
    definirErros((atuais) => ({ ...atuais, [campo]: undefined }));
  }, []);

  /** Preenche nome, número e validade; o CVV é sempre digitado (FR-014). */
  async function usarMeuCartao() {
    definirBuscando(true);
    definirErro(null);
    const resultado = await api.obterDadosCartao();
    definirBuscando(false);

    if (resultado.ok) {
      const { nomeImpresso, numero, validade } = resultado.dados;
      definirCartao({ nome: nomeImpresso, numero: mascararNumero(numero), validade, cvv: '' });
      definirErros({});
      cvv.current?.focus();
    } else if (resultado.erro.codigo === 'cartao-nao-encontrado') {
      aoFicarSemCartao();
    } else if (!resultado.erro.encerrarSessao) {
      definirErro({ ...resultado.erro, acao: undefined });
    }
  }

  async function pagar(evento: FormEvent) {
    evento.preventDefault();
    const locais = validarCartao(cartao);
    definirErros(locais);
    if (Object.keys(locais).length > 0) {
      focarPrimeiroErro(formulario.current);
      return;
    }

    const resultado = await executar((chave) => api.pagarCobranca(cobranca.cobrancaId, paraEnvio(cartao), chave));
    if (!resultado) return;

    if (resultado.ok) {
      const { status, pagaEm, bandeira, ultimosDigitosCartao, codigoAutorizacao } = resultado.dados;
      consulta.definir({ ...cobranca, status, pagaEm, pagamento: { bandeira, ultimosDigitosCartao, codigoAutorizacao } });
      return;
    }

    await tratarRecusa(resultado.erro);
  }

  async function tratarRecusa(recusa: MensagemErro) {
    if (recusa.campos) {
      const { erros: daApi, restantes } = separarErrosDaApi(recusa.campos);
      definirErros(daApi);
      definirErro(restantes.length > 0 ? { ...recusa, detalhe: restantes.join(' ') } : Object.keys(daApi).length > 0 ? null : recusa);
      focarPrimeiroErro(formulario.current);
      return;
    }

    if (recusa.codigo === 'cobranca-nao-pendente' || recusa.codigo === 'cobranca-nao-encontrada') {
      consulta.recarregar(); // mostra a situação atual: comprovante, indisponível ou não encontrada.
      return;
    }

    if (recusa.codigo === 'idempotencia-divergente') {
      await consulta.atualizar(); // se já paga por esta pessoa, vira o comprovante.
      alterar('cvv', '');
      return;
    }

    if (RECUSAS.has(recusa.codigo)) {
      alterar('cvv', '');
      cvv.current?.focus();
    } else {
      focarPrimeiroErro(formulario.current);
    }

    if (recusa.codigo === 'limite-cartao-insuficiente') {
      const atual = await api.obterMeuCartao();
      if (atual.ok) {
        const disponivel = formatarCentavos(saldoEmCentavos(atual.dados.limiteDisponivel));
        definirErro({ ...recusa, detalhe: `Você tem ${disponivel} de limite disponível.` });
      }
    }
  }

  return (
    <form ref={formulario} onSubmit={(e) => void pagar(e)} noValidate>
      <Cartao className={estilos.quadro} rotulo="Dados do cartão">
        <div className={estilos.cabecalho}>
          <h2>Cartão One Bank</h2>
          <Botao variante="secundario" carregando={buscando} disabled={enviando || buscando} onClick={() => void usarMeuCartao()}>
            Usar meu cartão
          </Botao>
        </div>
        {erro ? (
          <AvisoErro
            erro={erro}
            aoTentarDeNovo={() => formulario.current?.requestSubmit()}
            aoAgir={() => {
              definirErro(null);
              consulta.recarregar();
            }}
          />
        ) : null}
        <CampoTexto
          rotulo="Nome impresso no cartão"
          value={cartao.nome}
          onChange={(e) => alterar('nome', e.target.value)}
          erros={erros.nome}
          autoComplete="cc-name"
          autoCapitalize="characters"
          spellCheck={false}
          maxLength={150}
        />
        <CampoTexto
          rotulo="Número do cartão"
          value={cartao.numero}
          onChange={(e) => alterar('numero', e.target.value)}
          erros={erros.numero}
          autoComplete="cc-number"
          inputMode="numeric"
        />
        <div className={estilos.doisCampos}>
          <CampoTexto
            rotulo="Validade"
            value={cartao.validade}
            onChange={(e) => alterar('validade', e.target.value)}
            erros={erros.validade}
            autoComplete="cc-exp"
            inputMode="numeric"
            placeholder="MM/AA"
          />
          <CampoTexto
            ref={cvv}
            rotulo="CVV"
            value={cartao.cvv}
            onChange={(e) => alterar('cvv', e.target.value)}
            erros={erros.cvv}
            autoComplete="cc-csc"
            inputMode="numeric"
            placeholder="3 dígitos"
          />
        </div>
        <Botao type="submit" largo carregando={enviando}>
          Pagar {valor}
        </Botao>
        <p className={estilos.nota}>
          Os dados do cartão não ficam guardados. Se a conexão cair, pode tentar de novo: a cobrança não é paga duas vezes.
        </p>
      </Cartao>
    </form>
  );
}
