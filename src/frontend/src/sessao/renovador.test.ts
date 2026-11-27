import { NOME_TRAVA, renovarUmaVez } from './renovador';

/** Uma renovação por vez (specs/016-sessao-refresh-token, FR-015; research R9). */
describe('renovador', () => {
  const locksOriginal = Object.getOwnPropertyDescriptor(navigator, 'locks');

  afterEach(() => {
    if (locksOriginal) {
      Object.defineProperty(navigator, 'locks', locksOriginal);
    } else {
      Reflect.deleteProperty(navigator, 'locks');
    }
  });

  it('três chamadas concorrentes na aba geram uma única renovação', async () => {
    let execucoes = 0;
    const executar = async () => {
      execucoes++;
      await new Promise((resolve) => setTimeout(resolve, 10));
      return true;
    };

    const resultados = await Promise.all([renovarUmaVez(executar), renovarUmaVez(executar), renovarUmaVez(executar)]);

    expect(resultados).toEqual([true, true, true]);
    expect(execucoes).toBe(1);
  });

  it('depois de terminar, uma nova chamada renova de novo', async () => {
    let execucoes = 0;
    const executar = () => {
      execucoes++;
      return Promise.resolve(true);
    };

    await renovarUmaVez(executar);
    await renovarUmaVez(executar);

    expect(execucoes).toBe(2);
  });

  it('usa a trava entre abas quando a Web Locks API existe', async () => {
    const pedidos: string[] = [];
    Object.defineProperty(navigator, 'locks', {
      configurable: true,
      value: {
        request: (nome: string, funcao: () => Promise<boolean>) => {
          pedidos.push(nome);
          return funcao();
        },
      },
    });

    expect(await renovarUmaVez(() => Promise.resolve(true))).toBe(true);
    expect(pedidos).toEqual([NOME_TRAVA]);
  });

  it('sem Web Locks, funciona só com o voo único', async () => {
    Object.defineProperty(navigator, 'locks', { configurable: true, value: undefined });

    expect(await renovarUmaVez(() => Promise.resolve(false))).toBe(false);
  });

  it('uma falha propaga e libera a próxima renovação', async () => {
    await expect(renovarUmaVez(() => Promise.reject(new Error('rede')))).rejects.toThrow('rede');

    expect(await renovarUmaVez(() => Promise.resolve(true))).toBe(true);
  });
});
