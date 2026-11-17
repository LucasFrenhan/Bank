import type { ReactNode } from 'react';
import { NavLink } from 'react-router';
import { useSessaoAtiva, useSessao } from '../../sessao/ContextoSessao';
import type { TipoConta } from '../../sessao/claims';
import { Marca } from './Feedback';
import {
  IconeCobrancas,
  IconeExtrato,
  IconeInicio,
  IconePerfil,
  IconeRecebimentos,
  IconeSair,
  IconeTransferir,
} from './Icones';
import estilos from './Layout.module.css';

interface ItemNavegacao {
  rotulo: string;
  icone: () => ReactNode;
  /** Rota; ausente = "Em breve" (visível, não navegável — FR-003). */
  para?: string;
}

/** Itens por perfil (contracts/frontend.md, "Navegação por perfil"). */
export const ITENS_NAVEGACAO: Record<TipoConta, ItemNavegacao[]> = {
  PessoaFisica: [
    { rotulo: 'Início', icone: IconeInicio, para: '/' },
    { rotulo: 'Transferir', icone: IconeTransferir },
    { rotulo: 'Extrato', icone: IconeExtrato },
    { rotulo: 'Perfil', icone: IconePerfil, para: '/perfil' },
  ],
  PessoaJuridica: [
    { rotulo: 'Início', icone: IconeInicio, para: '/' },
    { rotulo: 'Recebimentos', icone: IconeRecebimentos },
    { rotulo: 'Cobranças', icone: IconeCobrancas },
    { rotulo: 'Perfil', icone: IconePerfil, para: '/perfil' },
  ],
};

/** Estrutura das telas autenticadas: menu lateral (desktop) e barra inferior (mobile). */
export function Layout({ children }: { children: ReactNode }) {
  const sessao = useSessaoAtiva();
  const { sair } = useSessao();
  const itens = ITENS_NAVEGACAO[sessao.tipoConta];

  return (
    <div className={estilos.estrutura}>
      <a href="#conteudo" className={estilos.pularConteudo}>
        Pular para o conteúdo
      </a>

      <nav className={estilos.menuLateral} aria-label="Menu principal">
        <div className={estilos.marcaLateral}>
          <Marca />
        </div>
        <ul className={estilos.listaLateral}>
          {itens.map((item) => (
            <li key={item.rotulo}>
              {item.para ? (
                <NavLink to={item.para} end className={estilos.itemLateral}>
                  {item.icone()}
                  {item.rotulo}
                </NavLink>
              ) : (
                <span className={estilos.itemLateral} aria-disabled="true">
                  {item.icone()}
                  {item.rotulo}
                  <span className="apenas-leitor-de-tela">, </span>
                  <span className={estilos.emBreve}>(Em breve)</span>
                </span>
              )}
            </li>
          ))}
        </ul>
        <button type="button" className={[estilos.itemLateral, estilos.sairLateral].join(' ')} onClick={sair}>
          <IconeSair />
          Sair
        </button>
      </nav>

      <main id="conteudo" className={estilos.conteudo} tabIndex={-1}>
        {children}
      </main>

      <nav className={estilos.barraInferior} aria-label="Menu principal (celular)">
        <ul className={estilos.listaInferior}>
          {itens.map((item) => (
            <li key={item.rotulo}>
              {item.para ? (
                <NavLink to={item.para} end className={estilos.itemInferior}>
                  {item.icone()}
                  {item.rotulo}
                </NavLink>
              ) : (
                <span className={estilos.itemInferior} aria-disabled="true">
                  {item.icone()}
                  {item.rotulo}
                  <span className={estilos.emBreve}>Em breve</span>
                </span>
              )}
            </li>
          ))}
        </ul>
      </nav>
    </div>
  );
}
