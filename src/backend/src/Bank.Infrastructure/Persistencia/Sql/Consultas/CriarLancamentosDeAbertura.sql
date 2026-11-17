-- Origem: specs/006-transferencias (FR-016; research R5) e spec 003 (saldo de demonstração).
-- Cria o lançamento de abertura das carteiras cujo saldo não está coberto por lançamentos
-- (carteiras criadas antes do razão). Idempotente: no máximo uma abertura por carteira
-- (ux_lancamentos_abertura_por_carteira). Executado só fora de Production.

INSERT INTO lancamentos (id, carteira_id, tipo, direcao, valor, transferencia_id, descricao, criado_em)
SELECT uuidv7(),
       c.id,
       'Abertura',
       'Entrada',
       c.saldo - COALESCE(s.soma, 0),
       NULL,
       'Saldo inicial de demonstração',
       u.criado_em
FROM carteiras c
JOIN usuarios u ON u.id = c.usuario_id
LEFT JOIN (
    SELECT carteira_id, SUM(CASE direcao WHEN 'Entrada' THEN valor ELSE -valor END) AS soma
    FROM lancamentos
    GROUP BY carteira_id
) s ON s.carteira_id = c.id
WHERE c.saldo - COALESCE(s.soma, 0) > 0
  AND NOT EXISTS (
      SELECT 1 FROM lancamentos a WHERE a.carteira_id = c.id AND a.tipo = 'Abertura'
  )
ON CONFLICT DO NOTHING
