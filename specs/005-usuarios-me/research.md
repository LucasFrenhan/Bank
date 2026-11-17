# Research: Dados do titular autenticado

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Date**: 2026-11-17

Funcionalidade pequena sobre a base da spec 003: nenhuma dependência nova, nenhuma migration.

## R1. Camadas e caso de uso

- **Decision**: seguir exatamente o padrão de `GET /api/v1/carteiras/minha` (spec 003):
  - Domain: novo erro `ErrosUsuario.NaoEncontrado` (`usuario-nao-encontrado`, `TipoErro.NaoEncontrado`).
  - Application: abstração de leitura `IUsuarioConsultas.ObterTitularAsync(usuarioId)` →
    `DadosTitular?`, e caso de uso `ObterTitular` que devolve `Resultado<DadosTitular>`.
  - Infrastructure: `UsuarioConsultas` com projeção `AsNoTracking` sobre `usuarios` (sem
    carregar senha nem carteira).
  - Api: `UsuariosEndpoints` com `GET /api/v1/usuarios/me`, política `UsuarioAutenticado`,
    `sub` lido do token (transporte) e passado ao caso de uso.
- **Rationale**: consistência com a spec 003 e com o Princípio II (endpoint só transporte;
  leitura atrás de abstração).
- **Alternatives considered**: reaproveitar `IUsuarioRepositorio.ObterPorEmailAsync` (carrega a
  entidade com hash da senha e a carteira; leitura mais pesada e sem necessidade); acrescentar
  campos a `GET /carteiras/minha` (rejeitado no clarify da spec 004: mistura titular e carteira).

## R2. Identificação do titular

- **Decision**: o identificador vem só da claim `sub` (FR-003). Claim ausente → 403 pela
  política `UsuarioAutenticado`; `sub` presente mas não-GUID → 403 (`Forbid`), como em
  `/carteiras/minha`. Para evitar duplicar essa leitura, ela vai para uma extensão
  `ClaimsPrincipal.TentarObterUsuarioId(out Guid)` em `Bank.Api.Seguranca`, usada pelos dois
  endpoints.

## R3. Contrato e erros

- **Decision**: resposta `TitularResposta` (`usuarioId`, `nomeCompleto`, `email`, `documento`,
  `tipoConta`, `criadoEm`), documento completo para PF e PJ (Clarifications). Novo `type`
  `urn:bank:problema:usuario-nao-encontrado` (404) com título "Usuário não encontrado" no mapa
  de `ResultadoHttpExtensions`; 401/403 reaproveitam `nao-autenticado`/`acesso-negado`
  (spec 003, R4). OpenAPI com `Produces<TitularResposta>(200)` e `ProducesProblem` 401/403/404;
  o esquema `Bearer` é aplicado automaticamente pelo transformer da spec 003.

## R4. Privacidade e cache

- **Decision**: resposta com `Cache-Control: no-store` (dados pessoais não ficam em cache de
  navegador nem de proxy — sugestão do clarify). Nenhum log no caso de uso com e-mail ou
  documento (apenas o `UsuarioId` em caso de não encontrado); spans HTTP já não carregam corpo.
- **Teste**: a classe de integração de dados sensíveis da spec 003 é estendida com a consulta.

## R5. Observabilidade

- **Decision**: sem métrica de negócio nova — leitura de perfil não é caso de uso de negócio
  relevante (Princípio VI); as métricas HTTP (`http.server.request.duration` por rota) já
  cobrem volume e latência.

## R6. Testes

- **Unitários**: `ObterTitular` (encontrado; não encontrado → `usuario-nao-encontrado`).
- **Integração** (Testcontainers, `BankApiFactory`): 200 PF e PJ (CNPJ alfanumérico),
  isolamento entre dois usuários, valor atual após alteração direta na base, `no-store`;
  401 (sem token, expirado, adulterado, outra chave, `alg: none`, HS512, emissor e audiência
  errados — reaproveitando `TokensDeTeste`); 403 (sem `sub`/`tipo_conta`/`carteira_id`; `sub`
  não-GUID); 404 (usuário removido); OpenAPI com o caminho, as respostas e o `Bearer`.
