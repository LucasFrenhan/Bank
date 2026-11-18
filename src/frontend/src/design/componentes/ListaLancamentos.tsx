import { Link } from 'react-router';
import estilos from './componentes.module.css';
import { IconeEntrada, IconeSaida } from './Icones';

export interface LinhaLancamento {
  id: string;
  entrada: boolean;
  nome: string;
  /** Linha secundária (direção, descrição, data). */
  detalhe: string;
  /** Valor já com sinal (`+ R$ 120,00`). */
  valor: string;
  /** Comprovante, quando disponível. */
  para?: string | undefined;
}

/**
 * Lista de lançamentos do protótipo (início e extrato mobile): ícone de direção, nome, detalhe
 * e valor com sinal. Entrada e saída não dependem só da cor (FR-015; WCAG 1.4.1).
 */
export function ListaLancamentos({ linhas, rotulo }: { linhas: LinhaLancamento[]; rotulo?: string }) {
  return (
    <ul className={estilos.lancamentos} aria-label={rotulo}>
      {linhas.map((linha) => (
        <li key={linha.id}>
          <ItemLancamento linha={linha} />
        </li>
      ))}
    </ul>
  );
}

function ItemLancamento({ linha }: { linha: LinhaLancamento }) {
  const conteudo = (
    <>
      <span className={[estilos.iconeDirecao, linha.entrada ? estilos.iconeEntrada : ''].join(' ')}>
        {linha.entrada ? <IconeEntrada /> : <IconeSaida />}
      </span>
      <span className={estilos.textoLancamento}>
        <span className={estilos.nomeLancamento}>{linha.nome}</span>
        <span className={estilos.detalheLancamento}>{linha.detalhe}</span>
      </span>
      <span className={[estilos.valorLancamento, linha.entrada ? estilos.valorEntrada : ''].join(' ')}>{linha.valor}</span>
    </>
  );

  return linha.para ? (
    <Link to={linha.para} className={estilos.lancamento}>
      {conteudo}
    </Link>
  ) : (
    <div className={estilos.lancamento}>{conteudo}</div>
  );
}
