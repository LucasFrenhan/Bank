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
