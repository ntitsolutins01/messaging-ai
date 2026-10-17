# Guia de Pastas — messaging-ai

> Tour completo por cada pasta e arquivo do projeto. Leia este documento uma vez antes de abrir qualquer código — você vai saber exatamente onde está cada coisa e por que ela existe.

---

## Mapa geral

```
messaging-ai/
│
├── 📄 Raiz                    configuração global do projeto
│
├── 📁 src/                    código-fonte da aplicação
│   ├── Shared/                tipos base sem dependências externas
│   ├── Contracts/             canal público entre módulos
│   ├── Modules/               lógica de negócio (Ingestion · Processing · Operations)
│   └── Hosts/                 ponto de entrada (API)
│
├── 📁 tests/                  todos os tipos de teste
│   ├── Messaging.TestSupport/ helpers compartilhados
│   ├── Messaging.UnitTests/   testes unitários
│   ├── Messaging.ArchitectureTests/ fronteiras entre módulos
│   ├── Messaging.IntegrationTests/  testes end-to-end da API
│   └── load/                  teste de carga com k6
│
├── 📁 specs/                  especificações escritas antes do código
├── 📁 docs/                   documentação arquitetural (ADRs, módulos, prompts)
├── 📁 evals/                  sistema de avaliação LLM-as-a-judge
├── 📁 scripts/                automação local (bootstrap, quality gate, ops)
├── 📁 observability/          configuração OTLP para telemetria
├── 📁 ops/                    automações de operação (Hermes Agent)
├── 📁 .claude/                cockpit do Claude Code (regras, agentes, MCPs, skills)
├── 📁 .github/                CI/CD e workflows automatizados
└── 📁 .cursor/                regras de contexto para o Cursor IDE
```

---

## 1. Raiz — arquivos de configuração global

Esses arquivos ficam na raiz e se aplicam ao projeto inteiro.

### `README.md`
Porta de entrada pública do projeto. Contém o fluxo completo, status de implementação, como rodar, invariantes, quality gate, referência de comandos e seção sobre o Claude Code. É o primeiro arquivo a ler.

### `CLAUDE.md`
Instruções do projeto para o Claude Code. Define o objetivo, o fluxo, a estrutura de módulos, os comandos disponíveis, as invariantes e como trabalhar. É carregado automaticamente em toda sessão do Claude Code neste diretório.

### `docker-compose.yml`
Sobe os três serviços de infraestrutura locais:
- **postgres:16** — banco de dados, porta `5432`, credenciais `poc/poc`, banco `messaging_poc`
- **rabbitmq:3-management** — broker AMQP (`5672`) com painel web de gerenciamento (`15672`)
- **apache/kafka:3.8.0** — broker Kafka KRaft (sem Zookeeper), porta `9092`

```bash
docker compose up -d   # sobe tudo
docker compose down    # derruba
```

### `docker-compose.observability.yml`
Sobe o stack de observabilidade separado (não obrigatório para desenvolver):
- **Elasticsearch 8.15.3** — armazena logs e traces
- **Kibana** — dashboard de visualização (`http://localhost:5601`)
- **OTLP Collector** — recebe telemetria da API e encaminha ao Elasticsearch

```bash
docker compose -f docker-compose.yml -f docker-compose.observability.yml up -d
```

