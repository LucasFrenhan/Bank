import { useEffect } from 'react';
import { BrowserRouter, Route, Routes, useLocation } from 'react-router';
import { Layout } from './design/componentes/Layout';
import { Comprovante } from './paginas/Comprovante';
import { ConfirmarTransferencia } from './paginas/ConfirmarTransferencia';
import { CriarConta } from './paginas/CriarConta';
import { Transferir } from './paginas/Transferir';
import { Entrar } from './paginas/Entrar';
import { Extrato } from './paginas/Extrato';
import { Inicio } from './paginas/Inicio';
import { NaoEncontrada } from './paginas/NaoEncontrada';
import { Perfil } from './paginas/Perfil';
import { ProvedorSessao } from './sessao/ContextoSessao';
import { RotaProtegida, RotaPublica } from './sessao/Rotas';
import { registrarNavegacao } from './telemetria/ponte';

/** Rotas do contrato (contracts/frontend.md). */
export function Rotas() {
  const local = useLocation();

  useEffect(() => {
    registrarNavegacao(local.pathname);
  }, [local.pathname]);

  return (
    <Routes>
      <Route path="/entrar" element={<RotaPublica><Entrar /></RotaPublica>} />
      <Route path="/criar-conta" element={<RotaPublica><CriarConta /></RotaPublica>} />
      <Route path="/" element={<RotaProtegida><Layout><Inicio /></Layout></RotaProtegida>} />
      <Route path="/perfil" element={<RotaProtegida><Layout><Perfil /></Layout></RotaProtegida>} />
      <Route path="/extrato" element={<RotaProtegida><Layout><Extrato /></Layout></RotaProtegida>} />
      <Route path="/transferir" element={<RotaProtegida><Transferir /></RotaProtegida>} />
      <Route path="/transferir/confirmar" element={<RotaProtegida><ConfirmarTransferencia /></RotaProtegida>} />
      <Route path="/transferencias/:transferenciaId" element={<RotaProtegida><Comprovante /></RotaProtegida>} />
      <Route path="*" element={<NaoEncontrada />} />
    </Routes>
  );
}

export function App() {
  return (
    <BrowserRouter>
      <ProvedorSessao>
        <Rotas />
      </ProvedorSessao>
    </BrowserRouter>
  );
}
