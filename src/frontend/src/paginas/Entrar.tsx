import { useRef, useState, type FormEvent } from 'react';
import { Link, useLocation, useNavigate, useSearchParams } from 'react-router';
import { MENSAGENS, type MensagemErro } from '../api/erros';
import { Botao } from '../design/componentes/Botao';
import { CampoSenha, CampoTexto } from '../design/componentes/Campos';
import { Aviso, AvisoErro, Marca } from '../design/componentes/Feedback';
import { useSessao } from '../sessao/ContextoSessao';
import { useTitulo } from './comum';
import estilos from './Publico.module.css';

interface Erros {
  email?: string[];
  senha?: string[];
}

/** Login (US2; FR-009, FR-010). Sem "Esqueci a senha": a API não oferece (Assumptions). */
export function Entrar() {
  useTitulo('Entrar');
  const { entrar } = useSessao();
  const navegar = useNavigate();
  const local = useLocation();
  const [parametros] = useSearchParams();
  const [email, definirEmail] = useState('');
  const [senha, definirSenha] = useState('');
  const [erros, definirErros] = useState<Erros>({});
  const [erroGeral, definirErroGeral] = useState<MensagemErro | null>(null);
  const [enviando, definirEnviando] = useState(false);
  const emAndamento = useRef(false);
  const refEmail = useRef<HTMLInputElement>(null);
  const refSenha = useRef<HTMLInputElement>(null);

  const retorno = (local.state as { retorno?: string } | null)?.retorno ?? '/';
  const avisoSessao = parametros.has('expirada')
    ? MENSAGENS.sessaoExpirada
    : parametros.has('conta')
      ? MENSAGENS.contaNaoEncontrada
      : null;

  async function enviar(evento: FormEvent) {
    evento.preventDefault();
    if (emAndamento.current) {
      return;
    }

    const locais: Erros = {};
    if (!email.trim()) locais.email = ['Informe o e-mail.'];
    if (!senha) locais.senha = ['Informe a senha.'];
    definirErros(locais);
    definirErroGeral(null);
    if (locais.email) {
      refEmail.current?.focus();
      return;
    }

    if (locais.senha) {
      refSenha.current?.focus();
      return;
    }

    emAndamento.current = true;
    definirEnviando(true);
    const resultado = await entrar(email, senha);
    emAndamento.current = false;
    definirEnviando(false);

    if (resultado.ok) {
      void navegar(retorno, { replace: true });
      return;
    }

    const { campos } = resultado.erro;
    if (campos && Object.keys(campos).length > 0) {
      definirErros({ email: campos.email, senha: campos.senha });
    } else {
      definirErroGeral(resultado.erro);
    }
  }

  return (
    <div className={estilos.telaEntrar}>
      <aside className={estilos.painel}>
        <Marca para="/entrar" />
        <div className={estilos.painelTexto}>
          <p className={estilos.painelTitulo}>Sua carteira digital, para você e para sua empresa.</p>
          <p className={estilos.painelDescricao}>
            Envie e receba transferências entre contas Pessoa Física e Pessoa Jurídica, com extrato sempre em dia.
          </p>
        </div>
        <p className={estilos.painelDemonstracao}>Ambiente de demonstração.</p>
      </aside>

      <main className={estilos.areaFormulario}>
        <div className={estilos.cabecalhoMobile}>
          <Marca para="/entrar" />
        </div>

        <form className={estilos.formularioEntrar} onSubmit={enviar} noValidate>
          <div>
            <h1>Entrar</h1>
            <p className={estilos.subtitulo}>Use o e-mail e a senha da sua conta.</p>
          </div>

          {avisoSessao ? <Aviso variante="atencao" titulo={avisoSessao} /> : null}
          {erroGeral ? <AvisoErro erro={erroGeral} /> : null}

          <CampoTexto
            ref={refEmail}
            rotulo="E-mail"
            type="email"
            autoComplete="email"
            inputMode="email"
            placeholder="voce@exemplo.com.br"
            value={email}
            onChange={(e) => definirEmail(e.target.value)}
            erros={erros.email}
            required
          />
          <CampoSenha
            ref={refSenha}
            rotulo="Senha"
            autoComplete="current-password"
            value={senha}
            onChange={(e) => definirSenha(e.target.value)}
            erros={erros.senha}
            required
          />

          <Botao type="submit" largo carregando={enviando}>
            Entrar
          </Botao>

          <p className={estilos.rodapeFormulario}>
            Ainda não tem conta? <Link to="/criar-conta">Criar conta</Link>
          </p>
          <p className={estilos.demonstracao}>Ambiente de demonstração.</p>
        </form>
      </main>
    </div>
  );
}
