-- Origem: specs/012-dados-bancarios-carteira (FR-007, FR-008; research R7). Executado uma única
-- vez pela migration AdicionarDadosBancariosCarteira, antes de as colunas ficarem obrigatórias.
-- Numera as carteiras existentes em ordem de id (UUIDv7 = ordem de criação) com row_number, e
-- não com nextval, porque numa subconsulta uma função volátil pode ser avaliada antes do
-- ORDER BY. Depois ajusta a sequence para o próximo cadastro continuar a partir do maior número.

UPDATE carteiras AS c
SET codigo_banco = '001',
    agencia = '0001',
    numero_conta = lpad(numerada.n::text, 8, '0'),
    digito_conta = calcular_digito_conta('0001', lpad(numerada.n::text, 8, '0')),
    tipo_conta_bancaria = 'conta_corrente'
FROM (
    SELECT id, row_number() OVER (ORDER BY id) AS n
    FROM carteiras
    WHERE numero_conta IS NULL
) AS numerada
WHERE c.id = numerada.id;

SELECT CASE
           WHEN max_numero IS NULL THEN setval('seq_numero_conta_one_bank', 1, false)
           ELSE setval('seq_numero_conta_one_bank', max_numero, true)
       END
FROM (SELECT max(numero_conta::bigint) AS max_numero FROM carteiras) AS maximo;
