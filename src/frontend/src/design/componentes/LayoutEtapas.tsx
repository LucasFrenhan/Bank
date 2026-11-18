import type { ReactNode } from 'react';
import { Link } from 'react-router';
import { Etapas } from './Etapas';
import { Marca } from './Feedback';
import { IconeVoltar } from './Icones';
import estilos from './LayoutEtapas.module.css';

interface Voltar {
  rotulo: string;
  para: string;
}

/**
 * Estrutura das telas de transferência (protótipo desktop/mobile 06 a 08): sem menu, com
 * cabeçalho de volta + etapas (Dados, Confirmação) ou só a marca (Comprovante).
 */
export function LayoutEtapas({ voltar, etapa, children }: { voltar?: Voltar; etapa?: 1 | 2 | 3; children: ReactNode }) {
  return (
    <div className={estilos.estrutura}>
      <a href="#conteudo" className={estilos.pularConteudo}>
        Pular para o conteúdo
      </a>

      <header className={estilos.cabecalho}>
        {voltar ? (
          <Link to={voltar.para} className={estilos.voltar}>
            <IconeVoltar />
            {voltar.rotulo}
          </Link>
        ) : (
          <Marca />
        )}
        {etapa ? <Etapas atual={etapa} /> : null}
      </header>

      <main id="conteudo" className={estilos.conteudo} tabIndex={-1}>
        {children}
      </main>
    </div>
  );
}
