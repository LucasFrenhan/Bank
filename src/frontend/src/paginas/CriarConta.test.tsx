import { screen, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { problema } from '../testes/handlers';
import { renderizarApp, renderizarAppPronto } from '../testes/renderizar';
import { servidor } from '../testes/setup';
import { avaliarSenha } from './CriarConta';

async function preencherPf(usuario: ReturnType<typeof renderizarApp>['usuario'], dados: Partial<Record<string, string>> = {}) {
  await usuario.type(screen.getByLabelText('Nome completo'), dados.nome ?? 'Lucas Silva');
  await usuario.type(screen.getByLabelText('CPF'), dados.documento ?? '52998224725');
  await usuario.type(screen.getByLabelText('E-mail'), dados.email ?? 'lucas@exemplo.com.br');
  await usuario.type(screen.getByLabelText('Senha'), dados.senha ?? 'SenhaForte@123');
}

describe('Criar conta (US1; FR-004 a FR-008)', () => {
  it('alternar PF/PJ muda rótulos, máscara e saldo, e limpa o documento', async () => {
    const { usuario } = await renderizarAppPronto('/criar-conta');
    await usuario.type(screen.getByLabelText('CPF'), '52998224725');
    expect(screen.getByLabelText('CPF')).toHaveValue('529.982.247-25');
    expect(screen.getByText(/começam com R\$ 10,00/)).toBeInTheDocument();

    await usuario.click(screen.getByRole('radio', { name: /Pessoa Jurídica/ }));

    expect(screen.getByLabelText('Razão social')).toBeInTheDocument();
    expect(screen.getByLabelText('CNPJ')).toHaveValue('');
    expect(screen.getByText(/começam com R\$ 0,00/)).toBeInTheDocument();
    expect(screen.getByText('O tipo de conta não pode ser alterado depois do cadastro.')).toBeInTheDocument();
  });

  it('CNPJ alfanumérico digitado em minúsculas aparece em maiúsculas com máscara', async () => {
    const { usuario } = await renderizarAppPronto('/criar-conta');
    await usuario.click(screen.getByRole('radio', { name: /Pessoa Jurídica/ }));

    await usuario.type(screen.getByLabelText('CNPJ'), '12abc34501de35');

    expect(screen.getByLabelText('CNPJ')).toHaveValue('12.ABC.345/01DE-35');
  });

  it('valida obrigatoriedade localmente e foca o primeiro campo inválido', async () => {
    const { usuario } = await renderizarAppPronto('/criar-conta');

    await usuario.click(screen.getByRole('button', { name: 'Criar conta' }));

    expect(screen.getByText('Informe o nome completo.')).toBeInTheDocument();
    expect(screen.getByText('Informe o CPF.')).toBeInTheDocument();
    expect(screen.getByLabelText('Nome completo')).toHaveFocus();
    expect(screen.getByLabelText('Nome completo')).toHaveAttribute('aria-invalid', 'true');
  });

  it('mostra os erros da API em cada campo e os desconhecidos no topo, preservando o que foi digitado', async () => {
    servidor.use(
      http.post('*/api/v1/autenticacao/registrar', () =>
        problema('validacao', 400, { errors: { documento: ['CPF inválido.'], senha: ['A senha deve conter ao menos um dígito.'], extra: ['Campo inesperado.'] } }),
      ),
    );
    const { usuario } = await renderizarAppPronto('/criar-conta');
    await preencherPf(usuario, { documento: '52998224724' });

    await usuario.click(screen.getByRole('button', { name: 'Criar conta' }));

    expect(await screen.findByText('CPF inválido.')).toBeInTheDocument();
    expect(screen.getByText('A senha deve conter ao menos um dígito.')).toBeInTheDocument();
    expect(screen.getByText('Campo inesperado.')).toBeInTheDocument();
    expect(screen.getByLabelText('CPF')).toHaveFocus();
    expect(screen.getByLabelText('Nome completo')).toHaveValue('Lucas Silva');
    expect(screen.getByLabelText('Senha')).toHaveValue('SenhaForte@123');
    expect(screen.queryByText('Detalhe técnico')).not.toBeInTheDocument();
  });

  it.each([
    ['email-ja-cadastrado', 'Este e-mail já tem conta. Entre com ele ou use outro e-mail.'],
    ['documento-ja-cadastrado', 'Este documento já tem conta. Entre com ela ou confira o documento.'],
  ])('conflito %s mostra mensagem com link para o login', async (codigo, mensagem) => {
    servidor.use(http.post('*/api/v1/autenticacao/registrar', () => problema(codigo, 409)));
    const { usuario } = await renderizarAppPronto('/criar-conta');
    await preencherPf(usuario);

    await usuario.click(screen.getByRole('button', { name: 'Criar conta' }));

    const alerta = await screen.findByRole('alert');
    expect(alerta).toHaveTextContent(mensagem);
    expect(screen.getByRole('link', { name: 'Ir para o login' })).toHaveAttribute('href', '/entrar');
  });

  it('duplo clique envia uma única requisição', async () => {
    let chamadas = 0;
    servidor.use(
      http.post('*/api/v1/autenticacao/registrar', async () => {
        chamadas++;
        await new Promise((r) => setTimeout(r, 50));
        return problema('email-ja-cadastrado', 409);
      }),
    );
    const { usuario } = await renderizarAppPronto('/criar-conta');
    await preencherPf(usuario);

    await usuario.dblClick(screen.getByRole('button', { name: 'Criar conta' }));
    await screen.findByRole('alert');

    expect(chamadas).toBe(1);
  });

  it('sucesso entra automaticamente e abre o início', async () => {
    let login: unknown;
    servidor.use(
      http.post('*/api/v1/autenticacao/login', async ({ request }) => {
        login = await request.json();
        return HttpResponse.json({ tokenAcesso: (await import('../testes/handlers')).tokenFalso(), tipoToken: 'Bearer', expiraEmSegundos: 300, sessaoExpiraPorInatividadeEmSegundos: 900, sessaoExpiraEmSegundos: 28800 });
      }),
    );
    const { usuario } = await renderizarAppPronto('/criar-conta');
    await preencherPf(usuario);

    await usuario.click(screen.getByRole('button', { name: 'Criar conta' }));

    await waitFor(() => expect(screen.getByTestId('rota-atual')).toHaveTextContent(/^\/$/));
    expect(login).toEqual({ email: 'lucas@exemplo.com.br', senha: 'SenhaForte@123' });
    expect(await screen.findByText(/Olá, Lucas/)).toBeInTheDocument();
  });

  it('indicador de senha segue a política', () => {
    expect(avaliarSenha('').atendidos).toBe(0);
    expect(avaliarSenha('abc').atendidos).toBe(0);
    expect(avaliarSenha('SenhaForte@123')).toEqual({ atendidos: 4, rotulo: 'Senha boa.' });
  });
});
