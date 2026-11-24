-- Origem: specs/014-cartao-credito-cobrancas (FR-031, FR-032; research R8).
-- Bloqueia o recebível para liquidar ou antecipar. Devolve todas as colunas mapeadas da entidade
-- Recebivel. Parâmetro: @id (uuid). Executado dentro de uma transação explícita.

SELECT id, cobranca_id, lojista_usuario_id, carteira_id, valor_bruto, taxa, valor_liquido, data_prevista,
       situacao, criado_em, recebido_em, chave_idempotencia
FROM recebiveis
WHERE id = @id
FOR UPDATE
