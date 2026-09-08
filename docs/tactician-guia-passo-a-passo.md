# Tactician — Guia Passo a Passo (do repo vazio ao docker compose local)

## Como usar este guia
Cada etapa tem 3 partes: **conceito** (o que você precisa entender antes de codar), **o que fazer** (passos concretos, sem te dar o código pronto — você vai escrever a lógica), e **definição de pronto** (um teste manual simples que prova que a etapa funcionou antes de você seguir pra próxima).

Regra de ouro: **não pule etapa**. A tentação vai ser ir direto pro Kafka porque é a parte "chique", mas cada etapa isolada te dá uma vitória rápida e te impede de debugar 3 problemas ao mesmo tempo (é código, é infra, é Kafka?). Etapas 0–6 nem tocam em Kafka de propósito.

Escopo deste guia: do repo vazio até `docker compose up` rodando os dois serviços (Combat e Stats) localmente, com um fluxo completo funcionando. Deploy em nuvem fica pra depois, como combinado.

---

## Etapa 0 — Repositório e estrutura de pastas

**Conceito: monorepo vs multi-repo.** Na sua empresa provavelmente cada microsserviço tem seu próprio repositório — faz sentido lá, porque times diferentes fazem deploy independente. Pra você, solo, isso só criaria fricção (PRs cruzados, versionar contratos compartilhados entre repos, etc). Use **monorepo**: um repositório `tactician`, uma pasta por serviço. Isso não te impede de aprender o padrão de múltiplos serviços — só remove a dor operacional de multi-repo que não te ensina nada de arquitetura, só de DevOps.

**O que fazer:**
1. Criar o repositório `tactician` no GitHub (público, é seu portfólio).
2. Clonar localmente e criar a estrutura de pastas:
   ```
   tactician/
     src/
       services/
         Combat/          (fica vazio por enquanto)
         Stats/           (fica vazio por enquanto)
       shared/
         Tactician.Shared.Contracts/   (DTOs de evento Kafka compartilhados entre serviços)
     docker/
       docker-compose.yml
     docs/
       (jogue aqui os documentos de arquitetura que já produzimos)
   ```
3. Adicionar um `.gitignore` de .NET (`dotnet new gitignore` gera um bom padrão).
4. Criar um README inicial com um parágrafo descrevendo o projeto e um diagrama simples (pode ser texto mesmo, tipo o que já temos no doc de arquitetura).
5. Fluxo de trabalho que eu recomendo pro resto do projeto: uma branch curta por etapa deste guia (`feature/etapa-2-domain-combat`), merge na `main` só quando a "definição de pronto" da etapa bater. Isso te dá um histórico de commits organizado — ótimo pra quem for olhar o repo depois.
6. Opcional, mas ajuda a não se perder: crie um **GitHub Project (board)** com uma issue por etapa deste guia.

**Definição de pronto:** repo criado, primeiro commit com a estrutura de pastas e README, branch `main` protegida (ou pelo menos com o hábito de não commitar direto nela).

---

## Etapa 1 — Solução .NET e as camadas do Clean Architecture

**Conceito.** Clean Architecture organiza o código em camadas onde a dependência **só aponta pra dentro**:
- `Domain`: entidades e regras de negócio puras. Não referencia nenhum pacote externo (nada de EF Core, nada de Kafka). Se você conseguir copiar essa pasta pra outro projeto sem instalar nenhum NuGet, está certo.
- `Application`: casos de uso (o que o sistema *faz*). Conhece o `Domain`, define **interfaces** para tudo que é externo (ex: `IActionRepository`, `IEventPublisher`) mas não implementa nada.
- `Infrastructure`: implementações concretas das interfaces do `Application` — EF Core, Kafka producer/consumer, etc.
- `Api` / `Worker`: os pontos de entrada. Conhecem tudo, "montam" as peças (Dependency Injection) e não têm regra de negócio nenhuma dentro deles.

Isso é diferente de um CRUD simples onde o Controller já faz tudo — aqui o objetivo explícito é conseguir trocar o Postgres por outro banco, ou o Kafka por outro broker, sem tocar no `Domain` nem no `Application`.

