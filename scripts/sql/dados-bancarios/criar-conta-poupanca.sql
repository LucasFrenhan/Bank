-- Origem: specs/012-dados-bancarios-carteira (US4, FR-010; research R8).
-- Cria um titular PF de demonstração cuja única conta é uma conta POUPANÇA no banco 001
-- (agência 0001, próximo número da sequence seq_numero_conta_one_bank, saldo 0). Só por script:
-- o sistema cria sempre conta corrente (FR-006).
-- Login de demonstração: e-mail poupanca.demo@exemplo.com.br / senha Poupanca@123
-- (senha_hash no formato V3 do ASP.NET Core Identity, PBKDF2-HMAC-SHA512, 100.000 iterações).
-- Reexecutável: se o e-mail já existir, nada é criado.
-- Uso (na raiz do repositório, com o Compose em execução):
--   docker compose exec -T postgres psql -U bank -d bank -v ON_ERROR_STOP=1 < scripts/sql/dados-bancarios/criar-conta-poupanca.sql

BEGIN;

WITH novo_usuario AS (
    INSERT INTO usuarios (id, nome_completo, email, documento, tipo_conta, senha_hash, criado_em)
    SELECT uuidv7(),
           'Paula Poupança Demonstração',
           'poupanca.demo@exemplo.com.br',
           '12012012094',
           'PessoaFisica',
           'AQAAAAIAAYagAAAAEEp3PL0s8zAi4PVOkvlFFanBB+FiMclq9+qAfg4bJzd1lWIpR2OTWDj2/ng3Bs7Pcg==',
           now()
    WHERE NOT EXISTS (SELECT 1 FROM usuarios WHERE email = 'poupanca.demo@exemplo.com.br')
    RETURNING id
),
numero AS (
    SELECT lpad(nextval('seq_numero_conta_one_bank')::text, 8, '0') AS numero_conta
    FROM novo_usuario
)
INSERT INTO carteiras
    (id, usuario_id, saldo, moeda, atualizado_em, codigo_banco, agencia, numero_conta, digito_conta, tipo_conta_bancaria)
SELECT uuidv7(), u.id, 0, 'BRL', now(), '001', '0001', n.numero_conta,
       calcular_digito_conta('0001', n.numero_conta), 'conta_poupanca'
FROM novo_usuario AS u
CROSS JOIN numero AS n;

COMMIT;
