import { useRef, useState } from 'react';
import { Navigate, useNavigate } from 'react-router';
import { Botao } from '../design/componentes/Botao';
import { Cartao } from '../design/componentes/Feedback';
import { LayoutEtapas } from '../design/componentes/LayoutEtapas';
import { formatarCentavos, interpretarValor, saldoEmCentavos } from '../formatacao/valor';
import { useSessaoAtiva } from '../sessao/ContextoSessao';
import { prontaParaConfirmar, useTransferencia } from '../transferencia/useTransferencia';
import { AvisoRepetida, AvisoTransferencia, focarPrimeiroErro, IndisponivelPj } from './avisosTransferencia';
import { useTitulo } from './comum';
import estilos from './Transferencia.module.css';
import { descreverDocumento, nomeTipoConta } from './Transferir';
import { useCarteira } from './useCarteira';

const EDITAR_DADOS = { rotulo: 'Editar dados', para: '/transferir' };

/** Etapa Confirmação e envio (specs/007: US1–US3; FR-007 a FR-010, FR-012). */
export function ConfirmarTransferencia() {
  useTitulo('Confirmar transferência');
  const sessao = useSessaoAtiva();

  if (sessao.tipoConta === 'PessoaJuridica') {
    return (
      <LayoutEtapas voltar={{ rotulo: 'Voltar ao início', para: '/' }} etapa={2}>
        <div className={estilos.pagina}>
          <h1>Confira antes de enviar</h1>
          <IndisponivelPj />
        </div>
      </LayoutEtapas>
    );
  }

  return <Confirmacao />;
}

function Confirmacao() {
  const navegar = useNavigate();
  const carteira = useCarteira();
  const transferencia = useTransferencia();
  const [repetida, definirRepetida] = useState<string | null>(null);
  const area = useRef<HTMLDivElement>(null);
  const { dados, aviso, enviando, errosCampo } = transferencia;

  if (!prontaParaConfirmar(dados) || !dados.destinatario) {
    return <Navigate to="/transferir" replace />;
  }

  const valor = interpretarValor(dados.valorTexto);
  const centavos = valor.ok ? valor.centavos : 0;
  const saldoCentavos = carteira.estado === 'pronta' ? saldoEmCentavos(carteira.carteira.saldo) : null;
  const descricao = dados.descricao.trim();
  const errosDeCampo = Object.values(errosCampo).flat();

  async function transferir() {
    definirRepetida(null);
    const resultado = await transferencia.enviar();
    if (!resultado) return;

    if (resultado.tipo === 'concluida') {
      void navegar(`/transferencias/${resultado.transferenciaId}`);
      return;
    }

    if (resultado.tipo === 'repetida') {
      definirRepetida(resultado.transferenciaId);
    } else if (resultado.erro.codigo === 'saldo-insuficiente') {
      carteira.recarregar();
    }

    focarPrimeiroErro(area.current);
  }

  return (
    <LayoutEtapas voltar={EDITAR_DADOS} etapa={2}>
      <div className={estilos.pagina} ref={area}>
        <h1>Confira antes de enviar</h1>

        <Cartao>
          <div className={estilos.destaque}>
            <p className={estilos.rotuloDestaque}>Você vai transferir</p>
            <p className={estilos.valorDestaque}>{formatarCentavos(centavos)}</p>
          </div>
          <ul className={estilos.linhas} aria-label="Resumo da transferência">
            <Linha rotulo="Para" valor={dados.destinatario.nome} />
            <Linha rotulo="Documento" valor={descreverDocumento(dados.destinatario.documento)} />
            <Linha rotulo="Tipo de conta" valor={nomeTipoConta(dados.destinatario.tipoConta)} />
            {descricao ? <Linha rotulo="Descrição" valor={descricao} /> : null}
            <Linha rotulo="Saldo depois" valor={saldoCentavos !== null ? formatarCentavos(saldoCentavos - centavos) : '—'} />
          </ul>
        </Cartao>

        <p className={estilos.nota}>
          Transferências são concluídas na hora e não podem ser desfeitas. Se a conexão cair, pode tentar de novo: o
          valor não é cobrado duas vezes.
        </p>

        {repetida ? <AvisoRepetida transferenciaId={repetida} /> : null}
        {aviso ? <AvisoTransferencia erro={aviso} saldoCentavos={saldoCentavos} /> : null}
        {errosDeCampo.length > 0 ? (
          <AvisoTransferencia
            erro={{ codigo: 'validacao', mensagem: 'Confira os dados da transferência', detalhe: errosDeCampo.join(' ') }}
            saldoCentavos={saldoCentavos}
          />
        ) : null}

        <div className={estilos.acoes}>
          <Botao variante="secundario" onClick={() => void navegar('/transferir')}>
            Voltar
          </Botao>
          <Botao onClick={() => void transferir()} carregando={enviando}>
            Transferir {formatarCentavos(centavos)}
          </Botao>
        </div>
      </div>
    </LayoutEtapas>
  );
}

function Linha({ rotulo, valor }: { rotulo: string; valor: string }) {
  return (
    <li className={estilos.linha}>
      <span className={estilos.rotuloLinha}>{rotulo}</span>
      <span className={estilos.valorLinha}>{valor}</span>
    </li>
  );
}
