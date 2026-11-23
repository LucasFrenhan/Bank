-- Origem: specs/012-dados-bancarios-carteira (research R7). Usado pelo Down da migration
-- AdicionarDadosBancariosCarteira para desfazer CalcularDigitoConta.sql.

DROP FUNCTION IF EXISTS calcular_digito_conta(text, text);
