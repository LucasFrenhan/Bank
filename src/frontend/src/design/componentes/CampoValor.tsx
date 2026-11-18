import { useId, type ReactNode, type Ref } from 'react';
import estilos from './componentes.module.css';

/**
 * Valor em reais com prefixo "R$" e teclado decimal (specs/007, FR-004). Texto de ajuda e erros
 * associados ao campo por `aria-describedby`.
 */
export function CampoValor({
  valor,
  aoAlterar,
  erros,
  dica,
  ref,
}: {
  valor: string;
  aoAlterar: (texto: string) => void;
  erros?: string[] | undefined;
  dica?: ReactNode;
  ref?: Ref<HTMLInputElement>;
}) {
  const id = useId();
  const temErro = !!erros && erros.length > 0;
  const descritores = [temErro ? `${id}-erro` : '', dica ? `${id}-dica` : ''].filter(Boolean).join(' ') || undefined;

  return (
    <div className={estilos.campo}>
      <label htmlFor={id} className={estilos.rotulo}>
        Valor
      </label>
      <div className={[estilos.controle, estilos.controleValor].join(' ')}>
        <span className={estilos.prefixoValor} aria-hidden="true">
          R$
        </span>
        <input
          ref={ref}
          id={id}
          className={[estilos.entrada, estilos.entradaValor].join(' ')}
          value={valor}
          onChange={(evento) => aoAlterar(evento.target.value)}
          inputMode="decimal"
          autoComplete="off"
          placeholder="0,00"
          aria-invalid={temErro || undefined}
          aria-describedby={descritores}
        />
      </div>
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