**O que fazer:**
1. Dentro de `src/services/Combat`, criar os projetos:
   ```
   dotnet new classlib -n Tactician.Combat.Domain
   dotnet new classlib -n Tactician.Combat.Application
   dotnet new classlib -n Tactician.Combat.Infrastructure
   dotnet new webapi   -n Tactician.Combat.Api
   dotnet new worker    -n Tactician.Combat.Worker
   ```
2. Criar a solução e adicionar os projetos:
   ```
   dotnet new sln -n Tactician
   dotnet sln add src/services/Combat/**/*.csproj
   ```
3. Configurar as referências entre projetos na direção certa: `Application` → referencia `Domain`. `Infrastructure` → referencia `Application`. `Api` e `Worker` → referenciam `Application` e `Infrastructure`. **`Domain` não referencia nada.**
4. Criar também `Tactician.Shared.Contracts` (classlib) em `src/shared` — vai guardar os DTOs de eventos Kafka (`ActionRegisteredEvent`, `AttackResolvedEvent` etc.) que tanto o `Combat` quanto, futuramente, o `Stats` vão precisar conhecer.

**Definição de pronto:** `dotnet build` na solução inteira compila sem erro, mesmo com os projetos praticamente vazios.

---

## Etapa 2 — Domain layer do Combat (sem nenhuma dependência externa)

**Conceito.** É aqui que você modela `Campaign`, `Player`, `NPC` como classes puras (entidades e, se quiser praticar, value objects para coisas como `HitPoints` ou `ArmorClass` que têm regras próprias, tipo "não pode ser negativo"). Comece **só** com `Campaign`, `Player`, `NPC` — deixe `Combat`, `Turn`, `Action` pra depois. Você quer uma vitória rápida: compilar, testar, e sentir o ciclo funcionando antes de entrar na parte mais complexa do domínio.

**O que fazer:**
1. Criar as classes `Campaign`, `Player`, `NPC` no projeto `Domain`, com as regras básicas que você já definiu no modelo (vida, nível, CA, tipo/nível de desafio).
2. Adicionar um projeto de testes xUnit (`Tactician.Combat.Domain.Tests`) e escrever pelo menos 2-3 testes de regra de domínio simples (ex: criar um `Player` com vida negativa deve lançar exceção).
3. Esse é um bom momento pra você praticar TDD se quiser: escrever o teste antes da regra, já que são regras pequenas e isoladas.

**Definição de pronto:** testes de domínio passando, sem tocar em EF Core, Kafka ou qualquer infraestrutura.

---

## Etapa 3 — Docker Compose com Postgres + Kafka (antes de terminar o C#)

