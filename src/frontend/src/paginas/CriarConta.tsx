import { useRef, useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router';
import { api } from '../api/cliente';
import type { MensagemErro } from '../api/erros';
import { Botao } from '../design/componentes/Botao';
import { CampoDocumento, CampoSenha, CampoTexto } from '../design/componentes/Campos';
import { Aviso, AvisoErro, Cartao, Marca } from '../design/componentes/Feedback';
import type { TipoConta } from '../sessao/claims';
import { useSessao } from '../sessao/ContextoSessao';
import { useTitulo } from './comum';
import estilos from './Publico.module.css';

type Campo = 'nomeCompleto' | 'documento' | 'email' | 'senha';
type Erros = Partial<Record<Campo, string[]>>;

const ORDEM_CAMPOS: Campo[] = ['nomeCompleto', 'documento', 'email', 'senha'];

const TIPOS: { valor: TipoConta; titulo: string; descricao: string; saldo: string }[] = [
  {
    valor: 'PessoaFisica',
    titulo: 'Pessoa Física',
    descricao: 'Conta com CPF. Envia e recebe transferências.',
    saldo: 'Contas Pessoa Física começam com R$ 10,00 de saldo de demonstração.',
  },
  {
    valor: 'PessoaJuridica',
    titulo: 'Pessoa Jurídica',
    descricao: 'Conta com CNPJ. Recebe transferências e gera cobranças.',
    saldo: 'Contas Pessoa Jurídica começam com R$ 0,00 de saldo.',
  },
];

/** Critérios da política de senha da API (spec 003, FR-009) para o indicador (FR-007). */
export function avaliarSenha(senha: string): { atendidos: number; rotulo: string } {
  const criterios = [
    senha.length >= 8 && senha.length <= 128,
    /[A-Z]/.test(senha) && /[a-z]/.test(senha),
    /\d/.test(senha),
    /[^A-Za-z0-9]/.test(senha),
  ];
  const atendidos = criterios.filter(Boolean).length;
  const rotulos = ['Senha muito fraca.', 'Senha fraca.', 'Senha razoável.', 'Senha quase boa.', 'Senha boa.'];
  return { atendidos, rotulo: rotulos[atendidos] ?? '' };
}

/** Cadastro PF/PJ (US1; FR-004 a FR-008). */
export function CriarConta() {
  useTitulo('Criar conta');
  const { entrar } = useSessao();
  const navegar = useNavigate();
  const [tipoConta, definirTipoConta] = useState<TipoConta>('PessoaFisica');
  const [valores, definirValores] = useState<Record<Campo, string>>({ nomeCompleto: '', documento: '', email: '', senha: '' });
  const [erros, definirErros] = useState<Erros>({});
  const [erroGeral, definirErroGeral] = useState<MensagemErro | null>(null);
  const [errosSemCampo, definirErrosSemCampo] = useState<string[]>([]);
  const [enviando, definirEnviando] = useState(false);
  const emAndamento = useRef(false);
  const refNome = useRef<HTMLInputElement>(null);
  const refDocumento = useRef<HTMLInputElement>(null);
  const refEmail = useRef<HTMLInputElement>(null);
  const refSenha = useRef<HTMLInputElement>(null);

  const pf = tipoConta === 'PessoaFisica';
  const tipoAtual = TIPOS.find((t) => t.valor === tipoConta)!;
  const forca = avaliarSenha(valores.senha);

  function alterar(campo: Campo, valor: string) {
    definirValores((atual) => ({ ...atual, [campo]: valor }));
  }

  function trocarTipo(novo: TipoConta) {
    if (novo === tipoConta) return;
    definirTipoConta(novo);
    alterar('documento', ''); // o formato muda com o tipo
    definirErros((atual) => ({ ...atual, documento: undefined }));
  }

  function focarPrimeiroErro(lista: Erros) {
    const primeiro = ORDEM_CAMPOS.find((c) => lista[c]?.length);
    const refs = { nomeCompleto: refNome, documento: refDocumento, email: refEmail, senha: refSenha };
    if (primeiro) refs[primeiro].current?.focus();
  }

  function validarLocalmente(): Erros {
    const locais: Erros = {};
    const nome = valores.nomeCompleto.trim();
    if (!nome) locais.nomeCompleto = [pf ? 'Informe o nome completo.' : 'Informe a razão social.'];
    else if (nome.length > 150) locais.nomeCompleto = ['Use no máximo 150 caracteres.'];

    const documento = valores.documento.replace(/[^0-9A-Z]/gi, '');
    if (!documento) locais.documento = [pf ? 'Informe o CPF.' : 'Informe o CNPJ.'];
    else if (documento.length !== (pf ? 11 : 14)) locais.documento = [pf ? 'O CPF tem 11 dígitos.' : 'O CNPJ tem 14 caracteres.'];

    if (!valores.email.trim()) locais.email = ['Informe o e-mail.'];
    else if (!/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(valores.email.trim())) locais.email = ['Informe um e-mail válido.'];

    if (!valores.senha) locais.senha = ['Crie uma senha.'];
    return locais;
  }

  async function enviar(evento: FormEvent) {
    evento.preventDefault();
    if (emAndamento.current) return;

    definirErroGeral(null);
    definirErrosSemCampo([]);
    const locais = validarLocalmente();
    definirErros(locais);
    if (Object.keys(locais).length > 0) {
      focarPrimeiroErro(locais);
      return;
    }

    emAndamento.current = true;
    definirEnviando(true);
    const resultado = await api.registrar({ ...valores, tipoConta });

    if (!resultado.ok) {
      emAndamento.current = false;
      definirEnviando(false);
      const campos = resultado.erro.campos;
      if (campos) {
        const daApi: Erros = {};
        const outros: string[] = [];
        for (const [campo, mensagens] of Object.entries(campos)) {
          if ((ORDEM_CAMPOS as string[]).includes(campo)) daApi[campo as Campo] = mensagens;
          else outros.push(...mensagens);
        }
        definirErros(daApi);
        definirErrosSemCampo(outros);
        focarPrimeiroErro(daApi);
      } else {
        definirErroGeral(resultado.erro);
      }
      return;
    }

    // A API de cadastro não devolve token: entra com as mesmas credenciais (FR-008).
    const login = await entrar(valores.email, valores.senha);
    emAndamento.current = false;
    definirEnviando(false);
    void navegar(login.ok ? '/' : '/entrar', { replace: true });
  }

  return (
    <div>
      <header className={estilos.topo}>
        <Marca para="/entrar" />
        <p>
          Já tem conta? <Link to="/entrar">Entrar</Link>
        </p>
      </header>

      <main>
        <form className={estilos.criarConta} onSubmit={enviar} noValidate>
          <div>
            <h1>Criar conta</h1>
            <p className={estilos.subtitulo}>Sua carteira digital é criada junto com a conta.</p>
          </div>

          {erroGeral ? <AvisoErro erro={erroGeral} /> : null}
          {errosSemCampo.length > 0 ? (
            <Aviso variante="erro" titulo="Confira os dados informados.">
              {errosSemCampo.map((m) => (
                <p key={m}>{m}</p>
              ))}
            </Aviso>
          ) : null}

          <fieldset className={estilos.grupoTipo}>
            <legend className={estilos.legenda}>Tipo de conta</legend>
            <div className={estilos.opcoesTipo}>
              {TIPOS.map((tipo) => (
                <label key={tipo.valor} className={estilos.opcaoTipo}>
                  <input
                    type="radio"
                    name="tipoConta"
                    value={tipo.valor}
                    checked={tipoConta === tipo.valor}
                    onChange={() => trocarTipo(tipo.valor)}
                  />
                  <span className={estilos.opcaoTitulo}>{tipo.titulo}</span>
                  <span className={estilos.opcaoDescricao}>{tipo.descricao}</span>
                </label>
              ))}
            </div>
            <p className={estilos.notaTipo}>O tipo de conta não pode ser alterado depois do cadastro.</p>
          </fieldset>

          <Cartao>
            <div className={estilos.camposCadastro}>
              <CampoTexto
                ref={refNome}
                className={estilos.campoInteiro}
                rotulo={pf ? 'Nome completo' : 'Razão social'}
                autoComplete={pf ? 'name' : 'organization'}
                value={valores.nomeCompleto}
                onChange={(e) => alterar('nomeCompleto', e.target.value)}
                erros={erros.nomeCompleto}
                maxLength={150}
                required
              />
              <CampoDocumento
                ref={refDocumento}
                tipoConta={tipoConta}
                valor={valores.documento}
                aoAlterar={(v) => alterar('documento', v)}
                erros={erros.documento}
                required
              />
              <CampoTexto
                ref={refEmail}
                rotulo="E-mail"
                type="email"
                inputMode="email"
                autoComplete="email"
                value={valores.email}
                onChange={(e) => alterar('email', e.target.value)}
                erros={erros.email}
                required
              />
              <CampoSenha
                ref={refSenha}
                className={estilos.campoInteiro}
                rotulo="Senha"
                autoComplete="new-password"
                value={valores.senha}
                onChange={(e) => alterar('senha', e.target.value)}
                erros={erros.senha}
                required
                dica={
                  <>
                    <span className={estilos.forca} aria-hidden="true">
                      {[0, 1, 2, 3].map((i) => (
                        <span key={i} className={[estilos.segmento, i < forca.atendidos ? estilos.segmentoAtivo : ''].join(' ')} />
                      ))}
                    </span>
                    <span aria-live="polite">
                      {valores.senha ? `${forca.rotulo} ` : ''}Use 8 ou mais caracteres, com letras maiúsculas e minúsculas, números e
                      símbolos.
                    </span>
                  </>
                }
              />
            </div>
          </Cartao>

          <Aviso variante="info">
            <p>{tipoAtual.saldo}</p>
          </Aviso>

          <Botao type="submit" largo carregando={enviando}>
            Criar conta
          </Botao>
        </form>
      </main>
    </div>
  );
}
