-- Origem: specs/012-dados-bancarios-carteira (FR-008; research R2). Próximo número de conta do
-- banco 001. nextval é atômico e não transacional: cadastros simultâneos nunca recebem o mesmo
-- número; o número de um cadastro que falha fica sem uso (lacuna aceita).
-- Resultado: uma coluna "Value" (bigint), como exige Database.SqlQueryRaw<long>.

SELECT nextval('seq_numero_conta_one_bank') AS "Value"
