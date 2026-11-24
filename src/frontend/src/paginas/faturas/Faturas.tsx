import { useCallback } from 'react';
import { Link, useSearchParams } from 'react-router';
import { api, type Fatura } from '../../api/cliente';
import { SeloSituacao } from '../../design/componentes/SeloSituacao';
import { formatarData } from '../../formatacao/data';
import { formatarDiaCurto, tituloCompetencia } from '../../formatacao/dia';
import { formatarCentavos, saldoEmCentavos } from '../../formatacao/valor';
import { useTitulo } from '../comum';
import { ListaPaginada } from '../ListaPaginada';
import linhas from '../ListaPaginada.module.css';
import { SomentePerfil } from '../SomentePerfil';
import estilos from './Faturas.module.css';

/** Faturas da PF por mês, da mais nova para a mais antiga (specs/015-frontend-cartao-credito, US4; FR-019). */
export function Faturas() {
  useTitulo('Faturas');

  return (
    <SomentePerfil tipo="PessoaFisica" titulo="Faturas">
      <Conteudo />
    </SomentePerfil>
  );
}

function detalhe(fatura: Fatura): string {
  switch (fatura.situacao) {
    case 'Paga':
      return fatura.pagaEm ? `Paga em ${formatarData(fatura.pagaEm)}` : 'Paga';
    case 'Vencida':
      return `Venceu em ${formatarDiaCurto(fatura.vencimento)} · ${diasDeAtraso(fatura.diasAtraso)}`;
    default:
      return `Vence em ${formatarDiaCurto(fatura.vencimento)}`;
  }
}

export function diasDeAtraso(dias: number | string): string {
  return Number(dias) === 1 ? '1 dia de atraso' : `${Number(dias)} dias de atraso`;
}

function Conteudo() {
  const [parametros, definirParametros] = useSearchParams();
  const numero = Number(parametros.get('pagina'));
  const pagina = Number.isInteger(numero) && numero >= 1 ? numero : 1;
  const irParaPagina = useCallback(
    (p: number, substituir?: boolean) => definirParametros(p > 1 ? { pagina: String(p) } : {}, { replace: substituir }),
    [definirParametros],
  );

  return (
    <div className={estilos.pagina}>
      <div>
        <Link to="/cartao" className={estilos.link}>
          Voltar ao cartão
        </Link>
        <h1>Faturas</h1>
      </div>
      <ListaPaginada
          rotulo="Lista de faturas"
          chave="faturas"
          buscar={(p, tamanhoPagina) => api.listarFaturas({ pagina: p, tamanhoPagina })}
          idDe={(f) => f.competencia}
          pagina={pagina}
          irParaPagina={irParaPagina}
          carregando="Carregando faturas…"
          vazio={{ titulo: 'Nenhuma fatura ainda' }}
        >
          {(itens) => (
            <ul className={linhas.linhas}>
              {itens.map((f) => (
                <li key={f.competencia}>
                  <Link to={`/cartao/faturas/${f.competencia}`} className={linhas.linha}>
                    <span className={linhas.texto}>
                      <span className={linhas.nome}>{tituloCompetencia(f.competencia)}</span>
                      <span className={linhas.detalhe}>{detalhe(f)}</span>
                    </span>
                    <SeloSituacao situacao={f.situacao} />
                    <span className={linhas.valor}>{formatarCentavos(saldoEmCentavos(f.valorPago ?? f.valorAPagar ?? f.total))}</span>
                  </Link>
                </li>
              ))}
            </ul>
          )}
      </ListaPaginada>
    </div>
  );
}