**Conceito.** Validar a infraestrutura "vazia" primeiro separa dois tipos de bug que, se aparecerem juntos, são muito mais difíceis de diagnosticar: bug de infraestrutura (Kafka mal configurado, rede do Docker) vs bug de código (sua lógica C#). Fazendo nessa ordem, quando você conectar o C# na Etapa 7, qualquer problema é problema de código — a infra já foi validada isoladamente.

Use **Redpanda** em vez de Kafka+Zookeeper — é compatível com o protocolo Kafka, muito mais leve pra rodar localmente, e vem com uma console web (Redpanda Console) que te deixa ver tópicos e mensagens visualmente, o que ajuda muito enquanto você aprende.

**O que fazer:**
1. Escrever `docker/docker-compose.yml` com serviços `postgres` e `redpanda` (+ `redpanda-console` opcional).
2. Subir com `docker compose up -d`.
3. Conectar no Postgres com um cliente (DBeaver, Azure Data Studio, ou `psql`) só pra confirmar que subiu.
4. Criar um tópico manualmente (via Redpanda Console ou `rpk topic create`) e publicar/consumir uma mensagem de teste manualmente, sem nenhuma linha de C# envolvida.

**Definição de pronto:** você consegue publicar uma mensagem de teste num tópico e vê-la aparecer no consumo, tudo via ferramentas gráficas/CLI, sem código seu.

---

## Etapa 4 — Infrastructure: EF Core + Postgres

**Conceito.** `DbContext`, migrations, e por que a connection string vem de configuração (appsettings + variável de ambiente), nunca hardcoded. Vale também decidir aqui se você quer o padrão Repository explícito (interface no `Application`, implementação no `Infrastructure`) ou usar o `DbContext` diretamente via interface — ambos são defensáveis num projeto de estudo; eu recomendo Repository explícito porque é o que vai te obrigar a manter o `Application` sem saber que EF Core existe.

**O que fazer:**
1. Criar o `CombatDbContext` no `Infrastructure`, mapeando `Campaign`, `Player`, `NPC`.
2. Rodar a primeira migration (`dotnet ef migrations add InitialCreate`) e aplicá-la no Postgres do compose (`dotnet ef database update`).
3. Implementar o repositório concreto (`CampaignRepository` etc.) satisfazendo a interface definida no `Application` (você vai criar essa interface na próxima etapa, ou já adianta aqui — tanto faz, contanto que a implementação fique no `Infrastructure`).

**Definição de pronto:** tabelas aparecem no Postgres do docker compose depois do `database update`.

---

## Etapa 5 — Application: casos de uso, ainda sem Kafka

**Conceito: CQRS leve.** Você não precisa de um framework pesado — pode usar **MediatR** (biblioteca popular pra separar Commands/Queries em classes explícitas com handlers) ou fazer na mão com classes de serviço simples. Pra um projeto de estudo, MediatR vale a pena porque é o que você provavelmente vai encontrar em projetos reais também.

Nesta etapa, o handler do comando ainda vai gravar **direto no banco**, sem publicar nada no Kafka — de propósito. Você quer primeiro fechar o ciclo Application → Infrastructure → Postgres antes de meter mensageria assíncrona no meio. Isso é uma etapa "descartável": na Etapa 7 você vai mudar esse handler pra publicar um evento em vez de gravar direto.

**O que fazer:**
1. Definir um comando simples, por exemplo `CreatePlayerCommand`, com handler que usa o repositório da Etapa 4 pra gravar.
2. Definir uma query simples, `GetCampaignQuery`, que lê direto (sem handler de escrita no meio).
3. Escrever um teste de integração que chama a Application layer diretamente (sem subir Api nem Worker) e confirma que grava no Postgres do compose.

**Definição de pronto:** teste de integração passando, gravando e lendo do Postgres real (do docker compose), sem nenhuma Api ou Worker rodando ainda.

---

## Etapa 6 — Api: primeiro ciclo completo (ainda sem Kafka)

**Conceito.** Esta é uma etapa transitória de propósito: a Api vai chamar a Application layer diretamente pra fechar o ciclo HTTP → banco, sem Kafka. Você vai literalmente violar a regra "Api só faz GET" por uma etapa só, pra separar dois aprendizados: "eu sei fazer uma Api em Clean Architecture" (esta etapa) de "eu sei desacoplar comando de leitura via mensageria" (próxima etapa). Prefira **Minimal API** em vez de Controllers — é o padrão mais atual do .NET e tem menos boilerplate, o que ajuda a enxergar melhor a estrutura.

**O que fazer:**
1. Endpoint `POST /campaigns/{id}/players` chamando `CreatePlayerCommand` via MediatR, direto (sem Kafka ainda).
2. Endpoint `GET /campaigns/{id}` chamando a query direto no banco.
3. Testar via Swagger/Postman/curl.

**Definição de pronto:** você cria uma campanha e um player via `POST`, e consegue ver via `GET`, tudo local, sem Kafka.

---

## Etapa 7 — Kafka de verdade: producer na Api, consumer no Worker

**Conceito.** Agora sim: Kafka entra no meio. Conceitos centrais pra entender antes de codar:
- **At-least-once delivery**: uma mensagem pode ser entregue mais de uma vez (por isso idempotência vai ser necessária logo na próxima etapa).
- **Consumer group**: o Worker se inscreve num grupo; se você rodar duas instâncias do Worker, o Kafka distribui as partições entre elas — é assim que você escalaria horizontalmente depois.
- **Offset**: a posição de leitura do consumer no tópico. Committar o offset cedo demais (antes de processar) pode perder mensagens; tarde demais (antes de commitar mas processar de novo) causa duplicação — outro motivo pra idempotência.
- **Por que Worker é um `BackgroundService`, não uma Api**: ele não responde a requisições HTTP, só fica consumindo o tópico em loop. O template `dotnet new worker` já te dá essa estrutura.

**O que fazer:**
1. Adicionar o pacote `Confluent.Kafka` no `Infrastructure` da Api (producer) e do Worker (consumer).
2. Mudar o endpoint `POST` da Etapa 6: em vez de chamar o `CreatePlayerCommand` direto, ele agora publica um evento (`PlayerRegistrationRequested`, por exemplo) no tópico e retorna `202 Accepted`.
3. No Worker, implementar o consumer que lê esse tópico e delega pro mesmo handler da Application layer que você já tinha na Etapa 5 (a lógica de negócio não muda — só quem chama ela muda).
4. Repita esse fluxo depois pra `ActionRegistered` quando chegar em `Combat`/`Turn`/`Action` (Etapa 10).

**Definição de pronto:** o `POST` na Api não grava mais nada diretamente — o dado só aparece no `GET` alguns milissegundos depois, depois de passar pelo Kafka e ser processado pelo Worker. Esse pequeno delay é esperado: é a consistência eventual acontecendo de verdade, não um bug.

---

## Etapa 8 — Idempotência (tabela de inbox / dedupe)

**Conceito revisitado do documento de arquitetura:** cada mensagem carrega um `EventId`. O Worker verifica (numa tabela `ProcessedEvents`, na mesma transação da escrita de negócio) se aquele `EventId` já foi processado; se sim, descarta.

**O que fazer:**
1. Criar a tabela/entidade `ProcessedEvent` (EventId, ProcessedAt).
2. No handler do Worker, abrir uma transação que: (a) checa/insere o EventId na tabela de controle, (b) só então aplica a escrita de negócio. Se a inserção do EventId falhar (já existe), aborta sem aplicar a escrita.
3. **Testar a duplicação de propósito**: publique manualmente a mesma mensagem duas vezes (mesmo `EventId`) via Redpanda Console, ou force o Worker a reprocessar um offset antigo, e confirme que o estado não duplica.

**Definição de pronto:** reprocessar a mesma mensagem (mesmo EventId) não altera o estado da segunda vez.

---

## Etapa 9 — Logging estruturado (Serilog) e correlação

**Conceito.** Log estruturado (JSON) permite filtrar/pesquisar por campo (ex: `combatId`), o que `Console.WriteLine` não permite. `CorrelationId` é um identificador que nasce na Api e viaja até o Worker (via header da mensagem Kafka) pra você conseguir juntar os logs dos dois processos que pertencem à mesma requisição do usuário.

**O que fazer:**
1. Adicionar Serilog na Api e no Worker, com sink de console em JSON (e, se quiser, arquivo).
2. Gerar um `CorrelationId` (GUID) na Api a cada request, colocar como header da mensagem Kafka.
3. No Worker, ler esse header e usar como `LogContext` do Serilog pro resto do processamento daquela mensagem.

**Definição de pronto:** você consegue pegar um log da Api e um log do Worker referentes ao mesmo request e ver que compartilham o mesmo `CorrelationId`.

---

## Etapa 10 — Domínio completo de combate: Combat, Turn, Action (Attack/Cure)

Agora sim entra a parte rica do domínio que o fluxograma revelou: `Combat` com snapshot de participantes, `Turn`, `Action` com os subtipos `Attack` (acerto/erro normal/crítico, dano) e `Cure` (normal com overheal, temporária), e a regra de `isDown`/fim de combate.

**O que fazer:**
1. Modelar essas entidades no `Domain` primeiro, com testes unitários das regras (ex: cura normal não pode passar da vida máxima; cura temporária não acumula).
2. Isolar a lógica de resolução (quem acertou, quanto de dano, se ficou isDown) num serviço de domínio dedicado (ex: `CombatResolutionService`) — assim ele é testável sem precisar de EF Core ou Kafka no meio do teste.
3. Repetir o padrão das Etapas 4–8 pra esse fluxo: Infrastructure (migration nova), Application (`RegisterActionCommand`), Api (endpoint publica `ActionRegistered`), Worker (consome, aplica a regra via `CombatResolutionService`, grava, decide fim de combate).

**Definição de pronto:** você consegue simular um combate completo via `POST`s sucessivos de actions e ver o combate encerrar (`CombatEnded`) quando os NPCs ficam isDown.

---

## Etapa 11 — Outbox pattern

**Conceito revisitado:** o Worker precisa gravar a Action resolvida no Postgres **e** publicar `AttackResolved`/`CureResolved`/`CombatEnded` de forma atômica. Sem isso, um crash entre os dois passos deixa o sistema inconsistente (gravou mas não publicou, ou vice-versa).

**O que fazer:**
1. Criar tabela `OutboxMessages` no banco do Combat.
2. Na mesma transação da escrita de negócio, inserir a mensagem a ser publicada nessa tabela (em vez de publicar direto no Kafka).
3. Criar um processo separado (um `BackgroundService` simples fazendo polling a cada X segundos) que lê mensagens não publicadas da tabela, publica no Kafka, e marca como publicadas.

**Definição de pronto:** simule um crash do Worker logo depois de gravar (antes de publicar) e confirme que, ao reiniciar, a mensagem pendente na Outbox ainda é publicada.

---

## Etapa 12 — Segundo serviço: Stats

Repita a estrutura de Etapas 1, 4, 5, 6, 7 e 8 (Domain → Infrastructure → Application → Api → Worker consumidor + idempotência), mas agora para o serviço `Stats`, que:
- Consome `combat.results.v1` (não `combat.actions.v1` — Stats reage a fatos, não a comandos).
- Mantém projeções agregadas por `Combat` e por `Campaign` (dano total por participante, maior crítico etc).
- Expõe só `GET /combats/{id}/stats` e `GET /campaigns/{id}/stats`.

Essa etapa deve ser bem mais rápida que a primeira vez — é o mesmo padrão, e essa repetição é proposital: é isso que fixa o aprendizado de "como estruturar um novo microsserviço do zero" na prática.

**Definição de pronto:** depois de um combate ser processado pelo `Combat`, o `GET /combats/{id}/stats` do `Stats` reflete os números corretos (com um pequeno delay esperado).

---

## Etapa 13 — Docker Compose com tudo junto

**O que fazer:**
1. Criar um `Dockerfile` multi-stage pra cada Api e cada Worker (build + runtime).
2. Expandir o `docker-compose.yml` da Etapa 3 pra incluir todos os 4 processos (`Combat.Api`, `Combat.Worker`, `Stats.Api`, `Stats.Worker`) além de Postgres e Redpanda.
3. Rodar `docker compose up --build` e testar o fluxo completo via Postman/curl/Swagger: criar campanha, players, NPCs, iniciar combate, registrar actions, ver o combate encerrar, e ver as stats agregadas aparecerem no serviço `Stats`.

**Definição de pronto:** `docker compose up` sobe tudo com um único comando, e você consegue rodar o fluxo inteiro (do `POST` de criação de campanha até o `GET` de stats) sem rodar nada fora do Docker.

---

## O que fica de fora deste guia (de propósito)
- **Deploy em nuvem** — você decidiu tratar depois de ter uma versão utilizável local, e faz sentido: validar a arquitetura local primeiro te poupa de debugar rede/cloud e lógica de negócio ao mesmo tempo.
- **Autenticação real** — use um `PlayerId` fixo ou um JWT hardcoded por enquanto.
- **Tempo real (SignalR)** — é a Fase 2 do produto no documento de arquitetura, vem depois de tudo isso estar sólido.

## Sobre o fluxo de trabalho no GitHub ao longo do guia
- Uma branch curta por etapa (`feature/etapa-N-descricao`), merge na `main` quando a "definição de pronto" bater.
- Commits pequenos e descritivos — cada commit deveria, idealmente, deixar a solução compilando.
- Atualize o README a cada novo serviço/etapa relevante (principalmente o "como rodar localmente"), pra ele virar de fato a porta de entrada de quem olhar o repo depois.
