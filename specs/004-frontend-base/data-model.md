# Data Model: Frontend base

**Feature**: [spec.md](spec.md) | **Research**: [research.md](research.md) | **Date**: 2026-11-17

O frontend não persiste dados de negócio; o estado relevante é a sessão, as visões
derivadas das respostas da API e a tradução de erros.

## Sessão

Guardada em `sessionStorage`, chave `bank.sessao` (research R5).

| Campo | Tipo | Origem / regra |
| --- | --- | --- |
| `token` | string | `tokenAcesso` de `POST /autenticacao/login`; nunca exibido, nunca em URL/log/telemetria |
| `expiraEm` | número (epoch ms) | instante do login + `expiraEmSegundos` × 1000 |

**Derivado** (decodificando o payload do JWT, só para exibição e roteamento):
`usuarioId` (`sub`), `email`, `tipoConta` (`tipo_conta`), `carteiraId` (`carteira_id`).
Token sem essas claims ou ilegível → sessão inválida (descartada).

**Ciclo de vida**: `ausente` → (login) → `ativa` → (sair | 401 | `agora ≥ expiraEm` | aba
fechada) → `ausente`. Ao abrir a aplicação, sessão com `expiraEm` no passado é descartada.

## Visões da API

| Visão | Endpoint | Campos usados |
| --- | --- | --- |
| Carteira | `GET /api/v1/carteiras/minha` | `nomeTitular`, `tipoConta`, `saldo`, `moeda`, `atualizadoEm` |
| Titular | `GET /api/v1/usuarios/me` (issue #18) | `nomeCompleto`, `email`, `documento`, `tipoConta`, `criadoEm` |
| Cadastro | `POST /api/v1/autenticacao/registrar` | requisição completa; da resposta, apenas para o login automático |

## Formulários

| Formulário | Campo | Validação local (feedback imediato) | Fonte da regra |
| --- | --- | --- | --- |
| Cadastro | `tipoConta` | obrigatório (PF/PJ) | API |
| | `nomeCompleto` | obrigatório; máx. 150 após remover espaços | API |
| | `documento` | máscara do tipo; PF: 11 dígitos; PJ: 12 `[0-9A-Z]` + 2 dígitos | API (DV, unicidade) |
| | `email` | obrigatório; formato básico | API |
| | `senha` | indicador da política (8–128; maiúscula, minúscula, dígito, símbolo) | API |
| Login | `email`, `senha` | obrigatórios | API |

O valor enviado à API é o digitado com a máscara (a API normaliza); a validação local nunca
bloqueia o envio por dígito verificador — quem decide é a API (FR-006).

## Máscaras e formatação

| Item | Regra |
| --- | --- |
| CPF | `000.000.000-00`, apenas dígitos |
| CNPJ | `XX.XXX.XXX/XXXX-00`; posições 1–12 `[0-9A-Z]` (minúsculas viram maiúsculas), 13–14 dígitos |
| Documento mascarado (perfil) | CPF `•••.456.789-••`; CNPJ `••.•••.345/01DE-••` (oculta os 5 primeiros e os 2 DV) |
| Moeda | `R$ 1.234,56` (`pt-BR`, BRL), algarismos tabulares |
| Data/hora | `17 nov 2026, 14:32` no fuso do navegador |

## Tradução de erros

Entrada: resposta `ProblemDetails` (`type`, `status`, `errors`, `traceId`) ou falha de rede.

| Condição | Mensagem para a pessoa | Onde | Ação |
| --- | --- | --- | --- |
| `urn:bank:problema:validacao` | Mensagens de `errors[campo]` | Abaixo de cada campo; campos desconhecidos → topo do formulário | — |
| `urn:bank:problema:email-ja-cadastrado` | "Este e-mail já tem conta. Entre com ele ou use outro e-mail." | Topo do formulário | Link "Ir para o login" |
| `urn:bank:problema:documento-ja-cadastrado` | "Este documento já tem conta. Entre com ela ou confira o documento." | Topo do formulário | Link "Ir para o login" |
| `urn:bank:problema:credenciais-invalidas` | "E-mail ou senha incorretos" | Topo do formulário | — |
| `urn:bank:problema:nao-autenticado` (ou 401) | "Sua sessão expirou. Entre novamente." | Tela de login | Encerra a sessão |
| `urn:bank:problema:acesso-negado` (ou 403) | "Você não tem acesso a esta área" | Tela | Link para o início |
| `urn:bank:problema:carteira-nao-encontrada` / `usuario-nao-encontrado` | "Não encontramos sua conta. Entre novamente." | Tela de login | Encerra a sessão |
| Rede indisponível | "Não foi possível conectar. Verifique sua internet e tente de novo." | Topo/tela | "Tentar de novo" |
| Qualquer outro (500, desconhecido, corpo inválido) | "Algo deu errado" + "Código para o suporte: {traceId}" | Topo/tela | "Tentar de novo" |

## Configuração

| Variável | Momento | Padrão | Uso |
| --- | --- | --- | --- |
| `BANK_API_URL` | desenvolvimento (proxy do Vite) | `http://localhost:5080` | Destino de `/api` |
| `BANK_OTLP_URL` | desenvolvimento (proxy do Vite) | `http://localhost:4318` | Destino de `/otlp` |
| `VITE_TELEMETRIA_HABILITADA` | build | `false` (`true` em dev e no Compose) | Liga a telemetria (FR-019d) |
| `VITE_OTLP_URL` | build | `/otlp` | Base dos exportadores OTLP/HTTP |
| `FRONTEND_PORT` | Compose | `3000` | Porta publicada em `127.0.0.1` |
