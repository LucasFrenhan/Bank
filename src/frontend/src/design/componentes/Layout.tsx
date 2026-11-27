import type { ReactNode } from 'react';
import { Link, NavLink, useLocation } from 'react-router';
import { useSessaoAtiva, useSessao } from '../../sessao/ContextoSessao';
import type { TipoConta } from '../../sessao/claims';
import { Marca } from './Feedback';
import {
  IconeCartao,
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
  para: string;
  /** Só no menu lateral (a barra inferior tem 4 itens — specs/015, FR-001). */
  somenteDesktop?: boolean;
  /** Áreas cujas rotas filhas também marcam o item (specs/015, contracts/frontend.md). */
  areas?: string[];
}

/** Itens por perfil (contracts/frontend.md v1.4.0, "Navegação por perfil"). */
export const ITENS_NAVEGACAO: Record<TipoConta, ItemNavegacao[]> = {
  PessoaFisica: [
    { rotulo: 'Início', icone: IconeInicio, para: '/' },
    { rotulo: 'Transferir', icone: IconeTransferir, para: '/transferir', somenteDesktop: true },
    { rotulo: 'Extrato', icone: IconeExtrato, para: '/extrato' },
    { rotulo: 'Cartão', icone: IconeCartao, para: '/cartao', areas: ['/cartao'] },
    { rotulo: 'Perfil', icone: IconePerfil, para: '/perfil' },
  ],
  PessoaJuridica: [
    { rotulo: 'Início', icone: IconeInicio, para: '/' },
    { rotulo: 'Recebimentos', icone: IconeRecebimentos, para: '/extrato?direcao=entradas' },
    { rotulo: 'Cobranças', icone: IconeCobrancas, para: '/cobrancas', areas: ['/cobrancas', '/c'] },
    { rotulo: 'Perfil', icone: IconePerfil, para: '/perfil' },
  ],
};

function ItemMenu({ item, className }: { item: ItemNavegacao; className: string | undefined }) {
  const { pathname } = useLocation();
  const conteudo = (
    <>
      {item.icone()}
      {item.rotulo}
    </>
  );

  if (!item.areas) {
    return (
      <NavLink to={item.para} end className={className}>
        {conteudo}
      </NavLink>
    );
  }

  const ativo = item.areas.some((area) => pathname === area || pathname.startsWith(`${area}/`));
  return (
    <Link to={item.para} className={className} aria-current={ativo ? 'page' : undefined}>
      {conteudo}
    </Link>
  );
}

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
              <ItemMenu item={item} className={estilos.itemLateral} />
            </li>
          ))}
        </ul>
        <button type="button" className={[estilos.itemLateral, estilos.sairLateral].join(' ')} onClick={() => void sair()}>
          <IconeSair />
          Sair
        </button>
      </nav>

      <main id="conteudo" className={estilos.conteudo} tabIndex={-1}>
        {children}
      </main>

      <nav className={estilos.barraInferior} aria-label="Menu principal (celular)">
        <ul className={estilos.listaInferior}>
          {itens.filter((item) => !item.somenteDesktop).map((item) => (
            <li key={item.rotulo}>
              <ItemMenu item={item} className={estilos.itemInferior} />
            </li>
          ))}
        </ul>
      </nav>
    </div>
  );
}
