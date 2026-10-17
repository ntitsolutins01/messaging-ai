## ⚡ Messaging AI

> POC em **.NET 8** de integração orientada a eventos com **monólito modular**: webhook → outbox → broker → worker idempotente. Prova **zero efeitos duplicados** mesmo com reenvio por 20 minutos, worker caindo no meio e reprocessamento manual da DLQ.

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![C#](https://img.shields.io/badge/C%23-12-239120?style=for-the-badge&logo=csharp&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-4169E1?style=for-the-badge&logo=postgresql&logoColor=white)
![Kafka](https://img.shields.io/badge/Apache_Kafka-3.8-231F20?style=for-the-badge&logo=apachekafka&logoColor=white)
![RabbitMQ](https://img.shields.io/badge/RabbitMQ-3-FF6600?style=for-the-badge&logo=rabbitmq&logoColor=white)
![Claude Code](https://img.shields.io/badge/Claude_Code-AI--first-D97757?style=for-the-badge&logo=anthropic&logoColor=white)

---

## 📑 Sumário

- [Sobre o Projeto](#-sobre-o-projeto)
- [Fluxo e Arquitetura](#️-fluxo-e-arquitetura)
- [Status](#-status)
- [Pré-requisitos](#-pré-requisitos)
- [Quick Start](#️-quick-start)
- [Brokers](#-brokers)
- [Invariantes](#-invariantes)
- [Quality Gate](#-quality-gate)
- [Roadmap](#️-roadmap)
- [Claude Code](#-claude-code)
- [Contribuições](#-contribuições)
- [Documentação adicional](#-documentação-adicional)
- [Autor](#-autor)

---

## 📌 Sobre o Projeto

POC de um pipeline de eventos confiável em .NET 8, construído como **monólito modular** (ver [ADR 0001](docs/adr/0001-modular-monolith.md)). O objetivo é provar que o sistema garante **zero efeitos duplicados** — independente de quantas vezes o webhook for reenviado, do worker cair no meio ou da DLQ ser reprocessada duas vezes.

Os módulos são independentes: o broker (Kafka ou RabbitMQ) é trocável com **uma variável de ambiente**. Nenhum módulo de domínio conhece Kafka ou RabbitMQ diretamente.

> 🤖 Também é uma plataforma de demonstração do **Claude Code** como co-piloto de engenharia: agentes especializados, MCP servers para Kafka/RabbitMQ/PostgreSQL e slash commands específicos do domínio de mensageria.

---

## 🏗️ Fluxo e Arquitetura

```text
Provedor externo
      │
      │  POST /webhooks/{provider}
      ▼
┌───────────────────────────────────────────────────┐
│  Messaging.Api  (Módulo: Ingestion)               │
│  1. Valida assinatura HMAC-SHA256 (janela 5 min)  │
│  2. Grava evento bruto  ┐                         │
│  3. Grava na outbox     ┘  mesma transação        │
│  4. Responde 200 rápido                           │
└─────────────────────┬─────────────────────────────┘
                      │ OutboxPublisher
          ┌───────────┴───────────┐
          │                       │
       Kafka                   RabbitMQ
   (PLAINTEXT/9092)         (AMQP/5672)
          └───────────┬───────────┘
                      │
┌─────────────────────▼─────────────────────────────┐
│  Messaging.Worker  (Módulo: Processing)            │
│  ┌── mesma transação ───────────────────────────┐ │
│  │  1. Marca evento → Processing                │ │
│  │  2. Aplica lógica de domínio                 │ │
│  │  3. Upsert registro interno (UNIQUE key)     │ │
│  │  4. Grava outbox de saída                    │ │
│  │  5. Marca → Done — somente então faz ack     │ │
│  └──────────────────────────────────────────────┘ │
│  Falhou N vezes? → DLQ                            │
└────────────────────────────────────────────────────┘
                      │
      POST /admin/events/{id}/reprocess
      (mesmo caminho · mesma chave · idempotente)
```

**Estrutura de pastas:**

```text
messaging-ai/
├── 📁 src/
│   ├── 📁 Shared/
│   │   ├── 📁 Messaging.SharedKernel/      # IdempotencyKey, IClock — zero dependências
│   │   └── 📁 Messaging.Observability/     # OTLP opt-in por variável de ambiente
│   ├── 📁 Contracts/
│   │   └── 📁 Messaging.Contracts/         # RawEventAccepted, IEventReprocessor
│   ├── 📁 Modules/
│   │   ├── 📁 Messaging.Ingestion/         # webhook, HMAC, evento bruto, outbox
│   │   ├── 📁 Messaging.Processing/        # idempotência, recovery, DLQ
│   │   └── 📁 Messaging.Operations/        # reprocessamento manual
│   ├── 📁 Hosts/
│   │   ├── 📁 Messaging.Api/               # endpoint webhook + health checks
│   │   └── 📁 Messaging.Worker/            # consumer + recovery job (⏳ pendente)
│   └── 📁 Brokers/                         # adapters Kafka e RabbitMQ (⏳ pendente)
├── 📁 tests/
│   ├── 📁 Messaging.TestSupport/           # FakeClock, WebhookSigner
│   ├── 📁 Messaging.UnitTests/             # domínio e handlers com fakes
│   ├── 📁 Messaging.ArchitectureTests/     # fronteiras de módulo (NetArchTest)
│   ├── 📁 Messaging.IntegrationTests/      # WebApplicationFactory + Testcontainers
│   ├── 📁 Messaging.ChaosTests/            # cenários de falha reais (⏳ pendente)
│   └── 📁 load/
│       └── 📄 webhook.js                   # k6: 100 rps/30 s + tempestade de reenvios
├── 📁 specs/                               # especificações antes do código
├── 📁 docs/
│   ├── 📁 adr/                             # 3 ADRs: monólito, outbox, broker
│   └── 📁 context/                         # mapa de contexto por tipo de tarefa
├── 📁 evals/                               # LLM-as-a-judge com rubrica objetiva
├── 📁 observability/                       # OTLP → Elasticsearch → Kibana
├── 📁 scripts/
│   ├── 📄 bootstrap.sh                     # cria solution + .csproj
│   ├── 📄 quality-gate.sh                  # 5 portões (build → cobertura ≥ 80%)
│   └── 📄 pentest-local.sh                 # testes OWASP (apenas localhost)
├── 📁 .claude/                             # regras, agentes, skills, MCP servers
├── 📁 .github/workflows/                   # CI, @claude interativo, PR review, issue analysis
├── 📄 docker-compose.yml                   # PostgreSQL · RabbitMQ · Kafka
├── 📄 docker-compose.observability.yml     # Elasticsearch · Kibana · OTLP Collector
└── 📄 Directory.Build.props                # nullable, warnings-as-errors
```

---

## 🚦 Status

```text
✅ Implementado e testado
   ├── IdempotencyKey, WebhookSignatureValidator (HMAC-SHA256, tempo constante)
   ├── WebhookIngestionService + InMemoryRawEventStore
   ├── Program.cs — endpoint webhook + health checks
   ├── StuckEventDetector, ReprocessEventHandler
   ├── ModuleBoundaryTests — 6 regras de arquitetura executáveis (NetArchTest)
   └── WebhookEndpointTests — 5 cenários de integração (WebApplicationFactory)

🟡 Parcialmente pronto
   ├── Spec 001 — T-01..T-04 concluídas; T-05 (PostgreSQL real) e T-06 (rotação de segredo) abertas
   └── Spec 003 — T-01 (StuckEventDetector) concluída; recovery job, admin API e alertas abertos

⏳ Pendente
   ├── IRawEventStore em PostgreSQL com Testcontainers (spec 001 / T-05)
   ├── IProcessingStore + handler transacional (spec 002)
   ├── Consumer Kafka e RabbitMQ via IMessageBus (spec 002 / ADR 0003)
   ├── Host Messaging.Worker (spec 002)
   ├── Endpoints /admin/events com perfil operations (spec 003)
   ├── Broker adapters (Messaging.Brokers.Kafka, Messaging.Brokers.RabbitMq)
   └── Testes de chaos (6 cenários obrigatórios)
```

> **Primeiro passo:** `bash scripts/bootstrap.sh && bash scripts/quality-gate.sh`

---

## 🔧 Pré-requisitos

| Ferramenta | Versão mínima |
|---|---|
| **.NET SDK** | 8.0 (`global.json`) |
| **Docker Desktop** | 4.x |
| **Node.js** | 18+ (MCP servers) |
| **Python** | 3.10+ (evals, opcional) |
| **k6** | qualquer (carga, opcional) |

---

## ▶️ Quick Start

### 1️⃣ Criar a solution

```bash
bash scripts/bootstrap.sh
```

### 2️⃣ Subir a infraestrutura

```bash
docker compose up -d
```

### 3️⃣ Configurar o segredo do webhook

```bash
# Nunca commitar — usar user-secrets ou variável de ambiente
dotnet user-secrets set "Webhook:Secret" "dev-secret" \
  --project src/Hosts/Messaging.Api
```

### 4️⃣ Rodar a API

```bash
dotnet run --project src/Hosts/Messaging.Api
# POST http://localhost:5000/webhooks/{provider}
# GET  http://localhost:5000/health/ready
```

### 5️⃣ Rodar os testes

```bash
dotnet test                                     # todos
dotnet test tests/Messaging.UnitTests
dotnet test tests/Messaging.IntegrationTests
dotnet test tests/Messaging.ArchitectureTests
```

### 6️⃣ Quality gate completo

```bash
bash scripts/quality-gate.sh             # build + arch + unit + int + cobertura ≥ 80%
bash scripts/quality-gate.sh --with-load # + k6: p95 < 300 ms, erros < 1%
```

### 7️⃣ Observabilidade (opcional)

```bash
docker compose -f docker-compose.yml -f docker-compose.observability.yml up -d
# Kibana: http://localhost:5601
# OTLP ativa automaticamente com OTEL_EXPORTER_OTLP_ENDPOINT
```

---

## 🔀 Brokers

| Serviço | Endereço | Credenciais |
|---|---|---|
| PostgreSQL | `localhost:5432` · db `messaging_poc` | `poc` / `poc` |
| RabbitMQ AMQP | `localhost:5672` | `poc` / `poc` |
| RabbitMQ Management | `http://localhost:15672` | `poc` / `poc` |
| Kafka | `localhost:9092` | sem auth |

**Trocar o broker com uma variável:**

```bash
export Messaging__Broker=Kafka      # padrão
export Messaging__Broker=RabbitMq
```

> Os adapters ficam em `src/Brokers/` atrás de `IMessageBus`. Nenhum módulo de domínio referencia Kafka ou RabbitMQ diretamente — ver [ADR 0003](docs/adr/0003-broker-abstraction.md).

---

## 🔒 Invariantes

Regras que **nunca podem ser violadas** — violação bloqueia o PR:

| # | Regra |
|---|---|
| 1 | Todo evento tem **chave de idempotência** única; ela vale para cada efeito separado |
| 2 | Estado + efeito interno + outbox gravam **na mesma transação** de banco |
| 3 | Escrita com `upsert` + restrição `UNIQUE` — nunca `INSERT` simples |
| 4 | Módulos só se comunicam por `Messaging.Contracts`; nenhum módulo conhece Kafka/RabbitMQ |
| 5 | Evento bruto gravado **antes** de qualquer parse; parse fica atrás de adaptador versionado |
| 6 | Sem segredos no repositório; webhook sem assinatura HMAC válida nunca tem efeito |

**Proibido explicitamente:**
- Publicar no broker dentro da transação de banco → use outbox
- `catch` que engole exceção sem registrar e sem decidir retry/DLQ
- Ack da mensagem antes do commit do banco

---

## 🧪 Quality Gate

```text
Portão 1 — Build      dotnet build -p:QualityGate=true
                       warnings-as-errors + complexidade ciclomática CA1502 ≤ 10

Portão 2 — Arquitetura Messaging.ArchitectureTests (NetArchTest)
                       6 regras de fronteira entre módulos

Portão 3 — Unitários   Messaging.UnitTests

Portão 4 — Integração  Messaging.IntegrationTests (Testcontainers)

Portão 5 — Cobertura   linhas ≥ 80% (configurável via COVERAGE_MIN)

Portão 6 — Carga*      k6: p95 < 300 ms · erros < 1%  (*--with-load)
```

O CI roda o gate completo em cada PR (`ci.yml`). PRs com label `claude-blocking` não podem ser mergeados.

---

## 🗺️ Roadmap

```text
✅ Fase 1 — Fundação
   ├── IdempotencyKey + WebhookSignatureValidator (HMAC-SHA256, tempo constante)
   ├── WebhookIngestionService + outbox em memória
   ├── Endpoint webhook + health checks
   ├── StuckEventDetector + ReprocessEventHandler
   ├── Architecture tests com NetArchTest (6 regras)
   └── Quality gate com 5 portões + CI no GitHub Actions

🚧 Fase 2 — Persistência e Processamento
   ├── IRawEventStore em PostgreSQL com Testcontainers
   ├── IProcessingStore + handler transacional
   ├── Consumer Kafka e RabbitMQ (IMessageBus)
   └── Host Messaging.Worker + recovery job com advisory lock

🔮 Fase 3 — Confiabilidade e Observabilidade
   ├── Testes de chaos (6 cenários obrigatórios)
   ├── Endpoints /admin/events com perfil operations
   ├── Alertas para eventos presos e DLQ crescendo
   └── Load test: 100 rps/30 s com reenvio em paralelo
```

---

## 🤖 Claude Code

Este projeto foi desenhado para ser desenvolvido com o **Claude Code** como co-piloto. A pasta `.claude/` tem regras, agentes, skills e MCPs específicos do domínio de mensageria.

### MCP Servers

Inspecione e interaja com a infraestrutura diretamente do Claude Code:

| Servidor | Ferramentas disponíveis |
|---|---|
| **postgres** | SQL direto em `events_raw`, `outbox`, registros internos |
| **kafka** | `kafka_list_topics` · `kafka_consume_messages` · `kafka_list_groups` · `kafka_get_group_lag` · `kafka_produce_message` · `kafka_alter_configs` |
| **rabbitmq** | `rabbitmq_list_queues` · `rabbitmq_get_messages` (peek) · `rabbitmq_publish_message` · `rabbitmq_purge_queue` · `rabbitmq_list_exchanges` · `rabbitmq_overview` |

> O servidor RabbitMQ é customizado (`.claude/mcp-servers/rabbitmq/`) — usa a Management HTTP API.

### Agentes especializados

| Agente | Quando usar |
|---|---|
| `messaging-reviewer` | Qualquer mudança em consumidor, outbox ou transação do worker |
| `chaos-tester` | Desenhar e rodar cenários de chaos |
| `security-auditor` | Validação HMAC, autenticação, segredos, configuração de broker |
| `pr-reviewer` | Revisão de diff (acionado pelo workflow `claude-pr-review.yml`) |
| `issue-analyzer` | Story points e subtarefas (acionado por `claude-issue-analysis.yml`) |
| `observability-analyst` | Depurar erro ou efeito duplicado em runtime via logs/traces |
| `quality-gate` | Quando o CI de qualidade falhar |
| `llm-judge` | Avaliar saída de outro agente contra `evals/rubric.md` |

### Slash commands

```bash
/spec             # cria especificação em specs/ antes do código
/tdd              # ciclo TDD guiado (RED → GREEN → REFACTOR)
/implement-spec   # implementa uma spec aprovada, tarefa por tarefa
/review           # revisão de mensageria — obrigatório antes do PR
/quality-gate     # roda o gate completo e corrige o que falhar
/chaos-test       # executa cenários de chaos e reporta duplicidades
/new-consumer     # cria consumidor/handler idempotente
/reprocess-dlq    # lista a DLQ e reprocessa com segurança
/debug-trace      # investiga CorrelationId nos logs/traces (ELK)
/owasp-check      # audita o diff contra o OWASP Top 10
/handoff          # resume o estado da sessão para a próxima
```

---

## 🤝 Contribuições

Contribuições são bem-vindas! Para contribuir:

1. Faça um **fork** do repositório
2. Crie uma branch: `git checkout -b feat/minha-feature`
3. Leia `.claude/rules/messaging-reliability.md` antes de mexer em qualquer fluxo de processamento
4. Siga o fluxo: **`/spec` → `/tdd` → `/review` → `/quality-gate`**
5. Faça o commit: `git commit -m "feat: adiciona suporte a X"`
6. Abra um **Pull Request** — o agente `pr-reviewer` revisará automaticamente 🚀

> PRs que tocam consumidor ou outbox são **bloqueados** sem `/review` e sem teste de chaos correspondente.

---

## 📚 Documentação adicional

| Documento | Descrição |
|---|---|
| 🏗️ [ADR 0001 — Monólito Modular](docs/adr/0001-modular-monolith.md) | Por que monólito modular em vez de microsserviços |
| 📦 [ADR 0002 — Outbox vs Dual Write](docs/adr/0002-outbox-vs-dual-write.md) | Por que Outbox pattern para garantir a entrega |
| 🔀 [ADR 0003 — Broker atrás de abstração](docs/adr/0003-broker-abstraction.md) | Por que `IMessageBus` e como trocar o broker |
| 📋 [Spec 001 — Webhook Ingestion](specs/001-webhook-ingestion.md) | Validação HMAC, evento bruto, outbox |
| ⚙️ [Spec 002 — Idempotent Processing](specs/002-idempotent-processing.md) | Processamento transacional e idempotente |
| 🔁 [Spec 003 — Recovery e DLQ](specs/003-recovery-and-dlq.md) | Eventos presos, reprocessamento, alertas |
| 🗺️ [Context Map](docs/context/CONTEXT-MAP.md) | O que carregar por tipo de tarefa |

---

## 👤 Autor

Feito com 💙 por **Fábio Muniz**

[![LinkedIn](https://img.shields.io/badge/LinkedIn-0077B5?style=flat&logo=linkedin&logoColor=white)](https://www.linkedin.com/in/fabiomunizdeveloper/)
[![GitHub](https://img.shields.io/badge/GitHub-181717?style=flat&logo=github&logoColor=white)](https://github.com/ntitsolutins01?tab=repositories)

---

**Versão:** 1.0.0 · **Status:** 🚧 Em desenvolvimento
