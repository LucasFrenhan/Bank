-- Origem: specs/006-transferencias (FR-005, FR-007; research R3; issue #8, seção 2).
-- Bloqueia as carteiras da transferência na ordem do identificador (anti-deadlock entre A->B e
-- B->A). Devolve todas as colunas mapeadas da entidade Carteira, inclusive xmin (token de
-- concorrência). Parâmetro: @ids (uuid[]). Executado dentro de uma transação explícita.

SELECT id, usuario_id, saldo, moeda, atualizado_em, xmin
FROM carteiras
WHERE id = ANY(@ids)
ORDER BY id
FOR UPDATE
