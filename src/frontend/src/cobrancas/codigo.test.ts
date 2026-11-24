import { caminhoDaCobranca, extrairCodigo, formatarCodigo, linkDaCobranca, textoDeCompartilhamento } from './codigo';

/** Código e link da cobrança (specs/015, FR-009, FR-012, SC-003a). */
describe('código da cobrança', () => {
  it.each([
    '7K3F9QXM',
    '7K3F-9QXM',
    '7k3f-9qxm',
    ' 7k3f 9qxm ',
    'https://onebank.exemplo/c/7K3F-9QXM',
    'http://localhost:3000/c/7k3f9qxm/',
    'onebank.exemplo/c/7K3F-9QXM?x=1#y',
    '/c/7K3F-9QXM',
    'https://onebank.exemplo/c/7K3F%209QXM',
  ])('%s → código', (texto) => {
    expect(extrairCodigo(texto)).toBe('7K3F9QXM');
  });

  it.each(['', '   ', '7K3F9QX', '7K3F9QXMA', '0K3F-9QXM', '1K3F-9QXM', 'IK3F-9QXM', 'LK3F-9QXM', 'OK3F-9QXM', 'UK3F-9QXM', 'https://onebank.exemplo/', '/c/', '/c/%E0%A4%A', '7K3F_9QXM'])(
    '%s → inválido',
    (texto) => {
      expect(extrairCodigo(texto)).toBeNull();
    },
  );

  it('formato e link com até 12 caracteres depois do endereço do site', () => {
    expect(formatarCodigo('7k3f9qxm')).toBe('7K3F-9QXM');
    expect(formatarCodigo('7K3F-9QXM')).toBe('7K3F-9QXM');
    expect(linkDaCobranca('7K3F-9QXM')).toBe(`${window.location.origin}/c/7K3F-9QXM`);
    expect(caminhoDaCobranca('7K3F9QXM').length).toBeLessThanOrEqual(12);
  });

  it('texto de compartilhamento: lojista, valor, descrição e link', () => {
    const texto = textoDeCompartilhamento({ codigo: '7K3F-9QXM', lojista: { nome: 'Cafeteria' }, valor: 299.9, descricao: 'Kit' });

    expect(texto.replace(/\s/g, ' ')).toBe(
      `Cafeteria: R$ 299,90 — Kit. Pague com o cartão One Bank: ${window.location.origin}/c/7K3F-9QXM`,
    );
  });
});
