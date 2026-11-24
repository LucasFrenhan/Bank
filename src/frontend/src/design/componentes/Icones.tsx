/** Ícones de traço simples, decorativos (sempre acompanhados de texto). */
const base = {
  width: 20,
  height: 20,
  viewBox: '0 0 24 24',
  fill: 'none',
  stroke: 'currentColor',
  strokeWidth: 2,
  strokeLinecap: 'round' as const,
  strokeLinejoin: 'round' as const,
  'aria-hidden': true,
  focusable: false,
};

export const IconeInicio = () => (
  <svg {...base}>
    <path d="M3 10.5 12 3l9 7.5V20a1 1 0 0 1-1 1h-5v-6h-6v6H4a1 1 0 0 1-1-1z" />
  </svg>
);

export const IconeTransferir = () => (
  <svg {...base}>
    <path d="M4 7h14l-3-3M20 17H6l3 3" />
  </svg>
);

export const IconeExtrato = () => (
  <svg {...base}>
    <rect x="5" y="3" width="14" height="18" rx="2" />
    <path d="M9 8h6M9 12h6M9 16h4" />
  </svg>
);

export const IconePerfil = () => (
  <svg {...base}>
    <circle cx="12" cy="8" r="4" />
    <path d="M4 21a8 8 0 0 1 16 0" />
  </svg>
);

export const IconeRecebimentos = () => (
  <svg {...base}>
    <path d="M12 4v12m0 0-5-5m5 5 5-5M5 20h14" />
  </svg>
);

export const IconeCobrancas = () => (
  <svg {...base}>
    <rect x="3" y="5" width="18" height="14" rx="2" />
    <path d="M3 10h18" />
  </svg>
);

export const IconeSair = () => (
  <svg {...base}>
    <path d="M15 4h4a1 1 0 0 1 1 1v14a1 1 0 0 1-1 1h-4M10 17l5-5-5-5M15 12H4" />
  </svg>
);

/* ---------- Transferências e extrato (specs/007) ---------- */

export const IconeEntrada = () => (
  <svg {...base}>
    <path d="M12 5v14m0 0-6-6m6 6 6-6" />
  </svg>
);

export const IconeSaida = () => (
  <svg {...base}>
    <path d="M12 19V5m0 0-6 6m6-6 6 6" />
  </svg>
);

export const IconeVoltar = () => (
  <svg {...base}>
    <path d="m15 18-6-6 6-6" />
  </svg>
);

export const IconeConfirmado = () => (
  <svg {...base}>
    <path d="m5 12 5 5L20 7" />
  </svg>
);

export const IconeAlerta = () => (
  <svg {...base}>
    <path d="M12 3 2 20h20L12 3z" />
    <path d="M12 10v4M12 17h.01" />
  </svg>
);

export const IconeInformacao = () => (
  <svg {...base}>
    <circle cx="12" cy="12" r="9" />
    <path d="M12 11v5M12 8h.01" />
  </svg>
);

export const IconeErro = () => (
  <svg {...base}>
    <circle cx="12" cy="12" r="9" />
    <path d="M12 7v6M12 16h.01" />
  </svg>
);

export const IconeCadeado = () => (
  <svg {...base}>
    <rect x="5" y="11" width="14" height="10" rx="2" />
    <path d="M8 11V8a4 4 0 0 1 8 0v3" />
  </svg>
);

/* ---------- Cartão (specs/015) ---------- */

export const IconeCartao = () => (
  <svg {...base}>
    <rect x="3" y="5" width="18" height="14" rx="2" />
    <path d="M3 10h18M7 15h4" />
  </svg>
);

/** Dados visíveis. */
export const IconeOlhoAberto = () => (
  <svg {...base} width={24} height={24}>
    <path d="M2 12s3.6-7 10-7 10 7 10 7-3.6 7-10 7S2 12 2 12z" />
    <circle cx="12" cy="12" r="3" />
  </svg>
);

/** Dados ocultos. */
export const IconeOlhoRiscado = () => (
  <svg {...base} width={24} height={24}>
    <path d="M9.9 5.2A10.6 10.6 0 0 1 12 5c6.4 0 10 7 10 7a17.6 17.6 0 0 1-3.2 4M6.2 6.6A17 17 0 0 0 2 12s3.6 7 10 7c1.8 0 3.3-.5 4.6-1.2M9.9 9.9a3 3 0 0 0 4.2 4.2M3 3l18 18" />
  </svg>
);
