-- Origem: specs/012-dados-bancarios-carteira (US4, FR-010, FR-011, FR-014; research R8).
-- Registra contas fictícias nos bancos 002 a 005 (uma corrente e uma poupança por banco), só
-- para simular a integração com outros bancos: sem usuário, login nem saldo.
-- Reexecutável: contas já registradas (mesmo banco, agência e número) são ignoradas.
-- Uso (na raiz do repositório, com o Compose em execução):
--   docker compose exec -T postgres psql -U bank -d bank -v ON_ERROR_STOP=1 < scripts/sql/dados-bancarios/registrar-contas-outros-bancos.sql

BEGIN;

INSERT INTO contas_externas
    (id, codigo_banco, agencia, numero_conta, digito_conta, tipo_conta_bancaria, titular_nome, titular_documento, criado_em)
SELECT uuidv7(), c.codigo_banco, c.agencia, c.numero_conta,
       calcular_digito_conta(c.agencia, c.numero_conta), c.tipo, c.titular_nome, c.titular_documento, now()
FROM (VALUES
    ('002', '0102', '20000001', 'conta_corrente', 'Ana Aurora Ribeiro',        '21098765494'),
    ('002', '0102', '20000002', 'conta_poupanca', 'Aurora Comércio Ltda',      '21234567000100'),
    ('003', '0203', '30000001', 'conta_corrente', 'Bruno Horizonte Lima',      '31098765400'),
    ('003', '0203', '30000002', 'conta_poupanca', 'Horizonte Serviços Ltda',   '31234567000156'),
    ('004', '0304', '40000001', 'conta_corrente', 'Carla Atlântico Souza',     '41098765400'),
    ('004', '0304', '40000002', 'conta_poupanca', 'Atlântico Pescados Ltda',   '41234567000100'),
    ('005', '0405', '50000001', 'conta_corrente', 'Diego Cerrado Alves',       '51098765419'),
    ('005', '0405', '50000002', 'conta_poupanca', 'Cerrado Agro Ltda',         '51234567000163')
) AS c (codigo_banco, agencia, numero_conta, tipo, titular_nome, titular_documento)
ON CONFLICT (codigo_banco, agencia, numero_conta) DO NOTHING; -- ux_contas_externas_conta_bancaria

COMMIT;
