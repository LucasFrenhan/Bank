import { contaBancaria } from '../testes/handlers';
import {
  contaComDigito,
  lerConta,
  lerDigitos,
  nomeBanco,
  nomeTipoContaBancaria,
  textoDadosDaConta,
} from './contaBancaria';

describe('conta bancária (specs/013; FR-001 a FR-006)', () => {
  it.each([
    ['ContaCorrente', 'Conta corrente'],
    ['ContaPoupanca', 'Conta poupança'],
    ['ContaSalario', 'Conta'],
    ['', 'Conta'],
    ['toString', 'Conta'],
  ])('tipo %s → "%s"', (tipo, nome) => {
    expect(nomeTipoContaBancaria(tipo)).toBe(nome);
  });

  it('banco com código e nome vindos da API', () => {
    expect(nomeBanco(contaBancaria())).toBe('001 – One Bank');
    expect(nomeBanco(contaBancaria({ banco: { codigo: '002', nome: 'Banco Aurora' } }))).toBe('002 – Banco Aurora');
  });

  it('conta com dígito preserva zeros, inclusive o dígito 0', () => {
    expect(contaComDigito(contaBancaria())).toBe('00000042-0');
    expect(contaComDigito(contaBancaria({ numero: '99999999', digito: '6' }))).toBe('99999999-6');
  });

  it('leitura dígito a dígito, com o dígito verificador identificado', () => {
    expect(lerDigitos('0001')).toBe('0 0 0 1');
    expect(lerConta(contaBancaria())).toBe('0 0 0 0 0 0 4 2, dígito 0');
  });

  it.each([
    ['ContaCorrente', 'Conta corrente 00000042-0'],
    ['ContaPoupanca', 'Conta poupança 00000042-0'],
    ['Outro', 'Conta 00000042-0'],
  ])('texto copiado (%s): exatamente três linhas', (tipo, linhaConta) => {
    expect(textoDadosDaConta(contaBancaria({ tipo }))).toBe(`Banco 001 – One Bank\nAgência 0001\n${linhaConta}`);
  });
});
