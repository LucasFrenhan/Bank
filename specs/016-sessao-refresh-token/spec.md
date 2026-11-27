# Feature Specification: Sessão renovável com refresh token rotativo e logout no servidor

**Feature Branch**: `016-sessao-refresh-token`

**Created**: 2026-11-27

**Status**: Draft

**Input**: Issue #57 ("Autenticação: refresh token com rotação e logout no servidor"). Permitir **renovar a
sessão sem novo login** e **encerrá-la de verdade no servidor**. O tema ficou fora de escopo na
[spec 003](../003-usuarios-carteiras/spec.md) (refresh token, logout e revogação) e foi adiado para "uma spec
futura de segurança" na [spec 004](../004-frontend-base/spec.md) (Assumptions, "Sessão"). Esta spec altera
os requisitos de sessão da spec 004 (FR-009, FR-011, FR-012 e FR-013) e o contrato de autenticação da
spec 003, de forma aditiva.

**Situação atual**: um único token de acesso válido por 1 hora, sem nenhum estado no servidor; no
navegador, o token fica guardado no armazenamento da aba. A pessoa é deslogada após 1 hora mesmo em uso
e perde a operação em andamento (transferência em revisão, chaves de idempotência de pagamentos e
antecipações); "Sair" só apaga o token do navegador, e um token vazado vale até expirar; o token guardado
pode ser lido por script em caso de XSS.

## Clarifications

### Session 2026-11-27

- Q: A sessão continua restrita à aba (spec 004, FR-013) ou passa a ser compartilhada entre as abas? → A:
  Compartilhada entre as abas do navegador: entrar em uma aba vale para todas, e "Sair" em uma aba encerra
  a sessão em todas, que são avisadas e vão ao login. Substitui o FR-013 da spec 004.
- Q: Quais durações padrão? → A: Token de acesso de 5 minutos, limite de inatividade de 15 minutos e
  limite absoluto de 8 horas desde o login, todos configuráveis por ambiente.
- Q: Spec única ou separada em backend e frontend? → A: Spec única, cobrindo servidor e app.
- Q: O que conta como atividade para o limite de inatividade? → A: Só chamadas à API; o app renova sob
  demanda (recusa por não autenticado ou recarga da página), sem renovação proativa. Um minuto antes do
  fim por inatividade, e também antes do limite absoluto, o app avisa "Sua sessão vai expirar" com a opção
  "Continuar conectado" (que renova; no limite absoluto não há como estender).
- Q: Uma pessoa pode ter várias sessões ativas ao mesmo tempo? → A: Sim, sem limite: cada navegador ou
  dispositivo tem sua sessão independente, e um login em um não afeta os outros.
- Q: A sessão derrubada por reuso mostra mensagem diferente de "Sua sessão expirou"? → A: Não. Resposta e
  tela são idênticas às de sessão expirada; o reuso aparece só no registro de segurança e nas métricas.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Continuar usando o app sem ser deslogado (Priority: P1)

Uma pessoa que entrou e está usando a carteira continua na sessão além da validade do token de acesso,
sem digitar a senha de novo e sem perder o que estava fazendo. A renovação acontece em segundo plano:
quando o token de acesso vence, o app obtém um novo e repete a ação que tinha sido recusada.

**Why this priority**: é a dor mais visível hoje (deslogar após 1 hora em uso e perder a operação em
andamento) e é a base das demais histórias: sem renovação não há como encurtar o token de acesso.

**Independent Test**: entrar, aguardar o vencimento do token de acesso (configurado curto no teste),
fazer uma consulta e uma transferência e verificar que ambas concluem sem voltar ao login e sem perder os
dados preenchidos.

**Acceptance Scenarios**:

1. **Given** uma pessoa com sessão ativa e token de acesso vencido, **When** realiza qualquer ação,
   **Then** a sessão é renovada sem interação e a ação conclui normalmente, uma única vez.
2. **Given** uma pessoa com uma transferência em revisão ou um pagamento com chave de idempotência,
   **When** a renovação acontece durante o fluxo, **Then** os dados da operação e a chave de idempotência
   são preservados e a operação não é duplicada.
3. **Given** uma pessoa com sessão ativa, **When** recarrega a página, **Then** continua na sessão na
   mesma tela, sem novo login.
4. **Given** várias chamadas recusadas ao mesmo tempo por token vencido, **When** o app renova a sessão,
   **Then** ocorre uma única renovação e todas as chamadas são repetidas com o novo token.
