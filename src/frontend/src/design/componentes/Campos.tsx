import { useId, useState, type InputHTMLAttributes, type ReactNode, type Ref, type TextareaHTMLAttributes } from 'react';
import { mascararDocumento } from '../../formatacao/mascaras';
import type { TipoConta } from '../../sessao/claims';
import estilos from './componentes.module.css';

export interface CampoTextoProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'id'> {
  rotulo: string;
  /** Mensagens de erro do campo (locais ou da API). */
  erros?: string[] | undefined;
  dica?: ReactNode;
  acessorioRotulo?: ReactNode;
  acao?: ReactNode;
  ref?: Ref<HTMLInputElement>;
}

/** Campo com rótulo associado e erros anunciados por leitores de tela (FR-001a). */
export function CampoTexto({ rotulo, erros, dica, acessorioRotulo, acao, className, ref, ...props }: CampoTextoProps) {
  const id = useId();
  const idErro = `${id}-erro`;
  const idDica = `${id}-dica`;
  const temErro = !!erros && erros.length > 0;
  const descritores = [temErro ? idErro : '', dica ? idDica : ''].filter(Boolean).join(' ') || undefined;

  return (
    <div className={[estilos.campo, className ?? ''].join(' ')}>
      <div className={estilos.cabecalhoCampo}>
        <label htmlFor={id} className={estilos.rotulo}>
          {rotulo}
        </label>
        {acessorioRotulo}
      </div>
      <div className={estilos.controle}>
        <input
          {...props}
          ref={ref}
          id={id}
          className={[estilos.entrada, acao ? estilos.entradaComAcao : ''].join(' ')}
          aria-invalid={temErro || undefined}
          aria-describedby={descritores}
        />
        {acao}
      </div>
      {temErro ? (
        <div id={idErro} className={estilos.erroCampo}>
          {erros.map((erro) => (
            <span key={erro}>{erro}</span>
          ))}
        </div>
      ) : null}
      {dica ? (
        <div id={idDica} className={estilos.dica}>
          {dica}
        </div>
      ) : null}
    </div>
  );
}

/** Texto de várias linhas, com o mesmo contrato de erros e dica do {@link CampoTexto} (specs/015, FR-010). */
export function CampoTextoLongo({
  rotulo,
  erros,
  dica,
  ...props
}: Omit<TextareaHTMLAttributes<HTMLTextAreaElement>, 'id'> & Pick<CampoTextoProps, 'rotulo' | 'erros' | 'dica'>) {
  const id = useId();
  const temErro = !!erros && erros.length > 0;
  const descritores = [temErro ? `${id}-erro` : '', dica ? `${id}-dica` : ''].filter(Boolean).join(' ') || undefined;

  return (
    <div className={estilos.campo}>
      <label htmlFor={id} className={estilos.rotulo}>
        {rotulo}
      </label>
      <textarea
        {...props}
        id={id}
        className={[estilos.entrada, estilos.entradaLonga].join(' ')}
        aria-invalid={temErro || undefined}
        aria-describedby={descritores}
      />
      {temErro ? (
        <div id={`${id}-erro`} className={estilos.erroCampo}>
          {erros.map((erro) => (
            <span key={erro}>{erro}</span>
          ))}
        </div>
      ) : null}
      {dica ? (
        <div id={`${id}-dica`} className={estilos.dica}>
          {dica}
        </div>
      ) : null}
    </div>
  );
}

/** Senha com opção de mostrar/ocultar (FR-007). */
export function CampoSenha(props: Omit<CampoTextoProps, 'type' | 'acao'>) {
  const [visivel, definirVisivel] = useState(false);

  return (
    <CampoTexto
      {...props}
      type={visivel ? 'text' : 'password'}
      acao={
        <button
          type="button"
          className={estilos.acaoCampo}
          aria-pressed={visivel}
          aria-label={visivel ? 'Ocultar senha' : 'Mostrar senha'}
          onClick={() => definirVisivel((v) => !v)}
        >
          {visivel ? 'Ocultar' : 'Mostrar'}
        </button>
      }
    />
  );
}

export interface CampoDocumentoProps extends Omit<CampoTextoProps, 'rotulo' | 'onChange' | 'value'> {
  tipoConta: TipoConta;
  valor: string;
  aoAlterar: (valorMascarado: string) => void;
}

/** CPF ou CNPJ alfanumérico com máscara do tipo (FR-005). */
export function CampoDocumento({ tipoConta, valor, aoAlterar, ...props }: CampoDocumentoProps) {
  const pf = tipoConta === 'PessoaFisica';

  return (
    <CampoTexto
      {...props}
      rotulo={pf ? 'CPF' : 'CNPJ'}
      value={valor}
      onChange={(evento) => aoAlterar(mascararDocumento(evento.target.value, tipoConta))}
      inputMode={pf ? 'numeric' : 'text'}
      autoCapitalize={pf ? 'off' : 'characters'}
      autoComplete="off"
      spellCheck={false}
      placeholder={pf ? '000.000.000-00' : 'XX.XXX.XXX/XXXX-00'}
    />
  );
}
