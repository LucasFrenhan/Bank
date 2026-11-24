-- Origem: specs/014-cartao-credito-cobrancas (FR-017, FR-026; research R5; constituição, Princípio IV).
-- Autorizações, compras e lançamentos de fatura são imutáveis. Reutiliza a function
-- impedir_alteracao_de_registro_imutavel (specs/006-transferencias).
-- Aplicado pela migration AdicionarCartoesCobrancasERecebiveis.

CREATE TRIGGER tg_autorizacoes_cartao_imutavel
    BEFORE UPDATE OR DELETE ON autorizacoes_cartao
    FOR EACH ROW EXECUTE FUNCTION impedir_alteracao_de_registro_imutavel();

CREATE TRIGGER tg_compras_cartao_imutavel
    BEFORE UPDATE OR DELETE ON compras_cartao
    FOR EACH ROW EXECUTE FUNCTION impedir_alteracao_de_registro_imutavel();

CREATE TRIGGER tg_lancamentos_fatura_imutavel
    BEFORE UPDATE OR DELETE ON lancamentos_fatura
    FOR EACH ROW EXECUTE FUNCTION impedir_alteracao_de_registro_imutavel();
