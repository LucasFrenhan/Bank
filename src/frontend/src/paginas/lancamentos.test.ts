import type { ItemExtrato } from '../api/cliente';
import { itemExtrato, ITEM_ABERTURA } from '../testes/handlers';
import { ehTransferencia, linkComprovante, nomeContraparte } from './lancamentos';

function lancamento(tipo: string, extra: object = {}): ItemExtrato {
  return itemExtrato(1, { tipo, transferenciaId: null, contraparte: null, ...extra });
}

describe('rótulos dos lançamentos (extrato 1.3.0, specs/014)', () => {
  it.each([
    ['PagamentoFatura', 'Pagamento de fatura'],
    ['LiquidacaoRecebivel', 'Recebimento de cobrança'],
    ['AntecipacaoRecebivel', 'Antecipação de recebível'],
    ['TipoFuturo', 'Lançamento'],
  ])('%s → "%s", sem comprovante', (tipo, nome) => {
    const item = lancamento(tipo);

    expect(nomeContraparte(item)).toBe(nome);
    expect(ehTransferencia(item)).toBe(false);
    expect(linkComprovante(item)).toBeUndefined();
  });

  it('"Saldo inicial" é só da abertura; transferência usa a contraparte', () => {
    expect(nomeContraparte(ITEM_ABERTURA as ItemExtrato)).toBe('Saldo inicial');
    expect(nomeContraparte(itemExtrato(2) as ItemExtrato)).toBe('Contraparte 2');
  });
});
