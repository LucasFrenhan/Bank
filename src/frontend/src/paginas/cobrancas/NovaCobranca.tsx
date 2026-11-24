import { useRef, useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router';
import { api } from '../../api/cliente';
import type { MensagemErro } from '../../api/erros';
import { caminhoDaCobranca } from '../../cobrancas/codigo';
import { Botao } from '../../design/componentes/Botao';
import { CampoTextoLongo } from '../../design/componentes/Campos';
import { CampoValor } from '../../design/componentes/CampoValor';
import { Aviso, AvisoErro, Cartao } from '../../design/componentes/Feedback';
import { LayoutEtapas } from '../../design/componentes/LayoutEtapas';
import { centavosParaNumero, interpretarValor, TAXA_ANTECIPACAO_PERCENTUAL } from '../../formatacao/valor';
import { focarPrimeiroErro } from '../avisosTransferencia';
import { useTitulo } from '../comum';
import { SomentePerfil } from '../SomentePerfil';
import estilos from './Cobrancas.module.css';

export const LIMITE_DESCRICAO = 200;

type Erros = { valor?: string[]; descricao?: string[] };

/** Emissão de cobrança pela PJ (specs/015-frontend-cartao-credito, US2; FR-010). */
export function NovaCobranca() {
  useTitulo('Nova cobrança');

  return (
    <LayoutEtapas voltar={{ rotulo: 'Voltar às cobranças', para: '/cobrancas' }}>
      <SomentePerfil tipo="PessoaJuridica" titulo="Nova cobrança">
        <Formulario />
      </SomentePerfil>
    </LayoutEtapas>
  );
}

function Formulario() {
  const navegar = useNavigate();
  const [valor, definirValor] = useState('');
  const [descricao, definirDescricao] = useState('');
  const [erros, definirErros] = useState<Erros>({});
  const [aviso, definirAviso] = useState<MensagemErro | null>(null);
  const [enviando, definirEnviando] = useState(false);
  const enviandoRef = useRef(false);
  const formulario = useRef<HTMLFormElement>(null);

  async function emitir(evento: FormEvent) {
    evento.preventDefault();
    if (enviandoRef.current) return;

    const interpretado = interpretarValor(valor);
    const texto = descricao.trim();
    const locais: Erros = {
      ...(interpretado.ok ? {} : { valor: [interpretado.erro] }),
      ...(texto ? {} : { descricao: ['Informe a descrição da cobrança'] }),
    };
    definirErros(locais);
    definirAviso(null);
    if (!interpretado.ok || !texto) {
      focarPrimeiroErro(formulario.current);
      return;
    }

    enviandoRef.current = true;
    definirEnviando(true);
    const resultado = await api.emitirCobranca(centavosParaNumero(interpretado.centavos), texto);
    enviandoRef.current = false;
    definirEnviando(false);

    if (resultado.ok) {
      void navegar(caminhoDaCobranca(resultado.dados.codigo), { replace: true });
      return;
    }

    const { erro } = resultado;
    if (erro.encerrarSessao) return;
    const { valor: noValor, descricao: naDescricao, ...outros } = erro.campos ?? {};
    definirErros({ ...(noValor ? { valor: noValor } : {}), ...(naDescricao ? { descricao: naDescricao } : {}) });
    if (!noValor && !naDescricao) {
      const restantes = Object.values(outros).flat().join(' ');
      definirAviso(restantes ? { ...erro, detalhe: restantes } : erro);
    }
    focarPrimeiroErro(formulario.current);
  }

  return (
    <form ref={formulario} className={estilos.pagina} onSubmit={(e) => void emitir(e)} noValidate>
      <h1>Nova cobrança</h1>
      <Cartao className={estilos.quadro} rotulo="Dados da cobrança">
        <CampoValor
          valor={valor}
          aoAlterar={(texto) => {
            definirValor(texto);
            definirErros(({ descricao: outro }) => (outro ? { descricao: outro } : {}));
          }}
          erros={erros.valor}
          dica="Mínimo de R$ 0,01."
        />
        <CampoTextoLongo
          rotulo="Descrição"
          value={descricao}
          onChange={(e) => {
            definirDescricao(e.target.value);
            definirErros(({ valor: outro }) => (outro ? { valor: outro } : {}));
          }}
          erros={erros.descricao}
          maxLength={LIMITE_DESCRICAO}
          rows={3}
          dica={`${descricao.length}/${LIMITE_DESCRICAO}`}
        />
        <Aviso variante="info" titulo="Quando você recebe">
          <p>
            {`O valor integral cai em 30 dias úteis depois do pagamento. Para receber antes, a taxa é de ${TAXA_ANTECIPACAO_PERCENTUAL}%.`}
          </p>
        </Aviso>
      </Cartao>
      {aviso ? <AvisoErro erro={aviso} aoTentarDeNovo={() => formulario.current?.requestSubmit()} /> : null}
      <Botao type="submit" largo carregando={enviando}>
        Emitir cobrança
      </Botao>
    </form>
  );
}