5. **Given** uma renovação que falha (sessão revogada, vencida ou inexistente), **When** a pessoa realiza
   uma ação, **Then** volta ao login com o aviso "Sua sessão expirou. Entre novamente." (comportamento
   atual da spec 004, FR-011).

---

### User Story 2 - Sair encerra a sessão de verdade (Priority: P1)

Ao escolher "Sair", a sessão é encerrada no servidor: a credencial de renovação deixa de valer e é
removida do navegador. Quem obtiver essa credencial depois do logout não consegue renovar a sessão.

**Why this priority**: hoje "Sair" é apenas local; sem revogação no servidor, um vazamento não pode ser
contido, o que é inaceitável em um app bancário.

**Independent Test**: entrar, guardar a credencial de renovação, sair e tentar renovar com ela; a
renovação é recusada e a pessoa precisa entrar de novo.

**Acceptance Scenarios**:

1. **Given** uma pessoa com sessão ativa, **When** escolhe "Sair", **Then** a sessão é revogada no
   servidor, a credencial de renovação é removida do navegador e a pessoa vai para a tela de login.
2. **Given** uma sessão encerrada por "Sair", **When** alguém tenta renová-la com a credencial antiga,
   **Then** a renovação é recusada e nenhum novo token é emitido.
3. **Given** uma falha de rede ao chamar o encerramento no servidor, **When** a pessoa escolhe "Sair",
   **Then** a sessão é descartada no navegador mesmo assim e a pessoa vai para o login.
4. **Given** um token de acesso emitido antes do "Sair", **When** é usado depois do logout, **Then** ele
   continua aceito apenas até expirar (no máximo a duração do token de acesso); a revogação imediata do
   token de acesso está fora de escopo.

---

### User Story 3 - Credencial roubada é detectada e a sessão é derrubada (Priority: P2)

Cada renovação troca a credencial de renovação por uma nova e invalida a anterior (rotação). Se uma
credencial já trocada for usada de novo, o sistema entende que ela pode ter sido copiada e revoga a
sessão inteira, exigindo novo login de quem estiver com ela.

**Why this priority**: limita o tempo útil de uma credencial vazada, mas depende da renovação (US1)
existir.

**Independent Test**: entrar, renovar uma vez guardando a credencial antiga, tentar renovar com a antiga
e verificar que a nova também deixou de funcionar.

**Acceptance Scenarios**:

1. **Given** uma credencial de renovação válida, **When** é usada para renovar, **Then** uma nova
   credencial é emitida e a anterior deixa de ser aceita.
2. **Given** uma credencial já rotacionada (fora da janela de tolerância), **When** é apresentada para
   renovação, **Then** a renovação é recusada e todas as credenciais daquela sessão são revogadas.
3. **Given** duas renovações quase simultâneas com a mesma credencial (duas abas, ou uma resposta perdida
   na rede) dentro da janela de tolerância, **When** ambas chegam ao servidor, **Then** nenhuma é tratada
   como reuso e a pessoa continua na sessão.
4. **Given** uma sessão revogada por reuso, **When** a pessoa realiza uma ação, **Then** volta ao login com
   o aviso de sessão expirada.

---

### User Story 4 - A sessão termina por inatividade ou duração máxima (Priority: P2)

Uma sessão sem uso por um período termina sozinha, e toda sessão tem uma duração máxima a partir do
login, mesmo que a pessoa continue ativa. Os dois limites são configuráveis.

**Why this priority**: sem limites, a renovação tornaria a sessão eterna; os limites recolocam o controle
de risco que hoje vem do prazo fixo de 1 hora.

**Independent Test**: com limites curtos de teste, ficar inativo além do limite de inatividade e verificar
que a próxima ação leva ao login; em outra sessão, manter atividade contínua até o limite absoluto e
verificar o mesmo.

**Acceptance Scenarios**:

1. **Given** uma sessão sem renovação além do limite de inatividade, **When** a pessoa realiza uma ação,
   **Then** a renovação é recusada e a pessoa volta ao login com o aviso de sessão expirada.
2. **Given** uma sessão ativa que atingiu o limite absoluto desde o login, **When** a próxima renovação é
   pedida, **Then** ela é recusada mesmo com uso contínuo.
