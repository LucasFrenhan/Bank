import estilos from './LayoutEtapas.module.css';
import { IconeConfirmado } from './Icones';

const ETAPAS = ['Dados', 'Confirmar', 'Comprovante'] as const;

/** Indicador Dados → Confirmar → Comprovante do protótipo (specs/007, US1 cenário 1). */
export function Etapas({ atual }: { atual: 1 | 2 | 3 }) {
  return (
    <ol className={estilos.etapas} aria-label="Etapas da transferência">
      {ETAPAS.map((rotulo, indice) => {
        const numero = indice + 1;
        const concluida = numero < atual;
        const corrente = numero === atual;

        return (
          <li
            key={rotulo}
            className={[estilos.etapa, corrente ? estilos.etapaAtual : '', concluida ? estilos.etapaConcluida : ''].join(' ')}
            aria-current={corrente ? 'step' : undefined}
          >
            <span className={estilos.marcadorEtapa} aria-hidden="true">
              {concluida ? <IconeConfirmado /> : numero}
            </span>
            <span className={estilos.rotuloEtapa}>{rotulo}</span>
            {concluida ? <span className="apenas-leitor-de-tela"> (concluída)</span> : null}
          </li>
        );
      })}
    </ol>
  );
}
