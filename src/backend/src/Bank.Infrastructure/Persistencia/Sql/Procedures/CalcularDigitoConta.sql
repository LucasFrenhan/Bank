-- Origem: specs/012-dados-bancarios-carteira (FR-004; research R1). Criada pela migration
-- AdicionarDadosBancariosCarteira e usada pelo backfill, pelos CHECK de carteiras e
-- contas_externas e pelos scripts de scripts/sql/dados-bancarios.
-- Dígito verificador por Luhn (módulo 10) sobre agencia || numero: da direita para a esquerda,
-- dobra um dígito sim, outro não (começando pelo mais à direita), subtrai 9 dos resultados
-- acima de 9 e devolve (10 - soma % 10) % 10. Mesma regra de ContaBancaria.CalcularDigito (C#).

CREATE OR REPLACE FUNCTION calcular_digito_conta(agencia text, numero text)
RETURNS char(1)
LANGUAGE sql
IMMUTABLE
STRICT
PARALLEL SAFE
AS $$
    SELECT (((10 - (sum(
               CASE
                   WHEN posicao % 2 = 1 THEN
                       CASE WHEN digito * 2 > 9 THEN digito * 2 - 9 ELSE digito * 2 END
                   ELSE digito
               END) % 10)) % 10))::text::char(1)
    FROM (
        SELECT substr(agencia || numero, length(agencia || numero) - posicao + 1, 1)::int AS digito,
               posicao
        FROM generate_series(1, length(agencia || numero)) AS posicao
    ) AS digitos
$$;
