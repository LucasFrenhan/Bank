-- Origem: specs/014-cartao-credito-cobrancas (research R5).
-- Desfaz ImpedirAlteracaoDeRegistrosDeCartao.sql (Down da migration AdicionarCartoesCobrancasERecebiveis).

DROP TRIGGER IF EXISTS tg_lancamentos_fatura_imutavel ON lancamentos_fatura;
DROP TRIGGER IF EXISTS tg_compras_cartao_imutavel ON compras_cartao;
DROP TRIGGER IF EXISTS tg_autorizacoes_cartao_imutavel ON autorizacoes_cartao;
