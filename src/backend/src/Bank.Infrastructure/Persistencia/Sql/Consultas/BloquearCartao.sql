-- Origem: specs/014-cartao-credito-cobrancas (FR-020, FR-023; research R4, R6, R8).
-- Bloqueia o cartão (e, por convenção, as suas faturas) para autorizar, lançar, liberar ou pagar fatura.
-- Devolve todas as colunas mapeadas da entidade Cartao.
-- Parâmetro: @id (uuid). Executado dentro de uma transação explícita.

SELECT id, usuario_id, codigo_bandeira, nome_impresso, ultimos_digitos, validade_ano, validade_mes,
       variacao, impressao_digital, limite_total, limite_utilizado, criado_em
FROM cartoes
WHERE id = @id
FOR UPDATE
