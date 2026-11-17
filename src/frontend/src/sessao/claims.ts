/**
 * Leitura das claims do JWT apenas para exibição e roteamento (research R5). A autorização
 * continua sendo feita pela API; aqui não há validação de assinatura.
 */

export type TipoConta = 'PessoaFisica' | 'PessoaJuridica';

export interface ClaimsSessao {
  usuarioId: string;
  email: string;
  tipoConta: TipoConta;
  carteiraId: string;
}

export function decodificarClaims(token: string): ClaimsSessao | null {
  const payload = token.split('.')[1];
  if (!payload) {
    return null;
  }

  try {
    const json = JSON.parse(decodificarBase64Url(payload)) as Record<string, unknown>;
    const { sub, email, tipo_conta: tipoConta, carteira_id: carteiraId } = json;

    if (
      typeof sub !== 'string' ||
      typeof email !== 'string' ||
      typeof carteiraId !== 'string' ||
      (tipoConta !== 'PessoaFisica' && tipoConta !== 'PessoaJuridica')
    ) {
      return null;
    }

    return { usuarioId: sub, email, tipoConta, carteiraId };
  } catch {
    return null;
  }
}

function decodificarBase64Url(valor: string): string {
  const base64 = valor.replace(/-/g, '+').replace(/_/g, '/').padEnd(Math.ceil(valor.length / 4) * 4, '=');
  const bytes = Uint8Array.from(atob(base64), (c) => c.charCodeAt(0));
  return new TextDecoder().decode(bytes);
}
