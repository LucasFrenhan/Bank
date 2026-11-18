import { useRef, type FormEvent } from 'react';
import { useNavigate } from 'react-router';
import { Botao } from '../design/componentes/Botao';
import { CampoTexto } from '../design/componentes/Campos';
import { CampoValor } from '../design/componentes/CampoValor';
import { Cartao } from '../design/componentes/Feedback';
import { IconeConfirmado } from '../design/componentes/Icones';
import { LayoutEtapas } from '../design/componentes/LayoutEtapas';
import { ocultarDocumento, tipoDocumento } from '../formatacao/mascaras';
import { formatarCentavos, saldoEmCentavos } from '../formatacao/valor';
import { useSessaoAtiva } from '../sessao/ContextoSessao';
import { NOME_TIPO_CONTA } from '../sessao/permissoes';
import type { TipoConta } from '../sessao/claims';
import { useTransferencia } from '../transferencia/useTransferencia';
import { AvisoTransferencia, focarPrimeiroErro, IndisponivelPj } from './avisosTransferencia';
import { useTitulo } from './comum';
import estilos from './Transferencia.module.css';
import { useCarteira } from './useCarteira';

export const LIMITE_DESCRICAO = 200;

const VOLTAR_INICIO = { rotulo: 'Voltar ao início', para: '/' };

/** Etapa Dados da transferência (specs/007: US1, US2; FR-001 a FR-006). */
export function Transferir() {
  useTitulo('Transferir');
  const sessao = useSessaoAtiva();

  return (
    <LayoutEtapas voltar={VOLTAR_INICIO} etapa={1}>
      {sessao.tipoConta === 'PessoaJuridica' ? (
        <div className={estilos.pagina}>
          <h1>Transferir dinheiro</h1>
          <IndisponivelPj />
        </div>
      ) : (
        <FormularioTransferir />
      )}
    </LayoutEtapas>
  );
}

function iniciais(nome: string): string {
  return nome
    .trim()
    .split(/\s+/)
    .slice(0, 2)
    .map((parte) => parte.charAt(0).toUpperCase())
    .join('');
}

export function descreverDocumento(documento: string): string {
  return `${tipoDocumento(documento) === 'cpf' ? 'CPF' : 'CNPJ'} ${ocultarDocumento(documento)}`;
}

export function nomeTipoConta(tipoConta: string): string {
  return NOME_TIPO_CONTA[tipoConta as TipoConta] ?? tipoConta;
}

function FormularioTransferir() {
  const navegar = useNavigate();
  const carteira = useCarteira();
  const transferencia = useTransferencia({ reiniciarSeConcluida: true });
  const formulario = useRef<HTMLFormElement>(null);
  const { dados, errosCampo, aviso, buscando, confirmado } = transferencia;
  const saldoCentavos = carteira.estado === 'pronta' ? saldoEmCentavos(carteira.carteira.saldo) : null;

  async function enviar(evento: FormEvent) {
    evento.preventDefault();
    if (await transferencia.continuar(saldoCentavos)) {
      void navegar('/transferir/confirmar');
    } else {
      focarPrimeiroErro(formulario.current);
    }
  }

  return (
    <form ref={formulario} className={estilos.formulario} onSubmit={(e) => void enviar(e)} noValidate>
      <h1>Transferir dinheiro</h1>

      <Cartao className={estilos.cartao}>
        <CampoTexto
          rotulo="CPF ou CNPJ de quem vai receber"
          value={dados.documento}
          onChange={(e) => transferencia.alterarDocumento(e.target.value)}
          onBlur={transferencia.sairDoDocumento}
          erros={errosCampo.documento}
          inputMode={/[A-Z]/i.test(dados.documento) ? 'text' : 'numeric'}
          autoCapitalize="characters"
          autoComplete="off"
          spellCheck={false}
          placeholder="000.000.000-00 ou XX.XXX.XXX/XXXX-00"
        />

        <p className={estilos.statusBusca} role="status" aria-live="polite">
          {buscando ? 'Buscando titular…' : ''}
        </p>

        {confirmado && dados.destinatario ? (
          <div className={estilos.titular} role="group" aria-label="Destinatário">
            <span className={estilos.iniciais} aria-hidden="true">
              {iniciais(dados.destinatario.nome)}
            </span>
            <span className={estilos.dadosTitular}>
              <span className={estilos.nomeTitular}>{dados.destinatario.nome}</span>
              <span className={estilos.detalheTitular}>
                {nomeTipoConta(dados.destinatario.tipoConta)} · {descreverDocumento(dados.destinatario.documento)}
              </span>
            </span>
            <span className={estilos.confirmado}>
              <IconeConfirmado />
              <span className="apenas-leitor-de-tela">Destinatário confirmado</span>
            </span>
          </div>
        ) : null}

        {aviso && aviso.codigo !== 'saldo-insuficiente' ? <AvisoTransferencia erro={aviso} saldoCentavos={saldoCentavos} /> : null}
      </Cartao>

      <Cartao className={estilos.cartao}>
        <CampoValor
          valor={dados.valorTexto}
          aoAlterar={transferencia.alterarValor}
          erros={errosCampo.valor}
          dica={saldoCentavos !== null ? `Disponível: ${formatarCentavos(saldoCentavos)} · mínimo R$ 0,01` : 'Mínimo R$ 0,01'}
        />

        {aviso?.codigo === 'saldo-insuficiente' ? <AvisoTransferencia erro={aviso} saldoCentavos={saldoCentavos} /> : null}

        <CampoTexto
          rotulo="Descrição (opcional)"
          value={dados.descricao}
          onChange={(e) => transferencia.alterarDescricao(e.target.value)}
          erros={errosCampo.descricao}
          maxLength={LIMITE_DESCRICAO}
          autoComplete="off"
          dica={
            <span className={estilos.contador}>
              {dados.descricao.length}/{LIMITE_DESCRICAO}
            </span>
          }
        />
      </Cartao>

      <Botao type="submit" largo>
        Continuar
      </Botao>
    </form>
  );
}
