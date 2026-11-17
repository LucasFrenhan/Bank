import { Botao } from '../design/componentes/Botao';
import { AvisoErro, Cartao, Carregando, Selo } from '../design/componentes/Feedback';
import { formatarData } from '../formatacao/data';
import { ocultarDocumento } from '../formatacao/mascaras';
import { useSessao, useSessaoAtiva } from '../sessao/ContextoSessao';
import { NOME_TIPO_CONTA, PERMISSOES_POR_PERFIL } from '../sessao/permissoes';
import estilos from './Autenticado.module.css';
import { useTitulo } from './comum';
import { useTitular } from './useTitular';

/**
 * Perfil (US4; FR-017): dados do titular vindos de `GET /api/v1/usuarios/me` (spec 005), com o
 * documento mascarado para exibição, tipo de conta não alterável, permissões e "Sair".
 */
export function Perfil() {
  useTitulo('Perfil');
  const sessao = useSessaoAtiva();
  const { sair } = useSessao();
  const titular = useTitular();
  const tipo = NOME_TIPO_CONTA[sessao.tipoConta];
  const rotuloDocumento = sessao.tipoConta === 'PessoaFisica' ? 'CPF' : 'CNPJ';

  if (titular.estado === 'carregando') {
    return <Carregando texto="Carregando seu perfil…" />;
  }

  if (titular.estado === 'erro') {
    return (
      <div className={estilos.pagina}>
        <h1>Perfil</h1>
        <AvisoErro erro={titular.erro} aoTentarDeNovo={titular.recarregar} />
      </div>
    );
  }

  const { nomeCompleto, email, documento, criadoEm } = titular.titular;

  return (
    <div className={estilos.pagina}>
      <h1>Perfil</h1>

      <Cartao>
        <div className={estilos.identidade}>
          <span className={estilos.avatar} aria-hidden="true">
            {iniciais(nomeCompleto)}
          </span>
          <div className={estilos.nomeIdentidade}>
            <h2>{nomeCompleto}</h2>
            <Selo>{tipo}</Selo>
          </div>
        </div>
        <dl className={estilos.dados} aria-label="Dados da conta">
          <div className={estilos.linhaDado}>
            <dt>E-mail</dt>
            <dd>{email}</dd>
          </div>
          <div className={estilos.linhaDado}>
            <dt>{rotuloDocumento}</dt>
            <dd>
              <span aria-hidden="true">{ocultarDocumento(documento)}</span>
              <span className="apenas-leitor-de-tela">{rotuloDocumento} parcialmente oculto</span>
            </dd>
          </div>
          <div className={estilos.linhaDado}>
            <dt>Tipo de conta</dt>
            <dd>{tipo} · não pode ser alterado</dd>
          </div>
          <div className={estilos.linhaDado}>
            <dt>Cliente desde</dt>
            <dd>{formatarData(criadoEm)}</dd>
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

function iniciais(nome: string): string {
  return nome
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((parte) => parte[0]?.toUpperCase())
    .join('');
}
