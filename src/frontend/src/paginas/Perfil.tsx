import { Botao } from '../design/componentes/Botao';
import { AvisoErro, Cartao, Carregando, Selo } from '../design/componentes/Feedback';
import { useSessao, useSessaoAtiva } from '../sessao/ContextoSessao';
import { NOME_TIPO_CONTA, PERMISSOES_POR_PERFIL } from '../sessao/permissoes';
import estilos from './Autenticado.module.css';
import { useTitulo } from './comum';
import { useCarteira } from './useCarteira';

/**
 * Perfil (US4; FR-017). Documento e "Cliente desde" chegam com a issue #18
 * (`GET /api/v1/usuarios/me`); até lá não se exibe nenhum dado fictício.
 */
export function Perfil() {
  useTitulo('Perfil');
  const sessao = useSessaoAtiva();
  const { sair } = useSessao();
  const carteira = useCarteira();
  const tipo = NOME_TIPO_CONTA[sessao.tipoConta];

  if (carteira.estado === 'carregando') {
    return <Carregando texto="Carregando seu perfil…" />;
  }

  if (carteira.estado === 'erro') {
    return (
      <div className={estilos.pagina}>
        <h1>Perfil</h1>
        <AvisoErro erro={carteira.erro} aoTentarDeNovo={carteira.recarregar} />
      </div>
    );
  }

  const nome = carteira.carteira.nomeTitular;
  const iniciais = nome
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((parte) => parte[0]?.toUpperCase())
    .join('');

  return (
    <div className={estilos.pagina}>
      <h1>Perfil</h1>

      <Cartao>
        <div className={estilos.identidade}>
          <span className={estilos.avatar} aria-hidden="true">
            {iniciais}
          </span>
          <div className={estilos.nomeIdentidade}>
            <h2>{nome}</h2>
            <Selo>{tipo}</Selo>
          </div>
        </div>
        <dl className={estilos.dados} aria-label="Dados da conta">
          <div className={estilos.linhaDado}>
            <dt>E-mail</dt>
            <dd>{sessao.email}</dd>
          </div>
          <div className={estilos.linhaDado}>
            <dt>Tipo de conta</dt>
            <dd>{tipo} · não pode ser alterado</dd>
          </div>
        </dl>
      </Cartao>

      <Cartao>
        <div className={estilos.cabecalhoSecao}>
          <h2>O que sua conta pode fazer</h2>
        </div>
        <ul className={estilos.permissoes}>
          {PERMISSOES_POR_PERFIL[sessao.tipoConta].map((permissao) => (
            <li key={permissao}>{permissao}</li>
          ))}
        </ul>
      </Cartao>

      <div>
        <Botao variante="secundario" onClick={sair}>
          Sair
        </Botao>
      </div>
    </div>
  );
}
