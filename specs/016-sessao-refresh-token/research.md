# Research: Sessão renovável com refresh token rotativo e logout no servidor

**Feature**: [spec.md](spec.md) | **Plano**: [plan.md](plan.md) | **Data**: 2026-11-27

Cada item registra a decisão, o motivo e as alternativas descartadas. A spec não tem pendências: as 6
clarificações estão na seção Clarifications.

## R1. Formato e armazenamento da credencial de renovação

- **Decisão**: 32 bytes de `RandomNumberGenerator`, serializados em Base64Url (43 caracteres). O banco guarda
  só o SHA-256 do valor (`bytea`, 32 bytes, índice único). A comparação é feita pelo índice (busca pelo hash),
  não por comparação de strings em memória.
- **Motivo**: 256 bits de entropia tornam a adivinhação inviável, então um hash rápido basta; um hash lento
  (PBKDF2/Argon2) só faz sentido para segredos de baixa entropia, como senhas. Busca por hash exato evita
  ataques de tempo.
- **Alternativas**: JWT como refresh token (não é revogável sem estado e expõe claims; descartado); HMAC com
  chave do servidor (protege contra vazamento só do banco, mas acrescenta mais um segredo para rotacionar;
  sem ganho real, já que o valor é aleatório); guardar o valor cifrado (desnecessário, nunca precisa ser lido
  de volta).

## R2. Modelo: sessão como agregado e histórico de credenciais

- **Decisão**: agregado `Sessao` (raiz, `SessaoId` UUIDv7) com início, última renovação, limite absoluto,
  revogação e motivo; tabela filha `credenciais_renovacao` com uma linha por credencial já emitida (hash,
  emissão, vencimento, rotação e substituta). Só a credencial mais recente da sessão está ativa; as demais
  ficam como histórico para detectar reuso. Detalhes em [data-model.md](data-model.md).
- **Motivo**: o reuso só é detectável se o servidor souber que um hash antigo pertenceu à sessão. A sessão
  concentra as regras (limites, revogação, tolerância) e é o ponto de bloqueio.
- **Alternativas**: uma tabela única de tokens com `familia_id` (sugerida na issue; espalha as regras da
  família por várias linhas e complica o bloqueio); guardar só a credencial atual e a anterior na sessão (não
  detecta reuso de credenciais mais antigas que a anterior).

## R3. Rotação atômica, tolerância e concorrência no servidor

- **Decisão**: a renovação acontece em uma transação `ReadCommitted`:
  1. busca a credencial pelo hash; se não existe → recusa;
  2. bloqueia a sessão com `SELECT … FOR UPDATE` (`Sql/Consultas/BloquearSessao.sql`), serializando
     renovações da mesma sessão;
  3. a sessão decide (`Sessao.Renovar`): revogada, vencida por inatividade ou limite absoluto → recusa; a
     credencial é a ativa → rotaciona; a credencial foi rotacionada há até 30 s
     (`Sessao:ToleranciaReusoSegundos`) → também rotaciona a partir da credencial ativa (a tolerância vale
     para a credencial apresentada, não cria família nova); rotacionada há mais de 30 s → **reuso**: revoga a
     sessão (motivo `reuso`) e recusa;
  4. a rotação marca a ativa como rotacionada, insere a nova e atualiza a última renovação.
- **Motivo**: o bloqueio na linha da sessão é o padrão já usado no projeto (`BloquearCobranca.sql` etc.) e
  impede duas rotações concorrentes de "ganharem" ao mesmo tempo. Aceitar credenciais rotacionadas há poucos
  segundos cobre abas simultâneas e respostas perdidas (spec FR-008) sem devolver ao cliente um valor que o
  servidor não tem (só existe o hash).
- **Alternativas**: devolver a mesma credencial nova na repetição (exigiria guardar o valor original,
  contrariando FR-004); controle otimista com `xmin` (gera falsos conflitos e uma segunda tentativa no
  cliente); tolerância zero (derrubaria sessões legítimas com várias abas).

