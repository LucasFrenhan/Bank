import estilos from './Filtros.module.css';

/** Botões de filtro (extrato, cobranças e recebíveis): um ativo por vez, indicado por `aria-pressed`. */
export function Filtros<V>({
  rotulo,
  opcoes,
  atual,
  aoEscolher,
}: {
  rotulo: string;
  opcoes: readonly { rotulo: string; valor: V }[];
  atual: V;
  aoEscolher: (valor: V) => void;
}) {
  return (
    <div className={[estilos.filtros, opcoes.length > 3 ? estilos.muitos : ''].join(' ')} role="group" aria-label={rotulo}>
      {opcoes.map((opcao) => (
        <button
          key={opcao.rotulo}
          type="button"
          className={estilos.filtro}
          aria-pressed={atual === opcao.valor}
          onClick={() => aoEscolher(opcao.valor)}
        >
          {opcao.rotulo}
        </button>
      ))}
    </div>
  );
}
