import type { ReactNode } from 'react';
import { Link } from 'react-router';
import type { MensagemErro } from '../../api/erros';
import estilos from './componentes.module.css';

type VarianteAviso = 'erro' | 'atencao' | 'info' | 'sucesso';

const classeAviso: Record<VarianteAviso, string | undefined> = {
  erro: estilos.avisoErro,
  atencao: estilos.avisoAtencao,
  info: estilos.avisoInfo,
  sucesso: estilos.avisoSucesso,
};

/** Mensagem de feedback; erros são anunciados imediatamente por leitores de tela. */
export function Aviso({ variante, titulo, children }: { variante: VarianteAviso; titulo?: string; children?: ReactNode }) {
  return (
    <div className={[estilos.aviso, classeAviso[variante]].join(' ')} role={variante === 'erro' ? 'alert' : 'status'}>
      <div>
        {titulo ? <p className={estilos.avisoTitulo}>{titulo}</p> : null}
        {children}
      </div>
    </div>
  );
}

/** Erro da API traduzido (FR-018/FR-019): mensagem, código de suporte e ação sugerida. */
export function AvisoErro({ erro, aoTentarDeNovo }: { erro: MensagemErro; aoTentarDeNovo?: () => void }) {
  return (
    <Aviso variante="erro" titulo={erro.mensagem}>
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
