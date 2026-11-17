-- Origem: specs/006-transferencias (research R2). Usado pelo Down da migration
-- CriarTransferenciasLancamentosEOutbox para desfazer ImpedirAlteracaoDeRegistrosImutaveis.sql.

DROP TRIGGER IF EXISTS tg_lancamentos_imutavel ON lancamentos;
DROP TRIGGER IF EXISTS tg_transferencias_imutavel ON transferencias;
DROP FUNCTION IF EXISTS impedir_alteracao_de_registro_imutavel();
