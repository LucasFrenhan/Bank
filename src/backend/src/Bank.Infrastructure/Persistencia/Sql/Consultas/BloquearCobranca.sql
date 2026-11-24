-- Origem: specs/014-cartao-credito-cobrancas (FR-020; research R8).
-- Bloqueia a cobrança para pagar ou cancelar. Devolve todas as colunas mapeadas da entidade Cobranca.
-- Parâmetro: @id (uuid). Executado dentro de uma transação explícita.

SELECT id, lojista_usuario_id, carteira_destino_id, valor, descricao, status, pagador_usuario_id,
       codigo_bandeira, ultimos_digitos_cartao, codigo_autorizacao, paga_em,
       ultima_recusa_motivo, ultima_recusa_em, criado_em, cancelada_em
FROM cobrancas
WHERE id = @id
FOR UPDATE
