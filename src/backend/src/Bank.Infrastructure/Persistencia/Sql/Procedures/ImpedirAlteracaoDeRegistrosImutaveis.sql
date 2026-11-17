-- Origem: specs/006-transferencias (FR-008; research R2; constituição, Princípio IV).
-- Transferências e lançamentos são imutáveis: correções são novos lançamentos compensatórios.
-- Aplicado pela migration CriarTransferenciasLancamentosEOutbox.

CREATE OR REPLACE FUNCTION impedir_alteracao_de_registro_imutavel()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION 'Registro imutável: % em % não é permitido.', TG_OP, TG_TABLE_NAME
        USING ERRCODE = 'restrict_violation';
END;
$$;

CREATE TRIGGER tg_transferencias_imutavel
    BEFORE UPDATE OR DELETE ON transferencias
    FOR EACH ROW EXECUTE FUNCTION impedir_alteracao_de_registro_imutavel();

CREATE TRIGGER tg_lancamentos_imutavel
    BEFORE UPDATE OR DELETE ON lancamentos
    FOR EACH ROW EXECUTE FUNCTION impedir_alteracao_de_registro_imutavel();
