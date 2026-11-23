-- Origem: specs/006-transferencias (FR-005, FR-007; research R3; issue #8, seção 2).
-- Bloqueia as carteiras da transferência na ordem do identificador (anti-deadlock entre A->B e
-- B->A). Devolve todas as colunas mapeadas da entidade Carteira, inclusive xmin (token de
-- concorrência) e os dados bancários (specs/012-dados-bancarios-carteira). Parâmetro: @ids (uuid[]).
-- Executado dentro de uma transação explícita.

SELECT id, usuario_id, saldo, moeda, atualizado_em,
       codigo_banco, agencia, numero_conta, digito_conta, tipo_conta_bancaria,
       xmin
FROM carteiras
WHERE id = ANY(@ids)
ORDER BY id
FOR UPDATE
