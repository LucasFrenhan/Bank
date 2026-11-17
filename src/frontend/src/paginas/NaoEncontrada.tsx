import { Link } from 'react-router';
import { Marca } from '../design/componentes/Feedback';
import { useTitulo } from './comum';
import estilos from './Publico.module.css';

export function NaoEncontrada() {
  useTitulo('Página não encontrada');

  return (
    <main className={estilos.naoEncontrada}>
      <Marca />
      <h1>Página não encontrada</h1>
      <p>O endereço acessado não existe no Bank.</p>
      <Link to="/">Ir para o início</Link>
    </main>
  );
}