3. **Given** uma pessoa com a tela parada, **When** falta um minuto para o fim por inatividade, **Then** o
   app exibe "Sua sessão vai expirar" com "Continuar conectado"; ao escolher, a sessão é renovada e a
   pessoa continua na mesma tela, sem perder dados.
4. **Given** uma sessão a um minuto do limite absoluto, **When** o aviso aparece, **Then** informa que será
   preciso entrar de novo e não oferece extensão.
5. **Given** credenciais de renovação vencidas ou revogadas, **When** passa o período de retenção,
   **Then** são removidas do armazenamento do servidor.

---

### Edge Cases

- **Várias abas abertas**: renovações disparadas ao mesmo tempo por abas diferentes MUST resultar em uma
  única rotação efetiva, sem derrubar a sessão por falso reuso (FR-008 e FR-015); "Sair" em uma aba
  leva as demais ao login (FR-016).
- **Resposta da renovação perdida**: a rede cai depois que o servidor rotacionou; a nova tentativa com a
  credencial antiga dentro da janela de tolerância MUST NOT ser tratada como reuso.
- **Renovação em andamento durante "Sair"**: o logout prevalece. O encerramento revoga a sessão a partir de
  qualquer credencial dela, inclusive uma que acabou de ser trocada por uma renovação concorrente; uma
  renovação concluída depois MUST NOT restabelecer a sessão, nem no servidor nem no app.
- **Login de outra pessoa no mesmo navegador**: o novo login substitui a credencial de renovação anterior,
  e a sessão anterior é revogada no servidor. Uma aba que ainda tenha em memória a sessão de outra pessoa
  MUST descartá-la ao saber do novo login.
- **Navegador que bloqueia cookies**: o login funciona, mas a sessão dura apenas até o token de acesso
  vencer ou até a página ser recarregada; a partir daí a pessoa vai ao login com o aviso de sessão expirada,
  como em qualquer recusa de renovação.
- **Usuário excluído**: a exclusão apaga as sessões, e a renovação é recusada.
- **Relógio do navegador errado**: o app não decide a validade pela hora local; recorre à renovação quando
  a API recusa a chamada.
- **401 na própria chamada de renovação ou de login**: MUST NOT disparar nova renovação (sem laço).
- **Chamada repetida após renovação também recusada**: a sessão é encerrada (uma única repetição por
  chamada).

## Requirements *(mandatory)*

### Functional Requirements

**Tokens e credenciais**

- **FR-001**: O login MUST emitir um token de acesso de curta duração e iniciar uma sessão no servidor,
  entregando ao navegador uma credencial de renovação.
- **FR-002**: O token de acesso MUST durar 5 minutos; a sessão MUST terminar após 15 minutos sem
  renovação (inatividade) ou 8 horas após o login (limite absoluto). Os três valores são configuráveis por
  ambiente.
- **FR-003**: A credencial de renovação MUST ser um valor aleatório e opaco, sem dados da pessoa,
  inacessível a scripts da página, enviada apenas às operações de autenticação do próprio site e nunca
  a terceiros.
- **FR-004**: O servidor MUST guardar apenas uma representação irreversível (hash) da credencial de
  renovação; o valor original MUST NOT ser persistido.
- **FR-005**: O token de acesso MUST ficar apenas na memória da página; nenhum token MUST ser gravado no
  armazenamento local ou de sessão do navegador.

**Renovação**

- **FR-006**: MUST existir uma operação de renovação que, com uma credencial de renovação válida, emite um
  novo token de acesso e uma nova credencial de renovação, invalidando a anterior de forma atômica.
- **FR-007**: O uso de uma credencial já rotacionada, fora da janela de tolerância, MUST revogar todas as
  credenciais da mesma sessão (família) e recusar a renovação.
- **FR-008**: MUST existir uma janela de tolerância curta (padrão: 30 segundos, configurável) em que a
  reapresentação da credencial recém-rotacionada não é tratada como reuso e devolve um resultado válido
  para a mesma sessão, sem criar uma segunda família.
- **FR-009**: A renovação MUST ser recusada quando a sessão estiver revogada, quando a credencial estiver
  vencida, quando o limite de inatividade ou o limite absoluto tiver sido atingido, ou quando o usuário não
  existir mais (a exclusão do usuário apaga as suas sessões; o sistema não tem desativação de usuário).
