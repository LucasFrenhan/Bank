import type { TipoConta } from './claims';

/**
 * Permissões por perfil para exibição no perfil (espelho da RN-06 da spec 003; quem decide é a
 * API). Mantido num único mapa.
 */
export const PERMISSOES_POR_PERFIL: Record<TipoConta, string[]> = {
  PessoaFisica: ['Enviar transferências para PF e PJ', 'Receber transferências', 'Pagar cobranças de cartão de crédito'],
  PessoaJuridica: ['Receber transferências', 'Gerar cobranças de cartão de crédito'],
};

export const NOME_TIPO_CONTA: Record<TipoConta, string> = {
  PessoaFisica: 'Pessoa Física',
  PessoaJuridica: 'Pessoa Jurídica',
};
