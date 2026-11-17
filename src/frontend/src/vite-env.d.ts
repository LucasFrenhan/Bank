/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** `true` liga a telemetria do navegador (research R7). */
  readonly VITE_TELEMETRIA_HABILITADA?: string;
  /** Base dos exportadores OTLP/HTTP (padrão `/otlp`). */
  readonly VITE_OTLP_URL?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