- **FR-010**: Toda recusa de renovação MUST responder com um único tipo de erro padronizado de
  "não autenticado", sem revelar o motivo (vencida, revogada ou reuso), e MUST remover a credencial do
  navegador. No app, todas essas recusas, inclusive o reuso, levam ao login com o mesmo aviso "Sua sessão
  expirou. Entre novamente."; o motivo fica apenas no registro de segurança (FR-021).

**Encerramento**

- **FR-011**: MUST existir uma operação de encerramento ("Sair") que revoga a sessão no servidor (identificada
  por qualquer credencial dela, ativa ou já trocada) e remove a credencial do navegador; ela MUST ser idempotente e responder com sucesso mesmo sem sessão válida.
- **FR-012**: Um novo login no mesmo navegador MUST substituir a credencial de renovação e revogar a sessão
  anterior apresentada por esse navegador.
- **FR-012a**: Uma pessoa MAY ter quantas sessões simultâneas quiser, uma por navegador ou dispositivo;
  login, renovação, revogação por reuso e "Sair" afetam apenas a sessão envolvida, nunca as outras sessões
  da mesma pessoa.
- **FR-013**: Sessões encerradas (revogadas ou vencidas), com todas as suas credenciais, MUST ser removidas
  periodicamente do servidor após um período de retenção (padrão: 7 dias após o vencimento ou a revogação,
  para investigação). As credenciais já trocadas de uma sessão ativa ficam até a sessão terminar, pois são
  necessárias para detectar o reuso.

**Comportamento no app (altera a spec 004)**

- **FR-014**: Ao receber uma recusa por não autenticado em uma chamada protegida, o app MUST renovar a
  sessão e repetir a chamada uma única vez; se a renovação falhar, MUST encerrar a sessão e levar ao login
  com o aviso de sessão expirada (substitui o comportamento da spec 004, FR-011, que encerrava direto).
- **FR-015**: MUST haver no máximo uma renovação em andamento por vez; chamadas concorrentes aguardam o
  resultado dela. Entre abas, a coordenação MUST evitar renovações simultâneas sempre que a sessão for
  compartilhada.
- **FR-016**: A sessão MUST ser compartilhada entre as abas do mesmo navegador (substitui a spec 004,
  FR-013): uma nova aba ou um link aberto em nova aba já entra na sessão; "Sair" em uma aba MUST encerrar a
  sessão nas demais, que vão ao login; e um login em uma aba vale para as outras. Uma aba com a sessão de
  outra pessoa em memória MUST descartá-la quando outra aba entrar com uma pessoa diferente.
- **FR-016a**: Atividade, para o limite de inatividade, é uma renovação; o app MUST renovar apenas sob
  demanda (recusa por não autenticado, recarga ou "Continuar conectado"), nunca por temporizador. O login
  e cada renovação MUST informar ao app quando a sessão vence por inatividade e pelo limite absoluto.
- **FR-016b**: Um minuto antes do fim por inatividade (ou na metade do prazo, quando ele for menor que dois
  minutos, como nos testes ponta a ponta), o app MUST exibir o aviso "Sua sessão vai expirar"
  com "Continuar conectado" (renova e fecha o aviso) e "Sair"; sem resposta, a sessão termina e a pessoa
  vai ao login com o aviso de sessão expirada. Um minuto antes do limite absoluto, o aviso MUST informar
  que será preciso entrar de novo, sem opção de estender. O aviso aparece em todas as abas e fecha em
  todas quando uma delas responde.
- **FR-017**: Ao abrir ou recarregar a página, o app MUST tentar renovar a sessão antes de exibir uma tela
  protegida; sem sessão válida, segue o fluxo atual de ir ao login e voltar à tela pedida.
- **FR-018**: A renovação MUST NOT apagar os dados da operação em andamento (transferência em revisão,
  chaves de idempotência de pagamentos e antecipações); esses dados só são descartados quando a sessão
  termina (logout, falha de renovação ou troca de pessoa).
- **FR-019**: "Sair" no app MUST chamar o encerramento no servidor e, independentemente do resultado,
  descartar a sessão local e levar ao login (substitui a spec 004, FR-012).

**Segurança e observabilidade**

- **FR-020**: Tokens de acesso, credenciais de renovação e cabeçalhos de cookie MUST NOT aparecer em logs,
  traces, métricas, mensagens de erro ou na documentação de exemplo.