## R4. Cookie

- **Decisão**: `Set-Cookie: bank_renovacao=<valor>; Path=/api/v1/autenticacao; HttpOnly; Secure;
  SameSite=Strict; Max-Age=<segundos até o vencimento da credencial>`. Remoção: mesmo nome e `Path`, com
  `Max-Age=0`. Sem atributo `Domain` (fica restrito ao host). O nome e o caminho são constantes do contrato.
- **Motivo**: `HttpOnly` afasta scripts (XSS); `Path` restrito faz o navegador enviar o cookie só aos
  endpoints de autenticação (FR-003); `SameSite=Strict` impede o envio a partir de outro site, o que, com
  métodos `POST` sem CORS habilitado, cobre CSRF em `renovar` e `sair` (FR-022). `Secure` funciona no ambiente
  local porque Chromium, Firefox e WebKit tratam `http://localhost` como contexto seguro.
- **Alternativas**: prefixo `__Host-` (exige `Path=/`, perdendo a restrição de caminho); prefixo `__Secure-`
  (o suporte em `http://localhost` varia entre navegadores, risco desnecessário); `SameSite=Lax` (envia em
  navegação de topo vinda de outro site, sem ganho aqui); desligar `Secure` em desenvolvimento (diferença
  entre ambientes que esconderia problemas).
- **Testes de integração**: o `HttpClient` do `WebApplicationFactory` usa `http://localhost`, e o
  `CookieContainer` do .NET não reenvia cookies `Secure` sobre HTTP. Os testes leem o `Set-Cookie` e mandam o
  cabeçalho `Cookie` explicitamente (helper `SessaoDeTeste`), o que também permite simular reuso.

## R5. Token de acesso

- **Decisão**: mantém o JWT HS256 atual; `Jwt:ExpiracaoSegundos` passa de 3600 para **300**. Nova claim
  `sid` (identificador da sessão) para correlação em logs e testes. A validação na API não consulta a sessão
  (revogação imediata do token de acesso está fora de escopo).
- **Motivo**: menor mudança possível; 5 minutos limitam o uso de um token vazado (SC-005). A claim é aditiva
  e não quebra o frontend atual.
- **Alternativas**: consultar a sessão a cada requisição (vira revogação imediata, fora de escopo, e custa uma
  ida ao banco por chamada); assinatura assimétrica (sem consumidor externo que precise só da chave pública).

## R6. Contrato HTTP

- **Decisão**: contrato `usuarios-carteiras-api` 1.0.0 → **1.1.0**, aditivo:
  - `POST /login` mantém o corpo e acrescenta `sessaoExpiraPorInatividadeEmSegundos` e
    `sessaoExpiraEmSegundos` (limite absoluto), relativos ao momento da resposta, e o `Set-Cookie`;
  - `POST /renovar` (público, sem corpo, lê o cookie) → 200 com o mesmo corpo do login, ou 401
    `urn:bank:problema:nao-autenticado` com remoção do cookie;
  - `POST /sair` (público, sem corpo) → 204 sempre, com remoção do cookie.
  Detalhes em [contracts/autenticacao-sessao-api.md](contracts/autenticacao-sessao-api.md).
- **Motivo**: prazos relativos evitam depender do relógio do navegador (edge case da spec). Reaproveitar
  `nao-autenticado` dá a recusa única exigida por FR-010 e pela clarificação sobre reuso, e o frontend já trata
  esse tipo.
- **Alternativas**: instantes absolutos (dependem do relógio local); tipo de erro novo `sessao-expirada`
  (revelaria nada a mais, mas obrigaria o frontend a tratar dois tipos para o mesmo efeito).

## R7. Novo login e sessões simultâneas

- **Decisão**: o login lê o cookie recebido, se houver, e revoga essa sessão (motivo `novo-login`) na mesma
  transação em que cria a nova. Sessões de outros navegadores não são tocadas (FR-012a); não há limite de
  sessões por pessoa.
