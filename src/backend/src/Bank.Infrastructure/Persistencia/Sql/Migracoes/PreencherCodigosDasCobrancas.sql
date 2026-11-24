-- Origem: specs/015-frontend-cartao-credito (FR-009a; research R2). Executado uma única vez pela migration
-- AdicionarCodigoCobranca, com a coluna anulável e o índice único já criado. Sorteia o código de cada cobrança
-- existente, com 32 bits aleatórios (gen_random_uuid, gerador criptográfico do PostgreSQL) por caractere, e
-- sorteia de novo se o código já existir.

DO $$
DECLARE
    alfabeto CONSTANT text := '23456789ABCDEFGHJKMNPQRSTVWXYZ';
    cobranca uuid;
    sorteado text;
BEGIN
    FOR cobranca IN SELECT id FROM cobrancas WHERE codigo IS NULL LOOP
        LOOP
            sorteado := '';
            FOR i IN 1..8 LOOP
                sorteado := sorteado || substr(alfabeto,
                    1 + (('x' || substr(gen_random_uuid()::text, 1, 8))::bit(32)::bigint % length(alfabeto))::int, 1);
            END LOOP;

            EXIT WHEN NOT EXISTS (SELECT 1 FROM cobrancas WHERE codigo = sorteado);
        END LOOP;

        UPDATE cobrancas SET codigo = sorteado WHERE id = cobranca;
    END LOOP;
END;
$$;
