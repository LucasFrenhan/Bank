import { useEffect, useState } from 'react';
import { Botao } from '../design/componentes/Botao';
import { AcoesConfirmacao, Confirmacao } from '../design/componentes/Confirmacao';
import { useSessao } from './ContextoSessao';
import estilos from './AvisoExpiracao.module.css';

/** Intervalo dos anúncios do contador a leitores de tela (contracts/frontend-sessao.md). */
const ANUNCIO_A_CADA_S = 15;

/**
 * Aviso antes do fim da sessão (specs/016-sessao-refresh-token, FR-016b; contracts/frontend-sessao.md): por
 * inatividade, com "Continuar conectado" (renova) e "Sair"; pelo limite absoluto, só informa ("Entendi"). Aparece
 * em todas as abas; uma renovação em qualquer aba muda os prazos e fecha o aviso nas outras. No fim do prazo, a
 * sessão expira.
 */
export function AvisoExpiracao() {
  const { sessao, renovar, sair, expirar } = useSessao();
  const [agora, definirAgora] = useState(() => Date.now());
  const [absolutoDispensado, definirAbsolutoDispensado] = useState(false);
  const [renovando, definirRenovando] = useState(false);

  const fim = sessao ? Math.min(sessao.inatividadeAte, sessao.absolutoAte) : 0;
  const inicioAviso = sessao ? fim - sessao.antecedenciaAvisoMs : 0;

  // Acorda exatamente no início do aviso e no fim do prazo.
  useEffect(() => {
    if (!sessao) return undefined;
    const atualizar = () => definirAgora(Date.now());
    const temporizadores = [inicioAviso, fim].map((instante) =>
      window.setTimeout(atualizar, Math.max(instante - Date.now(), 0)),
    );
    return () => temporizadores.forEach((temporizador) => window.clearTimeout(temporizador));
  }, [sessao, inicioAviso, fim]);

  // Durante o aviso, o contador anda de segundo em segundo.
  const emAviso = sessao !== null && agora >= inicioAviso && agora < fim;
  useEffect(() => {
    if (!emAviso) return undefined;
    const intervalo = window.setInterval(() => definirAgora(Date.now()), 1000);
    return () => window.clearInterval(intervalo);
  }, [emAviso]);

  useEffect(() => {
    if (sessao && agora >= fim) {
      expirar('expirada');
    }
  }, [sessao, agora, fim, expirar]);

  if (!sessao || agora < inicioAviso || agora >= fim) {
    return null;
  }

  const porLimiteAbsoluto = sessao.absolutoAte <= sessao.inatividadeAte;
  if (porLimiteAbsoluto && absolutoDispensado) {
    return null;
  }

  const segundos = Math.max(Math.ceil((fim - agora) / 1000), 0);
  const anunciado = Math.ceil(segundos / ANUNCIO_A_CADA_S) * ANUNCIO_A_CADA_S;

  const continuar = async () => {
    definirRenovando(true);
    const continua = await renovar();
    definirRenovando(false);
    if (!continua) expirar('expirada');
  };

  const titulo = porLimiteAbsoluto ? 'Sua sessão vai terminar' : 'Sua sessão vai expirar';
  const corpo = porLimiteAbsoluto
    ? `Por segurança, a sessão tem duração máxima. Você precisará entrar de novo em ${segundos} s.`
    : `Por segurança, você sairá em ${segundos} s se não houver atividade.`;

  return (
    <div className={estilos.fundo}>
      <div className={estilos.janela} role="alertdialog" aria-modal="true" aria-label={titulo}>
        <Confirmacao titulo={titulo} aoFechar={porLimiteAbsoluto ? () => definirAbsolutoDispensado(true) : () => void continuar()}>
          <p className={estilos.texto}>{corpo}</p>
          <p className={estilos.somenteLeitor} aria-live="polite">
            {porLimiteAbsoluto ? `A sessão termina em cerca de ${anunciado} segundos.` : `Você sairá em cerca de ${anunciado} segundos.`}
          </p>
          <AcoesConfirmacao>
            {porLimiteAbsoluto ? (
              <Botao onClick={() => definirAbsolutoDispensado(true)}>Entendi</Botao>
            ) : (
              <>
                <Botao onClick={() => void continuar()} carregando={renovando}>
                  Continuar conectado
                </Botao>
                <Botao variante="secundario" onClick={() => void sair()}>
                  Sair
                </Botao>
              </>
            )}
          </AcoesConfirmacao>
        </Confirmacao>
      </div>
    </div>
  );
}
