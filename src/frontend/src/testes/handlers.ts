import { http, HttpResponse } from 'msw';
import type { TipoConta } from '../sessao/claims';

/** Respostas simuladas da API da spec 003 para testes de componentes (research R3). */

export const TRACE_ID = '4bf92f3577b34da6a3ce929d0e0e4736';

export function problema(codigo: string, status: number, extra: object = {}) {
  return HttpResponse.json(
    { type: `urn:bank:problema:${codigo}`, title: 'Título técnico', status, detail: 'Detalhe técnico', traceId: TRACE_ID, ...extra },
    { status, headers: { 'Content-Type': 'application/problem+json' } },
  );
}

function base64Url(valor: object): string {
  return btoa(String.fromCharCode(...new TextEncoder().encode(JSON.stringify(valor))))
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/, '');
}

/** JWT falso (assinatura irrelevante para o frontend) com as claims do contrato. */
export function tokenFalso(tipoConta: TipoConta = 'PessoaFisica', email = 'lucas@exemplo.com.br'): string {
  return [
    base64Url({ alg: 'HS256', typ: 'JWT' }),
    base64Url({ sub: 'usuario-1', email, tipo_conta: tipoConta, carteira_id: 'carteira-1' }),
    'assinatura',
  ].join('.');
}

export function carteira(tipoConta: TipoConta = 'PessoaFisica') {
  return {
    carteiraId: 'carteira-1',
    usuarioId: 'usuario-1',
    nomeTitular: tipoConta === 'PessoaFisica' ? 'Lucas Silva' : 'Empresa Exemplo Ltda',
    tipoConta,
    saldo: tipoConta === 'PessoaFisica' ? 10.0 : 0.0,
    moeda: 'BRL',
    atualizadoEm: '2026-11-17T17:32:00Z',
  };
}

export function titular(tipoConta: TipoConta = 'PessoaFisica') {
  return {
    usuarioId: 'usuario-1',
    nomeCompleto: tipoConta === 'PessoaFisica' ? 'Lucas Silva' : 'Empresa Exemplo Ltda',
    email: tipoConta === 'PessoaFisica' ? 'lucas@exemplo.com.br' : 'contato@exemplo.com.br',
    documento: tipoConta === 'PessoaFisica' ? '52998224725' : '12ABC34501DE35',
    tipoConta,
    criadoEm: '2026-11-09T12:00:00Z',
  };
}

export const handlersPadrao = [
  http.post('*/api/v1/autenticacao/login', () =>
    HttpResponse.json({ tokenAcesso: tokenFalso(), tipoToken: 'Bearer', expiraEmSegundos: 3600 }),
  ),
  http.post('*/api/v1/autenticacao/registrar', () =>
    HttpResponse.json(
      {
        usuarioId: 'usuario-1',
        carteiraId: 'carteira-1',
        nomeCompleto: 'Lucas Silva',
        email: 'lucas@exemplo.com.br',
        documento: '52998224725',
        tipoConta: 'PessoaFisica',
        saldoInicial: 10.0,
        moeda: 'BRL',
      },
      { status: 201 },
    ),
  ),
  http.get('*/api/v1/carteiras/minha', () => HttpResponse.json(carteira())),
  http.get('*/api/v1/usuarios/me', () => HttpResponse.json(titular())),
];