- **Motivo**: atende FR-012 sem precisar de gestão de dispositivos.
- **Alternativas**: revogar todas as sessões da pessoa no login (contraria a clarificação de sessões
  ilimitadas).

## R8. Limpeza

- **Decisão**: serviço hospedado no **Worker** (`LimpezaSessoesHospedado`), a cada
  `LimpezaSessoes:IntervaloMinutos` (padrão 60), apaga em lote as sessões revogadas ou vencidas há mais de
  `Sessao:RetencaoDias` (padrão 7), com as credenciais em cascata (`ON DELETE CASCADE`). Usa
  `ExecuteDeleteAsync` (LINQ), sem SQL puro. Desligado em `Testing`; os testes chamam o caso de uso direto.
- **Motivo**: o Worker já hospeda serviços de fundo (processamento diário) e a API não agenda nada. A exclusão
  é idempotente, então várias instâncias não causam problema.
- **Alternativas**: job no PostgreSQL (`pg_cron`, dependência de infraestrutura nova); limpar durante o login
  (acopla latência do login à limpeza).

## R9. Frontend: token só em memória e renovação única

- **Decisão**:
  - `sessao/tokenMemoria.ts` guarda o token de acesso em variável de módulo; `armazenamento.ts` deixa de usar
    `sessionStorage` para a sessão e remove a chave antiga `bank.sessao` na primeira carga (limpeza de quem já
    tinha sessão gravada).
  - `sessao/renovador.ts` faz a renovação em voo único: chamadas concorrentes na mesma aba aguardam a mesma
    promessa; entre abas, a chamada roda dentro de `navigator.locks.request('bank.renovacao', …)` (Web Locks
    API), então nunca há duas renovações simultâneas no mesmo navegador. Onde a API não existir, cai para o
    voo único por aba (a tolerância do servidor cobre o resto).
  - O `fetch` passado ao `openapi-fetch` vira um invólucro: guarda `request.clone()`, e num 401 de chamada
    autenticada renova e repete **uma vez**; 401 em `/autenticacao/*` nunca dispara renovação (sem laço).
  - Ao carregar a página, `ProvedorSessao` começa em estado `verificando` e chama `renovar` antes de liberar
    rotas protegidas; telas de login e cadastro com sessão válida redirecionam para o início, como hoje.
- **Motivo**: memória é o único lugar fora do alcance de leitura persistente; Web Locks é suportada pelos
  navegadores da spec 004 e é a forma padrão de exclusão mútua entre abas.
- **Alternativas**: compartilhar o token de acesso entre abas por `BroadcastChannel` (evita renovações, mas
  espalha o token por mais contextos; cada aba renovar a sua é simples e a rotação serializada é barata);
  middleware `onResponse` do `openapi-fetch` (não permite repetir a requisição); renovação proativa por
  temporizador (contraria a clarificação de renovar só sob demanda).

## R10. Frontend: sessão compartilhada entre abas e aviso de expiração

- **Decisão**: `BroadcastChannel('bank.sessao')` com mensagens sem token: `entrou` (com o `sub`, que não é segredo), `renovou` (com os prazos
  de inatividade e absoluto, em segundos), `saiu` e `expirou`.
  - `entrou` → as abas em login/cadastro renovam e seguem para o início; uma aba com sessão de outro `sub` descarta a sessão (inclusive dados da operação) e renova; `saiu`/`expirou` → as demais abas
    descartam a sessão local (inclusive transferência em andamento e chaves de idempotência daquela aba) e vão
    ao login; `renovou` → todas reiniciam os temporizadores do aviso e fecham o aviso se estiver aberto.
  - `AvisoExpiracao` usa o modal de confirmação existente (`design/componentes/Confirmacao.tsx`): aparece 60 s (ou na metade do prazo de inatividade, se ele for menor que 2 min)
    antes do fim por inatividade com "Continuar conectado" e "Sair"; antes do limite absoluto, só informa e
    oferece "Entendi". Ao chegar a zero, encerra como sessão expirada.
