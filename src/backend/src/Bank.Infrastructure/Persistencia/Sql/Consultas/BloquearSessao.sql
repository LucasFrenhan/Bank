-- Origem: specs/016-sessao-refresh-token (FR-006 a FR-008, FR-011; research R3).
-- Bloqueia a sessão dona de uma credencial de renovação (ativa ou já trocada), serializando renovação, saída e
-- novo login da mesma sessão. Devolve todas as colunas mapeadas da entidade Sessao.
-- Parâmetro: @hash (bytea, SHA-256 da credencial). Executado dentro de uma transação explícita.

SELECT s.id, s.usuario_id, s.iniciada_em, s.ultima_renovacao_em, s.expira_em, s.revogada_em, s.motivo_revogacao
FROM sessoes s
WHERE s.id = (SELECT c.sessao_id FROM credenciais_renovacao c WHERE c.hash = @hash)
FOR UPDATE
