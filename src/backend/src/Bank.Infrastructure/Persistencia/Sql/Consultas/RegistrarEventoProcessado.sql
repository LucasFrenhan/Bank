-- Origem: specs/010-mensageria-rabbitmq (FR-016, FR-017, FR-019; research R7).
-- Registra que o consumidor tratou o evento, na mesma transação do efeito. Linhas afetadas:
-- 1 = primeira vez (processar); 0 = já processado (duplicado). Uma entrega simultânea do mesmo
-- evento espera a transação da primeira e então vê o conflito.
-- Parâmetros: @evento_id (uuid), @nome_consumidor (varchar(100)), @processado_em (timestamptz).

INSERT INTO eventos_processados (evento_id, nome_consumidor, processado_em)
VALUES (@evento_id, @nome_consumidor, @processado_em)
ON CONFLICT (evento_id, nome_consumidor) DO NOTHING
