import { useRef, useState, type FormEvent } from 'react';
import { api, type CartaoCredito } from '../../api/cliente';
import type { MensagemErro } from '../../api/erros';
import { Botao } from '../../design/componentes/Botao';
import { Aviso, AvisoErro } from '../../design/componentes/Feedback';
import { focarPrimeiroErro } from '../avisosTransferencia';
import estilos from './Cartao.module.css';

const BANDEIRAS = [
  { codigo: '001', nome: 'Visa' },
  { codigo: '002', nome: 'Mastercard' },
];

/** Solicitação do cartão: bandeira obrigatória e definitiva (specs/015-frontend-cartao-credito, FR-005). */
export function SolicitarCartao({
  aoEmitir,
  aoJaExistir,
}: {
  aoEmitir: (cartao: CartaoCredito) => void;
  aoJaExistir: (erro: MensagemErro) => void;
}) {
  const [bandeira, definirBandeira] = useState<string | null>(null);
  const [semBandeira, definirSemBandeira] = useState(false);
  const [erro, definirErro] = useState<MensagemErro | null>(null);
  const [enviando, definirEnviando] = useState(false);
  const formulario = useRef<HTMLFormElement>(null);
  const nome = BANDEIRAS.find((b) => b.codigo === bandeira)?.nome;

  async function solicitar(evento: FormEvent) {
    evento.preventDefault();
    if (enviando) return;
    if (!bandeira) {
      definirSemBandeira(true);
      formulario.current?.querySelector<HTMLInputElement>('input')?.focus();
      return;
    }

    definirEnviando(true);
    definirErro(null);
    const resultado = await api.solicitarCartao(bandeira);
    definirEnviando(false);

    if (resultado.ok) {
      aoEmitir(resultado.dados);
    } else if (resultado.erro.codigo === 'cartao-ja-existente') {
      aoJaExistir(resultado.erro);
    } else if (!resultado.erro.encerrarSessao) {
      definirErro(resultado.erro);
      focarPrimeiroErro(formulario.current);
    }
  }

  return (
    <form ref={formulario} className={[estilos.pagina, estilos.estreita].join(' ')} onSubmit={(e) => void solicitar(e)} noValidate>
      <div>
        <h1>Peça o seu cartão de crédito</h1>
        <p className={estilos.apoio}>Pronto para usar na hora.</p>
      </div>

      <fieldset className={estilos.bandeiras} aria-describedby={semBandeira ? 'erro-bandeira' : undefined}>
        <legend>Escolha a bandeira</legend>
        <div className={estilos.opcoes}>
          {BANDEIRAS.map((opcao) => (
            <label key={opcao.codigo} className={[estilos.opcao, bandeira === opcao.codigo ? estilos.opcaoMarcada : ''].join(' ')}>
              <input
                type="radio"
                name="bandeira"
                value={opcao.codigo}
                checked={bandeira === opcao.codigo}
                onChange={() => {
                  definirBandeira(opcao.codigo);
                  definirSemBandeira(false);
                }}
              />
              {opcao.nome}
            </label>
          ))}
        </div>
        {semBandeira ? (
          <p id="erro-bandeira" className={estilos.erroBandeira} role="alert">
            Escolha a bandeira
          </p>
        ) : null}
      </fieldset>

      <Aviso variante="info" titulo="Escolha com calma">
        <p>A bandeira não pode ser trocada depois, e cada CPF pode ter só um cartão.</p>
      </Aviso>

      {erro ? <AvisoErro erro={erro} aoTentarDeNovo={() => formulario.current?.requestSubmit()} /> : null}

      <Botao type="submit" largo carregando={enviando}>
        Solicitar cartão{nome ? ` ${nome}` : ''}
      </Botao>
    </form>
  );
}