- **FR-021**: O sistema MUST registrar como eventos de segurança, sem dados sensíveis, a detecção de reuso
  e a revogação de sessões, e MUST expor métricas de renovações concluídas, recusadas e reusos detectados.
- **FR-022**: A credencial de renovação MUST ser enviada somente em conexões seguras, inclusive no ambiente
  local (`localhost` é tratado como origem segura pelos navegadores suportados), e MUST NOT ser enviada em
  requisições originadas de outros sites.
- **FR-023**: As mudanças no contrato de autenticação MUST ser aditivas: os campos atuais da resposta de
  login são mantidos.

### Key Entities *(include if feature involves data)*

- **Sessão (família de credenciais)**: uma sessão iniciada por um login. Atributos: pessoa, início (base do
  limite absoluto), última renovação (base do limite de inatividade), situação (ativa ou revogada), motivo e
  momento da revogação (logout, reuso, novo login, expiração).
- **Credencial de renovação**: um elo da família. Atributos: hash do valor, sessão a que pertence, emissão,
  vencimento, momento em que foi rotacionada (e qual a substituiu) e revogação. Só a mais recente de cada
  sessão é aceita, salvo a janela de tolerância.
- **Token de acesso**: de curta duração, sem estado no servidor; identifica a pessoa e a sessão de origem.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Uma pessoa ativa continua na sessão por todo o limite absoluto sem nenhum novo login e sem
  perder nenhuma operação em andamento (0 operações perdidas ou duplicadas por renovação nos testes).
- **SC-002**: Em 100% das tentativas, uma credencial de renovação usada depois de "Sair" ou de uma
  revogação é recusada.
- **SC-003**: Em 100% das tentativas, o reuso de uma credencial rotacionada fora da janela de tolerância
  revoga a sessão inteira e exige novo login.
- **SC-004**: Em 100% das tentativas com várias abas ou chamadas simultâneas, a pessoa não é deslogada por
  falso reuso.
- **SC-005**: Depois de "Sair", um token de acesso vazado vale no máximo a duração do token de acesso
  (5 minutos, contra 1 hora hoje).
- **SC-006**: Nenhum token ou credencial é encontrado no armazenamento local ou de sessão do navegador,
  nem em texto puro no banco, nem na telemetria coletada durante os testes.
- **SC-007**: A renovação é imperceptível: a ação que a disparou conclui sem passo extra da pessoa.

## Assumptions

- **Spec única**: servidor e app na mesma spec, porque a rotação, a tolerância e a coordenação entre
  abas formam um único comportamento ponta a ponta.
- **Navegadores suportados**: os mesmos da spec 004; todos aceitam cookies seguros em `localhost` sem
  HTTPS, então não há exceção para o ambiente local.
- **Mesmo site**: o app e a API são servidos sob o mesmo site (proxy do ambiente local e de produção), o
  que permite enviar a credencial apenas a requisições do próprio site.
- **Janela de tolerância**: 30 segundos por padrão, prática comum para cobrir abas simultâneas e respostas
  perdidas sem abrir uma brecha relevante.
- **Prazos nos testes ponta a ponta**: o ambiente de e2e (CI e local) usa token de acesso de 10 s,
  inatividade de 25 s e tolerância de 5 s, para exercitar renovação e aviso em toda a suíte sem esperas
  longas; os padrões de produção continuam 5 min, 15 min, 8 h e 30 s.
- **Atualizações automáticas**: a única chamada feita pelo app sem ação da pessoa é a do detalhe de uma
  cobrança pendente, que se atualiza a cada 5 segundos apenas enquanto essa tela está aberta, a cobrança
  continua pendente e a aba está visível (spec 015). Ela conta como atividade, porque o servidor não a
  distingue; o limite absoluto continua valendo.
- **Retenção**: credenciais vencidas ou revogadas ficam 7 dias para investigação e depois são removidas.
- **Registro**: o cadastro continua entrando automaticamente pelo fluxo de login (spec 004, FR-008), que
  passa a emitir a credencial.
- **Fora de escopo**: "lembrar de mim", tela de gestão de dispositivos ou sessões, recuperação e troca de
  senha, MFA, revogação imediata do token de acesso e encerramento de todas as sessões da pessoa.
- **Roadmap**: esta spec vem antes da refatoração em monólito modular (#58); as novas peças devem ficar
  dentro da fronteira do futuro módulo de Identidade (#62), sem antecipar aquela refatoração.
