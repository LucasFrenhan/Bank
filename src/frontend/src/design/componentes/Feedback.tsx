import type { ReactNode } from 'react';
import { Link } from 'react-router';
import type { MensagemErro } from '../../api/erros';
import estilos from './componentes.module.css';
import { IconeAlerta, IconeCadeado, IconeErro, IconeInformacao } from './Icones';

export type VarianteAviso = 'erro' | 'atencao' | 'info' | 'sucesso' | 'neutro';

const classeAviso: Record<VarianteAviso, string | undefined> = {
  erro: estilos.avisoErro,
  atencao: estilos.avisoAtencao,
  info: estilos.avisoInfo,
  sucesso: estilos.avisoSucesso,
  neutro: estilos.avisoNeutro,
};

const iconeAviso: Partial<Record<VarianteAviso, () => ReactNode>> = {
  erro: IconeErro,
  atencao: IconeAlerta,
  info: IconeInformacao,
  neutro: IconeCadeado,
};

/**
 * Mensagem de feedback no estilo do protótipo (estados de erro e feedback). Erros e avisos de
 * regra de negócio são anunciados imediatamente por leitores de tela (`alert`); os demais, como
 * status.
 */
export function Aviso({
  variante,
  titulo,
  children,
  id,
}: {
  variante: VarianteAviso;
  titulo?: string;
  children?: ReactNode;
  id?: string;
}) {
  const Icone = iconeAviso[variante];

  return (
    <div
      id={id}
      className={[estilos.aviso, classeAviso[variante]].join(' ')}
      role={variante === 'erro' || variante === 'atencao' ? 'alert' : 'status'}
      tabIndex={-1}
    >
      {Icone ? (
        <span className={estilos.avisoIcone}>
          <Icone />
        </span>
      ) : null}
      <div>
        {titulo ? <p className={estilos.avisoTitulo}>{titulo}</p> : null}
        {children}
      </div>
    </div>
  );
}

const variantePorErro: Record<string, VarianteAviso> = {
  atencao: 'atencao',
  neutro: 'neutro',
  info: 'info',
};

/** Erro da API traduzido (FR-018/FR-019): mensagem, código de suporte e ação sugerida. */
export function AvisoErro({ erro, aoTentarDeNovo, id }: { erro: MensagemErro; aoTentarDeNovo?: () => void; id?: string }) {
  return (
    <Aviso variante={variantePorErro[erro.variante ?? ''] ?? 'erro'} titulo={erro.mensagem} {...(id ? { id } : {})}>
      {erro.detalhe ? <p>{erro.detalhe}</p> : null}
      {erro.traceId && erro.codigo === 'inesperado' ? (
        <p>
          Código para o suporte: <span className={estilos.codigoSuporte}>{erro.traceId}</span>
        </p>
      ) : null}
      {erro.acao === 'ir-para-login' ? (
        <p>
          <Link to="/entrar">Ir para o login</Link>
        </p>
      ) : null}
      {erro.acao === 'ir-para-inicio' ? (
        <p>
          <Link to="/">Voltar ao início</Link>
        </p>
      ) : null}
      {erro.acao === 'tentar-de-novo' && aoTentarDeNovo ? (
        <p>
          <button type="button" className={[estilos.botao, estilos.secundario].join(' ')} onClick={aoTentarDeNovo}>
            Tentar de novo
          </button>
        </p>
      ) : null}
    </Aviso>
  );
}

export function Carregando({ texto = 'Carregando…' }: { texto?: string }) {
  return (
    <div className={estilos.carregando} role="status" aria-live="polite">
      <span className={estilos.giro} aria-hidden="true" />
      {texto}
    </div>
  );
}

export function EstadoVazio({ titulo, descricao }: { titulo: string; descricao?: string }) {
  return (
    <div className={estilos.vazio}>
      <p className={estilos.vazioTitulo}>{titulo}</p>
      {descricao ? <p>{descricao}</p> : null}
    </div>
  );
}

export function Cartao({ children, className, rotulo }: { children: ReactNode; className?: string; rotulo?: string }) {
  return (
    <section className={[estilos.cartao, className ?? ''].join(' ')} aria-label={rotulo}>
      {children}
    </section>
  );
}

export function Selo({ children, escuro }: { children: ReactNode; escuro?: boolean }) {
  return <span className={[estilos.selo, escuro ? estilos.seloEscuro : ''].join(' ')}>{children}</span>;
}

export function Marca({ para = '/' }: { para?: string }) {
  return (
    <Link to={para} className={estilos.marca}>
      <span className={estilos.marcaIcone} aria-hidden="true">
        B
      </span>
      <span>Bank</span>
    </Link>
  );
}