- **Motivo**: atende FR-016, FR-016a e FR-016b sem guardar estado no navegador; reaproveita o modal já
  validado em desktop e mobile.
- **Alternativas**: evento `storage` do `localStorage` como canal (exige gravar algo no armazenamento, contra o
  espírito de FR-005); avisar só na aba ativa (a pessoa pode estar em outra aba).

## R11. Dados da operação em andamento

- **Decisão**: a renovação não toca em `bank.operacoes` nem nos dados de transferência (continuam no
  `sessionStorage` de cada aba, sem token). `removerSessao()` continua limpando esses dados, mas só é chamada
  em fim de sessão (sair, expirar, recusa de renovação, troca de pessoa).
- **Motivo**: FR-018. A repetição automática da requisição reaproveita o mesmo `Idempotency-Key`, porque o
  invólucro repete o mesmo `Request` clonado.

## R12. Observabilidade e dados sensíveis

- **Decisão**:
  - métricas em `Bank.Usuarios`: `bank.autenticacao.renovacoes` (tag `resultado`: `sucesso`, `recusada`,
    `reuso`), `bank.autenticacao.sessoes_revogadas` (tag `motivo`: `logout`, `reuso`, `novo-login`) e
    `bank.autenticacao.sessoes_removidas` (Worker);
  - logs estruturados só com `SessaoId` e `UsuarioId`; reuso em `Warning` ("Reuso de credencial detectado;
    sessão {SessaoId} revogada");
  - a instrumentação ASP.NET Core e a de `fetch` do navegador não capturam cabeçalhos (configuração atual);
    um teste de integração novo varre logs, spans e métricas coletados durante login, renovação e saída em
    busca do valor do cookie e do token, como `DadosSensiveisTelemetriaTests`.
- **Motivo**: FR-020 e FR-021; constituição VI.

## R13. Transição no deploy

- **Decisão**: nenhuma migração de dados. Tokens de 1 hora emitidos antes do deploy continuam válidos até
  vencer (mesma chave); sem cookie, a renovação falha e a pessoa entra de novo, uma única vez.
- **Motivo**: custo zero e comportamento igual ao de hoje para quem estiver logado no momento do deploy.

## R14. Limite de tentativas na renovação

- **Decisão**: sem limitação de taxa específica nesta spec.
- **Motivo**: com 256 bits de entropia não há força bruta viável; limitação de taxa geral da API é tema
  transversal, não desta issue.

## R15. Testes

- **Decisão**:
  - **Unitários (Domain/Application)**: `Sessao` (rotação, tolerância no limite de 30 s, reuso, inatividade,
    limite absoluto, revogação idempotente) com `FakeTimeProvider`; `RenovarSessao`, `EncerrarSessao`,
    `Autenticar` (cria sessão e revoga a anterior), `LimparSessoes`; gerador de credenciais (tamanho e
    unicidade).
  - **Integração (Testcontainers)**: atributos do cookie no login; renovação com rotação; reuso revoga a
    família; tolerância com duas renovações paralelas; inatividade e limite absoluto (relógio controlado);
    `sair` idempotente e sem cookie; novo login revoga a sessão anterior e não as de outro "navegador";
    banco sem valor em texto puro; varredura de telemetria; endpoints protegidos continuam 401 sem token.
  - **Arquitetura**: `SessaoId` e `CredencialRenovacaoId` tipados (regra existente cobre).
  - **Frontend (Vitest)**: renovador (voo único, repetição única, sem laço em `/autenticacao/*`, falha leva
    ao login), mensagens entre abas, aviso de expiração com temporizadores falsos, ausência do token em
    `sessionStorage`/`localStorage`.
  - **E2E (Playwright)**: `sessao.spec.ts` revisto — recarregar mantém a sessão; segunda aba no mesmo contexto
    já entra; "Sair" em uma aba leva a outra ao login; token de acesso curto (configurado no ambiente de teste)
    renovado sem perder uma transferência em revisão; nenhum token no armazenamento; cookie `HttpOnly`.
