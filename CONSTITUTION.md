# Constituição do projeto Bank

O texto canônico da constituição fica em
[`.specify/memory/constitution.md`](.specify/memory/constitution.md), que é o arquivo
lido pelos comandos do Spec Kit (`/speckit-plan`, `/speckit-analyze` etc.).

Este arquivo existe apenas como ponto de entrada; não duplique o conteúdo aqui.

Resumo dos princípios (versão 1.0.0):

1. **Specification-First (SDD)**: nenhum código sem spec aprovada em `specs/`.
2. **Isolamento da Camada de Aplicação**: endpoints não acessam `DbContext` nem contêm
   regra de negócio.
3. **Erros Padronizados**: toda resposta de erro usa `ProblemDetails` (RFC 7807).
4. **Integridade Financeira**: `decimal`, operações atômicas e idempotentes, lançamentos
   imutáveis.
5. **Contratos Explícitos e Versionados**: OpenAPI + Scalar; nada de quebras silenciosas.
6. **Observabilidade Nativa**: OpenTelemetry via OTLP, sem dados sensíveis.
7. **Qualidade Verificável**: testes unitários, de integração e de arquitetura no CI.

Estratégia de branching: uma branch por spec com o mesmo nome da pasta
(`NNN-nome-curto`); mudanças sem spec usam `<tipo>/<issue>-nome-curto`.
