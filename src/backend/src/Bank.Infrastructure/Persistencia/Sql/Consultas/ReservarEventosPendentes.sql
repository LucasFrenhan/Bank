-- Origem: specs/010-mensageria-rabbitmq (FR-002, FR-004; research R4).
-- Reserva até @lote eventos pendentes do outbox, em ordem de gravação, para o despachante.
-- SKIP LOCKED: outra instância do despachante não espera nem pega os mesmos eventos.
-- Devolve todas as colunas mapeadas da entidade EventoOutbox. Parâmetro: @lote (integer).
-- Executado dentro de uma transação explícita; os bloqueios duram até o commit.

SELECT id, tipo, versao, payload, ocorrido_em, publicado_em, trace_parent, trace_state
FROM outbox_eventos
WHERE publicado_em IS NULL
ORDER BY ocorrido_em, id
LIMIT @lote
FOR UPDATE SKIP LOCKED
