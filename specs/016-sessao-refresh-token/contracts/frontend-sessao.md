# Contrato do frontend: sessão (altera a spec 004)

**Feature**: [../spec.md](../spec.md) | **Base**: [specs/004-frontend-base/contracts/frontend.md](../../004-frontend-base/contracts/frontend.md)

## Requisitos da spec 004 substituídos

| Spec 004 | Passa a ser |
| --- | --- |
| FR-009 (sessão expira em 3600 s) | Token de acesso de 5 min renovado sob demanda; sessão vence por inatividade (15 min) ou limite absoluto (8 h) |
| FR-011 (401 encerra a sessão) | 401 → renova e repete uma vez; só encerra se a renovação falhar |
| FR-012 ("Sair" só no navegador) | "Sair" chama `POST /autenticacao/sair` e encerra localmente em qualquer caso |
| FR-013 (sessão restrita à aba) | Sessão compartilhada entre as abas do navegador |

## Comportamentos

| Situação | Resultado esperado |
| --- | --- |
| Abrir ou recarregar a página | Tela de carregamento enquanto renova; com sessão, segue na rota pedida; sem sessão, login com retorno à rota |
| Chamada protegida recebe 401 | Uma renovação (compartilhada por chamadas concorrentes e por abas) e uma repetição da chamada com o mesmo `Idempotency-Key` |
| Renovação recusada | Login com "Sua sessão expirou. Entre novamente." (também no reuso) |
| 401 em `/autenticacao/*` | Nunca dispara renovação |
| Entrar em uma aba | Abas em login/cadastro seguem para o início; abas com sessão de outra pessoa descartam a sessão e renovam |
| "Sair" em uma aba | Todas as abas vão ao login, sem aviso de expiração |
| 60 s antes do fim por inatividade (metade do prazo, se menor que 2 min) | Modal "Sua sessão vai expirar" com "Continuar conectado" (renova) e "Sair", em todas as abas; responder em uma fecha nas outras |
| 60 s antes do limite absoluto (mesma regra) | Modal "Sua sessão vai terminar" informando que será preciso entrar de novo, só com "Entendi" |
| Prazo chega a zero | Login com aviso de sessão expirada |

## Textos

| Chave | Texto |
| --- | --- |
| Título do aviso de inatividade | Sua sessão vai expirar |
| Corpo do aviso de inatividade | Por segurança, você sairá em {segundos} s se não houver atividade. |
| Botões | Continuar conectado · Sair |
| Título do aviso de limite absoluto | Sua sessão vai terminar |
| Corpo do aviso de limite absoluto | Por segurança, a sessão tem duração máxima. Você precisará entrar de novo em {segundos} s. |
| Botão | Entendi |

O contador é anunciado a leitores de tela só na abertura e a cada 15 s (`aria-live="polite"`), para não
sobrecarregar. O modal reaproveita `Confirmacao` e é validado em desktop e mobile.

## Armazenamento

- Nenhum token em `localStorage`, `sessionStorage`, IndexedDB, URL ou log.
- A chave `bank.sessao` de versões anteriores é removida do `sessionStorage` na primeira carga.
- `bank.operacoes` e os dados de transferência continuam por aba e só são apagados no fim da sessão.