### `Directory.Build.props`
Configuração MSBuild aplicada a **todos** os projetos .csproj do repositório. Define:
- `TargetFramework`: net8.0
- `LangVersion`: 12 (C# 12)
- `Nullable`: enable — nullability obrigatória
- `ImplicitUsings`: enable — `using` automáticos para namespaces comuns
- `TreatWarningsAsErrors`: ativado **apenas** quando `-p:QualityGate=true` (usado no CI e no `quality-gate.sh`)
- Referência ao `CodeMetricsConfig.txt` para análise de complexidade

### `global.json`
Fixa a versão do SDK .NET usada no projeto:
```json
{ "sdk": { "version": "8.0.100", "rollForward": "latestFeature" } }
```
Garante que todos usam o mesmo SDK, independente do que está instalado na máquina.

### `CodeMetricsConfig.txt`
Uma linha: `CA1502: 10`. Limita a **complexidade ciclomática** de qualquer método a 10. Métodos com mais de 10 caminhos de execução falham o build no quality gate. Evita métodos longos com múltiplos `if/switch/loops`.

### `.mcp.json`
Configura os **MCP servers** disponíveis no Claude Code. Três servidores:
- **postgres** — `@modelcontextprotocol/server-postgres` aponta para `postgresql://poc:poc@localhost:5432/messaging_poc`. Permite SQL direto nas tabelas.
- **kafka** — `mcp-kafka` via npx, aponta para `localhost:9092`. Ferramentas: `kafka_list_topics`, `kafka_consume_messages`, `kafka_get_group_lag`, etc.
- **rabbitmq** — servidor customizado em `.claude/mcp-servers/rabbitmq/index.js`, usa a Management HTTP API `http://localhost:15672`. Ferramentas: `rabbitmq_list_queues`, `rabbitmq_get_messages` (peek), `rabbitmq_publish_message`, etc.

### `.editorconfig`
Padrões de formatação de código (indentação, charset, trailing whitespace) para editores e IDEs. Garante consistência independente do editor usado.

### `.env.example`
Template de variáveis de ambiente. Nunca commitar o `.env` real — copiar este arquivo e preencher localmente.

### `.gitignore`
Exclui do versionamento: `bin/`, `obj/`, `.vs/`, `.idea/`, `*.user`, `.env`, `CLAUDE.local.md`, `.claude/settings.local.json`, `node_modules/` dos MCP servers, `TestResults/`, `coverage*/`, `Messaging.sln` (gerado pelo bootstrap), `*.zip`.

---

## 2. `src/Shared/` — tipos base sem dependências

### `Messaging.SharedKernel/`
O projeto mais fundamental. **Não pode depender de nada** externo — nenhum pacote NuGet de infraestrutura, nenhum módulo. Contém:

| Arquivo | O que faz |
|---|---|
| `IdempotencyKey.cs` | Record imutável que representa a chave de idempotência. Formato: `{provider}:{providerEventId}` em lowercase. Máx 200 chars. Valida na criação — nunca cria uma chave inválida. |
| `IClock.cs` | Interface `IClock { DateTimeOffset UtcNow }` + implementação `SystemClock`. Permite substituir o relógio nos testes (`FakeClock`) sem depender do sistema. |
| `Telemetry.cs` | `ActivitySource` para OpenTelemetry. Define o nome do source de traces usado na aplicação. |

**Por que existe separado?** Qualquer módulo pode referenciar SharedKernel sem criar dependência circular. É a base da pirâmide de dependências.

### `Messaging.Observability/`
Extensão opt-in de telemetria. Contém:

| Arquivo | O que faz |
|---|---|
| `ObservabilityExtensions.cs` | Método `AddMessagingObservability()`. **Só ativa** se a variável `OTEL_EXPORTER_OTLP_ENDPOINT` estiver definida. Configura traces (AspNetCore + HttpClient + source próprio), métricas e logs via OTLP. |

**Por que opt-in?** Testes e desenvolvimento local sem collector continuam funcionando normalmente — a telemetria não quebra nada quando o collector está ausente.

---

## 3. `src/Contracts/` — canal público entre módulos

### `Messaging.Contracts/`
O único meio legítimo de comunicação entre módulos. Nenhum módulo pode referenciar tipos internos de outro — só o que está aqui.

| Arquivo | O que faz |
|---|---|
| `Ingestion/RawEventAccepted.cs` | Evento de integração publicado pelo módulo Ingestion quando um webhook é aceito. Carrega: `EventId`, `IdempotencyKey`, `Provider`, `ReceivedAt`. O módulo Processing consome este contrato — nunca tipos de `Messaging.Ingestion`. |
| `Processing/IEventReprocessor.cs` | Interface com `ReprocessAsync(Guid eventId)`. É a porta pública do módulo Processing usada pelo módulo Operations. Retorna `ReprocessOutcome` (Requeued / AlreadyDone / NotFound). |

**Por que existe separado?** Se Ingestion referenciasse Processing diretamente, qualquer mudança interna de Processing quebraria Ingestion. Com Contracts no meio, os módulos são desacoplados e podem evoluir independentemente.

---

## 4. `src/Modules/` — lógica de negócio

### `Messaging.Ingestion/`
Responsável pelo fluxo de entrada: receber o webhook, validar, e gravar o evento bruto + outbox.

| Arquivo | O que faz |
|---|---|
| `WebhookSignatureValidator.cs` | Valida o cabeçalho `t=<unix>,v1=<hex>` onde `v1 = HMAC-SHA256(secret, "{t}.{corpo}")`. Usa `CryptographicOperations.FixedTimeEquals` para comparação em tempo constante (evita timing attack). Verifica janela de 5 minutos. Retorna `SignatureResult`. |
| `WebhookIngestionService.cs` | Caso de uso principal. Orquestra: valida assinatura → deriva `IdempotencyKey` → cria `RawEvent` + `RawEventAccepted` → chama `IRawEventStore.TryAddAsync`. Se já existir (duplicata), retorna `Duplicate` sem novo efeito. |
| `IRawEventStore.cs` | Porta (interface) para persistência. Define `TryAddAsync(RawEvent, RawEventAccepted, CancellationToken)` que deve gravar os dois na mesma transação. Retorna `bool` indicando se foi inserido ou era duplicata. |
| `InMemoryRawEventStore.cs` | Implementação em memória do `IRawEventStore`. Thread-safe com `lock`. Usada nos testes de integração e no desenvolvimento antes do PostgreSQL real. |
| `RawEvent.cs` | Record com os dados do evento bruto: `Id`, `IdempotencyKey`, `Provider`, `Payload`, `ReceivedAt`. |
| `SignatureResult.cs` | Enum com os resultados possíveis da validação: `Valid`, `Missing`, `Malformed`, `Mismatch`, `Expired`. |
| `IngestionOutcome.cs` | Enum com os resultados possíveis da ingestão: `Accepted`, `Duplicate`, `InvalidSignature`. |

### `Messaging.Processing/`
Responsável pelo ciclo de vida do processamento e pela detecção de eventos presos.

| Arquivo | O que faz |
|---|---|
| `EventStatus.cs` | Enum com os estados do evento: `Pending`, `Processing`, `Done`, `Failed`, `Dlq`. |
| `StuckEventDetector.cs` | Lógica pura de decisão para eventos presos. Dado um `EventState` e o tempo atual, decide: `None` (ainda não preso), `Requeue` (reinserir na fila) ou `MoveToDlq` (excedeu tentativas máximas). Completamente testável sem banco ou broker. |

### `Messaging.Operations/`
Responsável pelo reprocessamento manual de eventos da DLQ pela equipe de operações.

| Arquivo | O que faz |
|---|---|
| `ReprocessEventHandler.cs` | Caso de uso de reprocessamento. Recebe um `Guid eventId`, valida que não é vazio, e delega para `IEventReprocessor` (contrato do módulo Processing). Não conhece nenhum tipo interno de Processing. |

---

## 5. `src/Hosts/` — ponto de entrada

### `Messaging.Api/`
O host é apenas **composição**: registra DI, configura middleware, define endpoints. Sem regra de negócio.

| Arquivo | O que faz |
|---|---|
| `Program.cs` | Configura e sobe a API. Valida que `Webhook:Secret` existe (falha no startup se ausente). Limita o body a 1 MiB. Ativa observabilidade opt-in. Registra `IClock`, `IRawEventStore`, `WebhookSignatureValidator`, `WebhookIngestionService`. Middleware de `X-Correlation-Id`. Endpoints: `GET /health/live`, `GET /health/ready`, `POST /webhooks/{provider}`. |

**Endpoint `POST /webhooks/{provider}`:**
1. Valida o nome do provider (`[a-z0-9-]{1,50}`)
2. Lê o corpo completo em memória
3. Chama `WebhookIngestionService.IngestAsync`
4. Responde `401` se assinatura inválida, `200` em qualquer outro caso (inclusive duplicata)

---

## 6. `tests/` — todos os tipos de teste

### `Messaging.TestSupport/`
Helpers compartilhados por todos os projetos de teste.

| Arquivo | O que faz |
|---|---|
| `FakeClock.cs` | Implementação de `IClock` com horário controlável. Essencial para testar a janela de 5 minutos do validador de assinatura sem depender do relógio real. |
| `WebhookSigner.cs` | Gera cabeçalhos `X-Signature` válidos para testes. Replica a lógica de assinatura do provedor. Usado nos testes de integração para enviar requisições autenticadas. |

### `Messaging.UnitTests/`
Testes rápidos, sem IO, com fakes. Cobrem toda a lógica de domínio.

| Arquivo | O que cobre |
|---|---|
| `IdempotencyKeyTests.cs` | Validação de formato, normalização para lowercase, limite de tamanho, erros esperados. |
| `WebhookSignatureValidatorTests.cs` | Assinatura válida, ausente, malformada, adulterada, expirada. Usa `FakeClock`. |
| `WebhookIngestionServiceTests.cs` | Fluxo feliz (aceito), duplicata (200 sem novo efeito), assinatura inválida. |
| `StuckEventDetectorTests.cs` | Evento ainda no prazo (None), preso (Requeue), esgotado (MoveToDlq). |
| `ReprocessEventHandlerTests.cs` | EventId vazio (erro), delegação ao `IEventReprocessor`. |

### `Messaging.ArchitectureTests/`
Testes que verificam as fronteiras do monólito modular em tempo de compilação/execução.

| Arquivo | O que verifica |
|---|---|
| `ModuleBoundaryTests.cs` | 6 regras com NetArchTest: (1) Ingestion não depende de Processing/Operations, (2) Processing não depende de Ingestion/Operations, (3) Operations não depende de Ingestion/Processing, (4) SharedKernel não depende de nada, (5) Contracts não depende de módulos, (6) Nenhum módulo de domínio referencia Kafka ou RabbitMQ. |

**Se alguém violar uma fronteira — por exemplo, chamar `WebhookIngestionService` de dentro de Processing — este teste falha e o quality gate bloqueia o merge.**

### `Messaging.IntegrationTests/`
Testa a API completa rodando em memória com `WebApplicationFactory`.

| Arquivo | O que cobre |
|---|---|
| `WebhookEndpointTests.cs` | 5 cenários: assinatura inválida (401), evento válido (200), duplicata (200 sem novo efeito), corpo não-JSON (200, grava o bruto), provider inválido (400). |

### `tests/load/webhook.js`
Teste de carga com [k6](https://k6.io). Dois cenários simultâneos:
- **`unique_events`** — 100 requisições/segundo por 30 segundos com eventos únicos. Testa throughput.
- **`retry_storm`** — 50 requisições/segundo com o mesmo evento repetido 50 vezes antes de avançar. Testa idempotência sob pressão.

**Thresholds:** erros < 1%, p95 de latência < 300ms, zero respostas inesperadas.

```bash
BASE_URL=http://localhost:5080 WEBHOOK_SECRET=load-secret k6 run tests/load/webhook.js
```

---

## 7. `specs/` — especificações antes do código

O fluxo de desenvolvimento começa aqui: escreva a spec antes de abrir um arquivo `.cs`.

| Arquivo | Status | O que especifica |
|---|---|---|
| `README.md` | — | Como usar o diretório de specs, convenções de nomeação e formato. |
| `_template.md` | — | Template padrão para novas specs: Problema, Fora de escopo, Requisitos, Critérios de aceite, Modelo, Riscos, Perguntas em aberto, Tarefas. |
| `001-webhook-ingestion.md` | 🟡 T-01..T-04 feitas | Validação HMAC, evento bruto, outbox, chave de idempotência, endpoint. T-05 (PostgreSQL real) e T-06 (rotação de segredo) pendentes. |
| `002-idempotent-processing.md` | ⏳ rascunho | Processamento transacional, IProcessingStore, consumer Kafka/RabbitMQ, host Worker. Zero tarefas concluídas. |
| `003-recovery-and-dlq.md` | 🟡 T-01 feita | Recovery job (lock por chave), endpoints admin, alertas. StuckEventDetector implementado; resto pendente. |

---

## 8. `docs/` — documentação arquitetural

### `docs/adr/` — Architecture Decision Records
Registro permanente das decisões arquiteturais importantes. Cada ADR responde: qual foi o problema, quais opções foram consideradas, qual foi escolhida e por quê.

| Arquivo | Decisão |
|---|---|
| `0001-modular-monolith.md` | Por que monólito modular em vez de microsserviços. Fronteiras de módulo, regra de dependência, como testar as fronteiras. |
| `0002-outbox-over-dual-write.md` | Por que Outbox Pattern em vez de publicar diretamente no broker dentro da transação (dual-write). Garantia de entrega, atomicidade. |
| `0003-broker-behind-abstraction.md` | Por que `IMessageBus` em vez de Kafka/RabbitMQ direto no código de domínio. Como trocar o broker sem tocar a lógica. |

### `docs/context/CONTEXT-MAP.md`
Guia de contexto para o Claude Code: o que carregar em cada tipo de tarefa (novo consumer, outbox, spec, debug de duplicata, etc.). Evita carregar arquivos desnecessários e desperdiçar contexto.

### `docs/modulos/`
15 arquivos documentando os módulos DAPIA (Disciplinas de Arquitetura Profissional com Inteligência Artificial). Cada módulo aborda uma capacidade do sistema de engenharia:

| # | Módulo |
|---|---|
| 01 | Claude Code e Cursor |
| 02 | Spec-Driven Development |
| 03 | TDD |
| 04 | Pull Request Review automático |
| 05 | Issue Analysis com story points |
| 06 | Prompt Engineering |
| 07 | Context Engineering |
| 08 | OWASP Top 10 |
| 09 | Quality Gate |
| 10 | Análises de Segurança |
| 11 | Loop Engineering |
| 12 | Hermes Agent |
| 13 | Observabilidade (OTLP) |
| 14 | LLM-as-a-judge |
| 15 | Monólito Modular |

### `docs/prompts/`
Prompts de engenharia prontos para uso:

| Arquivo | Uso |
|---|---|
| `review-consumer.md` | Prompt para revisar um consumer quanto a idempotência, transação e ACK. |
| `debug-duplicate-effect.md` | Prompt para investigar um efeito duplicado em produção com traces e logs. |

---

## 9. `evals/` — LLM-as-a-judge

Sistema para avaliar a qualidade dos agentes (especialmente o `messaging-reviewer`).

| Arquivo/Pasta | O que faz |
|---|---|
| `rubric.md` | Rubrica objetiva de 0 a 5. Nota 5: achou todos os defeitos, explicou o cenário de falha, propôs correção correta, zero falso alarme. Nota 0: resposta vazia ou que piora o problema. |
| `cases/01-ack-before-commit.json` | Caso com defeito: ACK da mensagem antes do commit da transação. |
| `cases/02-plain-insert.json` | Caso com defeito: `INSERT` simples sem `ON CONFLICT` (não idempotente). |
| `cases/03-publish-in-transaction.json` | Caso com defeito: publicação no broker dentro da transação de banco. |
| `cases/04-timing-unsafe-signature.json` | Caso com defeito: comparação de assinatura sem tempo constante (vulnerável a timing attack). |
| `cases/05-clean-handler.json` | Caso limpo: handler correto, sem defeitos. O juiz deve dizer que não há problema. |
| `run_judge.py` | Runner Python que envia cada caso ao Claude e verifica a resposta contra a rubrica. `--dry-run` valida sem chamar o modelo. |

```bash
python3 evals/run_judge.py --dry-run   # valida estrutura dos casos
python3 evals/run_judge.py             # executa as avaliações
```

---

## 10. `scripts/` — automação local

### `bootstrap.sh`
Cria a solution `Messaging.sln` e adiciona todos os `.csproj` automaticamente. Rode uma vez ao clonar o repositório.
```bash
bash scripts/bootstrap.sh
```

### `quality-gate.sh`
Roda os 5 portões de qualidade em sequência. Falha com `exit 1` se qualquer portão falhar.

| Portão | O que verifica |
|---|---|
| 1. Build | `dotnet build -p:QualityGate=true` — warnings como erros, CA1502 ≤ 10 |
| 2. Arquitetura | `Messaging.ArchitectureTests` — fronteiras entre módulos |
| 3. Unitários | `Messaging.UnitTests` |
| 4. Integração | `Messaging.IntegrationTests` |
| 5. Cobertura | linhas ≥ `COVERAGE_MIN` (padrão 80%) |
| 6. Carga* | k6 p95 < 300ms, erros < 1% (`--with-load`) |

```bash
bash scripts/quality-gate.sh
bash scripts/quality-gate.sh --with-load
COVERAGE_MIN=90 bash scripts/quality-gate.sh
```

### `pentest-local.sh`
Testes defensivos OWASP contra a API rodando em `localhost`. Nunca deve ser executado contra ambientes externos.

### `loop.sh`
Script de loop de correção automatizada: roda o quality gate, detecta falhas, tenta corrigir, repete. Usado pelo slash command `/loop-fix`.

### `scripts/observability/watch-errors.sh`
Monitora erros nos logs do Elasticsearch em tempo real. Útil durante desenvolvimento para acompanhar problemas sem abrir o Kibana.

### `scripts/ops/`
Scripts de operação do ambiente:

| Script | O que faz |
|---|---|
| `backup-postgres.sh` | Faz dump do banco PostgreSQL local. |
| `analyze-logs.sh` | Analisa padrões nos logs (duplicatas, timeouts, erros recorrentes). |
| `notify-telegram.sh` | Envia notificação via Telegram (para alertas de DLQ, eventos presos). |
| `status-report.sh` | Gera relatório de status dos serviços (filas, banco, métricas principais). |

---

## 11. `observability/` e `ops/`

### `observability/otel-collector.yaml`
Configuração do OpenTelemetry Collector. Define o pipeline:
- **Receivers**: OTLP (recebe traces e logs da API via gRPC)
- **Processors**: batch (agrupa para eficiência)
- **Exporters**: Elasticsearch (persiste logs e traces para o Kibana)

Ativado apenas com `docker-compose.observability.yml`.

### `ops/hermes/README.md`
Documentação do Hermes Agent: automações de operação (relatórios periódicos, alertas de DLQ, backup agendado). Configurado para rodar como agente Claude Code em schedule.

---

## 12. `.claude/` — cockpit do Claude Code

Tudo que configura o Claude Code para este projeto específico.

### `.claude/rules/` — 13 regras do projeto
Carregadas automaticamente em toda sessão. Orientam o comportamento do Claude.

| Arquivo | O que define |
|---|---|
| `architecture.md` | Regras de dependência do monólito modular. Módulos só falam por Contracts. |
| `messaging-reliability.md` | As regras mais críticas: transação única, idempotência, ACK após commit, proibições explícitas. |
| `dotnet-style.md` | C# 12, nullable, async de ponta a ponta, CancellationToken, records imutáveis. |
| `code-style.md` | Métodos curtos, early return, sem código morto, constantes nomeadas. |
| `api-conventions.md` | Convenções do endpoint webhook, API admin, formato de erros (RFC 7807). |
| `testing.md` | Pirâmide de testes, cenários de chaos obrigatórios, convenções de nome. |
| `pr.md` | Conventional Commits, PR focado, o que é obrigatório antes do merge. |
| `security.md` | HMAC em tempo constante, segredos fora do repo, validação nas bordas. |
| `modular-monolith.md` | Como criar novos módulos, o que testar, como manter as fronteiras. |
| `spec-driven.md` | Como escrever uma spec antes de codificar, quando usar `/spec`. |
| `tdd.md` | Ciclo RED→GREEN→REFACTOR, o que cada fase deve e não deve fazer. |
| `context-engineering.md` | Como gerenciar contexto em sessões longas, quando usar `/handoff`. |
| `loop-engineering.md` | Como rodar o loop de correção automatizada com freios e critérios de parada. |

### `.claude/commands/` — 16 slash commands
Acionados com `/nome` no Claude Code. Cada arquivo `.md` define o que o comando faz.

| Comando | O que faz |
|---|---|
| `/spec` | Cria uma especificação em `specs/` antes de qualquer código. |
| `/tdd` | Guia o ciclo TDD: RED (teste que falha) → GREEN (código mínimo) → REFACTOR. |
| `/implement-spec` | Implementa uma spec aprovada, tarefa por tarefa. |
| `/review` | Aciona o agente `messaging-reviewer` sobre o diff atual. |
| `/quality-gate` | Roda `bash scripts/quality-gate.sh` e corrige o que falhar. |
| `/chaos-test` | Executa os cenários de chaos e reporta duplicidades encontradas. |
| `/new-consumer` | Cria um novo consumer/handler idempotente seguindo as regras. |
| `/reprocess-dlq` | Lista a DLQ e reprocessa eventos com segurança. |
| `/deploy` | Sobe o ambiente local e valida saúde dos serviços. |
| `/debug-trace` | Investiga um `CorrelationId` nos logs/traces do Elasticsearch. |
| `/owasp-check` | Audita o diff atual contra o OWASP Top 10. |
| `/pentest` | Aciona o agente `pentest-agent` contra `localhost`. |
| `/eval` | Roda o LLM-as-a-judge e reporta notas por caso. |
| `/handoff` | Salva o estado da sessão em `docs/context/HANDOFF.md` para a próxima. |
| `/loop-fix` | Itera (corrige → gate → corrige) até o quality gate ficar verde, com freios. |
| `/prompt` | Reescreve um prompt bruto no formato de engenharia (contexto, tarefa, restrições). |

### `.claude/agents/` — 10 agentes especializados
Subagentes que o Claude Code pode acionar para tarefas específicas.

| Agente | Especialidade |
|---|---|
| `messaging-reviewer` | Revisa mudanças em consumer, outbox e transação do worker. |
| `chaos-tester` | Desenha, roda e diagnostica cenários de chaos. |
| `security-auditor` | Audita validação HMAC, autenticação, segredos, configuração de broker. |
| `code-reviewer` | Revisão geral de código .NET antes do PR. |
| `pr-reviewer` | Revisa o diff de um PR contra as rules, classifica por severidade. |
| `issue-analyzer` | Estima story points, quebra issue em subtarefas, identifica arquivos afetados. |
| `observability-analyst` | Investiga erros e duplicidades em runtime via logs/traces no Elasticsearch. |
| `quality-gate` | Executa o quality gate e diagnostica cada falha com a correção certa. |
| `llm-judge` | Avalia a saída de outro agente contra `evals/rubric.md`. |
| `pentest-agent` | Pentest defensivo local: roda `scripts/pentest-local.sh`, testa variações. |

### `.claude/skills/` — 9 skills especializadas
Conhecimento aprofundado carregado sob demanda para tarefas específicas.

| Skill | Quando carregar |
|---|---|
| `outbox-idempotency` | Ao implementar ou revisar outbox, idempotência, upsert. |
| `kafka-vs-rabbitmq` | Ao implementar adapters de broker ou comparar os dois. |
| `testing-patterns` | Ao escrever testes (unit, integração, chaos) em .NET. |
| `owasp-top10` | Ao revisar código exposto (webhook, API admin). |
| `observability-debugging` | Para depurar com traces e logs seguindo um `CorrelationId`. |
| `performance-optimization` | Quando testes de carga reprovarem. |
| `cyclomatic-complexity` | Quando o build falhar com CA1502. |
| `load-testing` | Para criar e interpretar testes k6. |
| `prompt-engineering-dev` | Para escrever prompts para os agentes. |

### `.claude/hooks/` — 4 hooks automáticos
Executados automaticamente em eventos do Claude Code.

| Hook | Quando dispara | O que faz |
|---|---|---|
| `block-dangerous.sh` | Antes de qualquer tool use | Bloqueia comandos destrutivos (`rm -rf`, `git push --force`, etc.). |
| `guard-files.sh` | Antes de editar arquivo | Impede edição de migrations já aplicadas e arquivos de segredos. |
| `format-dotnet.sh` | Após editar arquivo `.cs` | Roda `dotnet format` no arquivo editado. |
| `validate-code.sh` | Ao encerrar a sessão | Roda o build para garantir que não há erro de compilação. |

### `.claude/mcp-servers/rabbitmq/`
Servidor MCP customizado para RabbitMQ (não há pacote npm oficial). Usa a [Management HTTP API](http://localhost:15672/api/) do RabbitMQ.

| Arquivo | O que faz |
|---|---|
| `index.js` | Servidor MCP em Node.js (ESM). 9 ferramentas: `rabbitmq_list_queues`, `rabbitmq_get_queue`, `rabbitmq_get_messages` (peek não-destrutivo), `rabbitmq_publish_message`, `rabbitmq_purge_queue`, `rabbitmq_list_exchanges`, `rabbitmq_list_bindings`, `rabbitmq_list_consumers`, `rabbitmq_overview`. |
| `package.json` | Dependência: `@modelcontextprotocol/sdk`. |

### `.claude/settings.json`
Configurações do Claude Code: permissões de ferramentas, mapeamento de hooks para eventos, configurações de MCP.

---

## 13. `.github/` — CI/CD e automações

### `.github/workflows/ci.yml`
**Dispara:** todo `push` em `main` e todo `pull_request`.
**O que faz:** checkout → setup .NET 8 → `bootstrap.sh` → `quality-gate.sh` → upload do relatório de cobertura.
**Cancela** runs anteriores do mesmo branch (concurrency group).

### `.github/workflows/claude-pr-review.yml`
**Dispara:** PR aberto, atualizado ou reaberto (não draft, não fork).
**O que faz:** aciona o agente `pr-reviewer` que lê o diff, comenta achados inline com severidade `[CRÍTICO]` / `[ATENÇÃO]` / `[SUGESTÃO]`, adiciona/remove o label `claude-blocking`, e posta resumo.
**Gate separado** (`Claude review gate`): falha se o label `claude-blocking` estiver presente — bloqueia o merge.

### `.github/workflows/claude-issue-analysis.yml`
**Dispara:** issue criada ou editada.
**O que faz:** aciona o agente `issue-analyzer` que estima story points, quebra em subtarefas testáveis e identifica os arquivos provavelmente afetados. Posta o resultado como comentário na issue.

### `.github/workflows/claude.yml`
**Dispara:** comentários com `@claude` em issues e PRs.
**O que faz:** responde ao `@claude` com análise, sugestão ou implementação conforme solicitado no comentário.

### `.github/workflows/claude-auto-fix.yml`
Workflow de correção automatizada disparado por evento específico.

### `.github/ISSUE_TEMPLATE/`
Templates para novas issues:
- `bug.yml` — template para bug report (descrição, reprodução, comportamento esperado).
- `feature.yml` — template para feature request.

### `.github/pull_request_template.md`
Template padrão para descrição de PR: Contexto, O que mudou, Como testar, Riscos, checklist de invariantes.

---

## 14. `.cursor/rules/` — contexto para o Cursor IDE

Regras no formato `.mdc` (Markdown com metadados) carregadas pelo Cursor IDE.

| Arquivo | O que define |
|---|---|
| `messaging-reliability.mdc` | As regras de confiabilidade de mensageria (espelho de `.claude/rules/messaging-reliability.md`). |
| `project-context.mdc` | Contexto geral do projeto para o Cursor entender o domínio. |
| `tdd.mdc` | Regras de TDD para o Cursor seguir ao sugerir código. |

---

## Resumo rápido

| Pergunta | Onde olhar |
|---|---|
| Como o projeto funciona? | `README.md` + `CLAUDE.md` |
| Como subir o ambiente? | `docker-compose.yml` + `scripts/bootstrap.sh` |
| Como validar o código? | `scripts/quality-gate.sh` |
| Onde está a lógica de validação HMAC? | `src/Modules/Messaging.Ingestion/WebhookSignatureValidator.cs` |
| Onde está o endpoint do webhook? | `src/Hosts/Messaging.Api/Program.cs` |
| Por que o módulo X não pode chamar o módulo Y? | `docs/adr/0001-modular-monolith.md` + `tests/Messaging.ArchitectureTests/ModuleBoundaryTests.cs` |
| Por que usar Outbox em vez de publicar direto? | `docs/adr/0002-outbox-over-dual-write.md` |
| O que ainda falta implementar? | `specs/` (status de cada spec) |
| Como os agentes do Claude Code funcionam? | `.claude/agents/` |
| Como o CI revisa PRs automaticamente? | `.github/workflows/claude-pr-review.yml` |
