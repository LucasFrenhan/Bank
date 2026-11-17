# Feature Specification: Dados do titular autenticado

**Feature Branch**: `005-usuarios-me`

**Created**: 2026-11-17

**Status**: Draft

**Input**: Issue #18 — "Expor os dados do titular autenticado para o frontend (perfil e tela
inicial PJ), que hoje não estão disponíveis na API da spec 003: documento e data de cadastro.
Contrato proposto: `GET /api/v1/usuarios/me`, autenticado, com usuarioId, nomeCompleto, email,
documento, tipoConta e criadoEm; erros 401, 403 e 404." Consumidor: frontend (spec 004, Fase 8;
issue #17).

## Clarifications

### Session 2026-11-17

- Q: A resposta deve trazer o documento completo para PF e PJ, ou completo só para PJ e
  mascarado para PF? → A: Documento completo para PF e PJ; a máscara de exibição fica toda no
  frontend.

## User Scenarios & Testing *(mandatory)*

Os usuários são clientes do Bank autenticados (Pessoa Física ou Jurídica), por meio do
frontend. A funcionalidade expõe os dados cadastrais do próprio titular, que hoje o frontend não
consegue obter: o documento (para o perfil e para a PJ informar o CNPJ a quem vai transferir) e
a data de cadastro ("Cliente desde").

### User Story 1 - Consultar os próprios dados cadastrais (Priority: P1)

Um cliente autenticado consulta seus dados cadastrais e recebe identificador, nome completo ou
razão social, e-mail, documento, tipo de conta e data de cadastro — sempre os seus, nunca os de
outra pessoa.

**Why this priority**: é a única história; desbloqueia o perfil completo e o "Copiar CNPJ" do
frontend (issue #17).

**Independent Test**: cadastrar contas PF e PJ (CNPJ alfanumérico), autenticar cada uma e
consultar os dados; conferir que cada token devolve exatamente os dados do próprio cadastro.

**Acceptance Scenarios**:

1. **Given** um cliente PF autenticado, **When** consulta os próprios dados, **Then** recebe
   200 com identificador, nome completo, e-mail, CPF (11 dígitos, sem máscara), tipo
   `PessoaFisica` e data/hora de cadastro em UTC.
2. **Given** um cliente PJ autenticado com CNPJ alfanumérico, **When** consulta os próprios
   dados, **Then** o documento vem com os 14 caracteres sem máscara e letras em maiúsculas, e
   o tipo é `PessoaJuridica`.
3. **Given** dois clientes autenticados, **When** cada um consulta, **Then** cada resposta
   contém apenas os dados do titular do token.
4. **Given** uma requisição sem token, com token expirado, adulterado ou assinado com outra
   chave/algoritmo, **When** os dados são consultados, **Then** a resposta é 401 no formato
   padronizado (`nao-autenticado`).
5. **Given** um token válido sem as informações de identidade exigidas, **When** os dados são
   consultados, **Then** a resposta é 403 no formato padronizado (`acesso-negado`).
6. **Given** um token válido de um usuário que não existe mais na base, **When** os dados são
   consultados, **Then** a resposta é 404 no formato padronizado (`usuario-nao-encontrado`).

---

### Edge Cases

- Dados alterados diretamente na base depois do login (por exemplo, nome): a resposta reflete
  o valor atual da base, não o do momento do login.
- Identificador do token que não é um identificador válido: tratado como token sem a
  informação de identidade exigida (403).
- O e-mail devolvido é o normalizado (minúsculas), como armazenado na spec 003.
- Data de cadastro devolvida sempre em UTC, com fuso explícito.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: O sistema MUST permitir que um cliente autenticado consulte os próprios dados
  cadastrais em `GET /api/v1/usuarios/me`, exigindo a mesma política de autenticação dos demais
  endpoints protegidos (spec 003, `UsuarioAutenticado`).
- **FR-002**: A resposta MUST conter identificador do usuário, nome completo (ou razão social),
  e-mail normalizado, documento sem máscara (CPF: 11 dígitos; CNPJ: 14 caracteres
  alfanuméricos em maiúsculas), tipo de conta e data/hora de cadastro em UTC.
- **FR-003**: O titular MUST ser identificado exclusivamente pelo token; não há parâmetro que
  permita consultar outro usuário.
- **FR-004**: Os dados MUST ser lidos da base no momento da consulta.
- **FR-005**: Respostas de erro MUST seguir o formato `ProblemDetails` com `traceId` e `type`
  estável: `nao-autenticado` (401), `acesso-negado` (403) e o novo `usuario-nao-encontrado`
  (404), este documentado no contrato.
- **FR-006**: Documento e e-mail MUST NOT aparecer em logs, traces ou métricas gerados pela
  consulta (constituição, Princípio VI).
- **FR-007**: O endpoint MUST ser publicado no documento OpenAPI com o esquema de segurança
  `Bearer` e as respostas 200, 401, 403 e 404, e o contrato MUST ser versionado
  (constituição, Princípio V).
- **FR-008**: O caso de uso MUST ter testes unitários e o endpoint testes de integração
  cobrindo 200, 401, 403 e 404 contra o banco real em contêiner (constituição, Princípio VII).

### Key Entities

- **Titular (visão)**: identificador, nome completo ou razão social, e-mail, documento, tipo
  de conta e data de cadastro do usuário autenticado — leitura dos dados do Usuário da spec 003,
  sem nenhum dado novo persistido.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Em 100% das consultas com token válido de usuário existente, os dados devolvidos
  são exatamente os do titular do token.
- **SC-002**: 100% dos cenários de erro (401, 403, 404) respondem no formato padronizado com
  `type` documentado.
- **SC-003**: 0 ocorrências de documento ou e-mail em logs, traces ou métricas gerados pelos
  testes da funcionalidade.
- **SC-004**: 95% das consultas respondidas em menos de 1 segundo no ambiente local.
- **SC-005**: O frontend consegue exibir documento mascarado, "Cliente desde" e "Copiar CNPJ"
  usando apenas esta consulta (desbloqueia a Fase 8 da spec 004).

## Assumptions

- **Base**: reaproveita o modelo, a autenticação JWT, as políticas e o tratamento de erros da
  spec 003; nenhuma migration nova.
- **Documento completo** (Clarifications): devolvido sem máscara para PF e PJ, por ser dado
  do próprio titular autenticado; a máscara de exibição é responsabilidade do frontend (spec
  004, data-model). A proteção do dado em trânsito e em registros é feita por FR-006 (fora de
  logs, traces e métricas) e pela autenticação de FR-001.
- **Fora de escopo**: alteração de dados cadastrais, foto/avatar, consulta de outros usuários
  (por exemplo, destinatário de transferência — fica com a spec de transferências).
- **Rota**: segue o contrato proposto na issue (`/api/v1/usuarios/me`), no mesmo grupo de
  versão da spec 003.
