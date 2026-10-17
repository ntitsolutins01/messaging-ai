# Roadmap — Tour completo pelo projeto

> Roteiro de 10 sessões para dominar a POC do zero ao demo. Cada sessão tem objetivo claro, arquivos relevantes, comandos para rodar e o que você deve conseguir explicar ao final.

---

## Visão geral

```
Sessão 0   🛠️  AMBIENTE            Subir tudo e validar que compila
    │
Sessão 1   🔐  HMAC + IDEM KEY     Segurança e unicidade de eventos
    │
Sessão 2   📥  WEBHOOK INGESTION   Receber, validar e gravar o evento
    │
Sessão 3   📦  OUTBOX PATTERN      Garantir entrega sem dual-write
    │
Sessão 4   🔀  KAFKA / RABBITMQ    Trocar broker com 1 variável
    │
Sessão 5   ⚙️   WORKER IDEMPOTENTE  Transação única: a garantia central
    │
Sessão 6   🔁  DLQ + REPROCESS     Falha forçada → reprocessamento 2×
    │
Sessão 7   💥  CHAOS TESTS         6 cenários de falha ao vivo
    │
Sessão 8   🏗️   QUALITY GATE        5 portões + arch tests + CI
    │
Sessão 9   🤖  DEMO FINAL          Claude Code ao vivo + apresentação
```

**Sessões 0–2** → código já existe, rodar hoje
**Sessões 3–7** → implementar juntos, um por um
**Sessões 8–9** → consolidar e preparar apresentação

---

## Sessão 0 — Ambiente: subir e validar tudo

**Objetivo:** confirmar que o ambiente está funcional antes de começar qualquer implementação.

### Pré-requisitos

| Ferramenta | Versão | Verificar |
|---|---|---|
| .NET SDK | 8.0 | `dotnet --version` |
| Docker Desktop | 4.x | `docker --version` |
| Node.js | 18+ | `node --version` |

### Passo a passo

```bash
# 1. Criar a solution (uma vez)
bash scripts/bootstrap.sh

# 2. Subir infraestrutura
docker compose up -d

# 3. Verificar serviços
docker compose ps

# 4. Configurar segredo do webhook
dotnet user-secrets set "Webhook:Secret" "dev-secret" \
  --project src/Hosts/Messaging.Api

# 5. Quality gate inicial
bash scripts/quality-gate.sh
```

### Resultado esperado

- `bootstrap.sh` cria `Messaging.sln` e adiciona todos os `.csproj`
- `docker compose ps` mostra postgres, rabbitmq e kafka `healthy`
- `quality-gate.sh` passa nos 5 portões (build, arch, unit, int, cobertura)

### Arquivos envolvidos

```
scripts/bootstrap.sh
scripts/quality-gate.sh
docker-compose.yml
global.json
Directory.Build.props
```

### O que você deve conseguir explicar

- Por que o `Messaging.sln` não está no repositório
- O que cada serviço do `docker-compose.yml` faz
- O que o `Directory.Build.props` configura globalmente

---

## Sessão 1 — IdempotencyKey e assinatura HMAC-SHA256

**Objetivo:** entender os dois mecanismos de segurança e unicidade que protegem toda a POC.

### Conceitos

**IdempotencyKey** — chave única no formato `{provider}:{providerEventId}`. Garante que o mesmo evento, enviado N vezes, produz exatamente 1 efeito. Toda escrita usa essa chave com `UNIQUE constraint` no banco.

**HMAC-SHA256** — o provedor assina cada webhook com `t=<unix>,v1=HMAC-SHA256(secret, "{t}.{corpo}")`. A API valida antes de qualquer processamento. Comparação em **tempo constante** (evita timing attack). Janela de tolerância de 5 minutos.

### Rodar os testes unitários

```bash
dotnet test tests/Messaging.UnitTests --filter "IdempotencyKey|Signature" -v normal
```

### Testar manualmente com curl

```bash
# Gerar assinatura (PowerShell)
$secret = "dev-secret"
$body = '{"id":"evt_001","amount":100}'
$ts = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
$payload = "$ts.$body"
$key = [System.Text.Encoding]::UTF8.GetBytes($secret)
$msg = [System.Text.Encoding]::UTF8.GetBytes($payload)
$hmac = New-Object System.Security.Cryptography.HMACSHA256
$hmac.Key = $key
$hash = [BitConverter]::ToString($hmac.ComputeHash($msg)) -replace '-',''
$sig = "t=$ts,v1=$hash.ToLower()"

# Enviar webhook válido
curl -X POST http://localhost:5000/webhooks/acme `
  -H "Content-Type: application/json" `
  -H "X-Event-Id: evt_001" `
  -H "X-Signature: $sig" `
  -d '{"id":"evt_001","amount":100}'

# Assinatura inválida → 401
curl -X POST http://localhost:5000/webhooks/acme `
  -H "Content-Type: application/json" `
  -H "X-Signature: t=123,v1=invalido" `
  -d '{"id":"evt_001","amount":100}'
```

### Arquivos envolvidos

```
src/Shared/Messaging.SharedKernel/IdempotencyKey.cs
src/Modules/Messaging.Ingestion/WebhookSignatureValidator.cs
src/Modules/Messaging.Ingestion/SignatureResult.cs
tests/Messaging.UnitTests/IdempotencyKeyTests.cs
tests/Messaging.UnitTests/WebhookSignatureValidatorTests.cs
tests/Messaging.TestSupport/FakeClock.cs
tests/Messaging.TestSupport/WebhookSigner.cs
```

### O que você deve conseguir explicar

- Por que usar `CryptographicOperations.FixedTimeEquals` em vez de `==`
- Como a `IdempotencyKey` previne duplicatas sem precisar do banco
- Por que o segredo não pode estar no repositório

---

## Sessão 2 — Webhook Ingestion: receber e gravar o evento

**Objetivo:** ver o fluxo completo de entrada funcionando — da requisição HTTP até o evento gravado.

### Subir a API

```bash
dotnet run --project src/Hosts/Messaging.Api
```

### Os 4 cenários para testar

```bash
# 1. Evento válido → 200, 1 evento gravado
curl -X POST http://localhost:5000/webhooks/acme \
  -H "X-Event-Id: evt_001" \
  -H "X-Signature: <assinatura_valida>" \
  -d '{"id":"evt_001","amount":100}'

# 2. Mesmo evento de novo → 200 (duplicata, sem novo efeito)
curl -X POST http://localhost:5000/webhooks/acme \
  -H "X-Event-Id: evt_001" \
  -H "X-Signature: <assinatura_valida>" \
  -d '{"id":"evt_001","amount":100}'

# 3. Assinatura inválida → 401
curl -X POST http://localhost:5000/webhooks/acme \
  -H "X-Signature: t=0,v1=fake" \
  -d '{"id":"evt_002"}'

# 4. Provider inválido → 400
curl -X POST http://localhost:5000/webhooks/INVALIDO \
  -H "X-Signature: <assinatura_valida>" \
  -d '{}'
```

### Rodar os testes de integração

```bash
dotnet test tests/Messaging.IntegrationTests -v normal
```

### Arquivos envolvidos

```
src/Hosts/Messaging.Api/Program.cs
src/Modules/Messaging.Ingestion/WebhookIngestionService.cs
src/Modules/Messaging.Ingestion/IRawEventStore.cs
src/Modules/Messaging.Ingestion/InMemoryRawEventStore.cs
src/Modules/Messaging.Ingestion/RawEvent.cs
src/Modules/Messaging.Ingestion/IngestionOutcome.cs
tests/Messaging.IntegrationTests/WebhookEndpointTests.cs
specs/001-webhook-ingestion.md
```

### O que você deve conseguir explicar

- Por que duplicata também responde `200` (o provedor para de reenviar)
- Por que corpo malformado grava o bruto e responde `200` (o parse falha depois, no worker)
- O que o `X-Correlation-Id` faz e por que importa

---

## Sessão 3 — Outbox Pattern: por que existe e como funciona

**Objetivo:** entender o problema do dual-write e implementar o `IRawEventStore` com PostgreSQL real.

### O problema sem Outbox

```
❌ SEM Outbox (dual-write):
   1. BEGIN TRANSACTION
   2. INSERT evento_bruto ✅
   3. COMMIT ✅
   4. Publicar no Kafka...  💥 FALHA AQUI
   → evento gravado no banco, mas nunca publicado no broker
   → ou: publicado no broker, mas rollback no banco
   → efeitos inconsistentes
```

```
✅ COM Outbox:
   1. BEGIN TRANSACTION
   2. INSERT evento_bruto
   3. INSERT outbox (mensagem a publicar)
   4. COMMIT  ← tudo ou nada
   5. OutboxPublisher lê a outbox e publica (pode repetir com segurança)
   → banco e broker sempre consistentes
```

### Implementar IRawEventStore com PostgreSQL (spec 001 / T-05)

```bash
# Instalar EF Core
dotnet add src/Modules/Messaging.Ingestion package Microsoft.EntityFrameworkCore
dotnet add src/Modules/Messaging.Ingestion package Npgsql.EntityFrameworkCore.PostgreSQL
```

A implementação deve:
- Usar `INSERT ... ON CONFLICT (idempotency_key) DO NOTHING`
- Gravar `evento_bruto` e `outbox` **na mesma transação**
- Retornar `true` se inseriu, `false` se era duplicata (conflict)

### Inspecionar com MCP postgres

Após implementar, usar o MCP do PostgreSQL direto no Claude Code:
```
rabbitmq_list_queues → ver a outbox crescer
SQL: SELECT * FROM ingestion_outbox ORDER BY created_at DESC LIMIT 10;
```

### Arquivos envolvidos

```
src/Modules/Messaging.Ingestion/IRawEventStore.cs
src/Modules/Messaging.Ingestion/InMemoryRawEventStore.cs  ← substituir
docs/adr/0002-outbox-over-dual-write.md
specs/001-webhook-ingestion.md  (T-05)
```

### O que você deve conseguir explicar

- Por que o `INSERT ... ON CONFLICT DO NOTHING` é o coração da idempotência
- Por que banco e broker na mesma transação é impossível e perigoso
- Por que o OutboxPublisher pode repetir a publicação com segurança

---

## Sessão 4 — Broker: Kafka e RabbitMQ com troca por env var

**Objetivo:** ver a abstração `IMessageBus` funcionando e trocar de broker sem tocar o domínio.

### A abstração

```csharp
// Nenhum módulo de domínio conhece Kafka ou RabbitMQ.
// Só esta interface.
public interface IMessageBus
{
    Task PublishAsync<T>(T message, CancellationToken cancellationToken);
}
```

Os adapters ficam em `src/Brokers/` (a implementar, ADR 0003).

### Testar com Kafka

```bash
export Messaging__Broker=Kafka
dotnet run --project src/Hosts/Messaging.Api
```

Inspecionar com MCP Kafka no Claude Code:
```
kafka_list_topics          → ver tópicos criados
kafka_consume_messages     → ler mensagens da outbox publicadas
kafka_list_groups          → ver consumer groups
kafka_get_group_lag        → ver se o worker está atrasado
```

### Trocar para RabbitMQ

```bash
export Messaging__Broker=RabbitMq
dotnet run --project src/Hosts/Messaging.Api
```

Inspecionar com MCP RabbitMQ no Claude Code:
```
rabbitmq_list_queues       → ver filas criadas
rabbitmq_get_messages      → peek nas mensagens (não-destrutivo)
rabbitmq_overview          → status geral do broker
```

### Acessar painel do RabbitMQ

```
http://localhost:15672   usuário: poc   senha: poc
```

### Arquivos envolvidos

```
docs/adr/0003-broker-behind-abstraction.md
.claude/skills/kafka-vs-rabbitmq/SKILL.md
.mcp.json
docker-compose.yml
```

### O que você deve conseguir explicar

- Por que `IMessageBus` existe e o que protege
- Como o `Messaging__Broker` seleciona o adapter em runtime
- Qual a diferença entre Kafka (log imutável, offsets) e RabbitMQ (filas, ack destrutivo)

---

## Sessão 5 — Worker: transação única e idempotência garantida

**Objetivo:** implementar o coração da POC — o processamento que garante zero duplicatas mesmo com falha no meio.

### A transação única

```
BEGIN TRANSACTION
  1. SELECT ... FOR UPDATE SKIP LOCKED     ← lock exclusivo no evento
  2. UPDATE status → Processing
  3. Aplicar lógica de domínio
  4. UPSERT registro_interno               ← ON CONFLICT DO UPDATE
  5. INSERT outbox_saida                   ← ON CONFLICT DO NOTHING
  6. UPDATE status → Done
COMMIT
  ↓ SOMENTE AQUI
ACK da mensagem no broker
```

**Se falhar em qualquer passo:** ROLLBACK, mensagem volta para a fila, tenta de novo. O `UPSERT` garante que mesmo rodando duas vezes, o efeito é um só.

### Implementar (spec 002)

```bash
# Instalar EF Core no Worker (quando existir)
dotnet add src/Hosts/Messaging.Worker package Npgsql.EntityFrameworkCore.PostgreSQL
```

A implementação deve:
1. Consumir `RawEventAccepted` do broker
2. Fazer lock do evento com `pg_advisory_xact_lock`
3. Executar tudo na mesma transação
4. Fazer ACK **somente após** o commit

### Provar idempotência ao vivo

```bash
# Enviar mesmo evento 5 vezes
for i in 1 2 3 4 5; do
  curl -X POST http://localhost:5000/webhooks/acme \
    -H "X-Event-Id: evt_idem_test" \
    -H "X-Signature: <sig>" \
    -d '{"id":"evt_idem_test"}'
done

# Verificar: deve haver exatamente 1 registro interno
# (via MCP postgres ou psql)
SELECT COUNT(*) FROM processing_records WHERE idempotency_key = 'acme:evt_idem_test';
-- Resultado esperado: 1
```

### Arquivos envolvidos

```
src/Modules/Messaging.Processing/StuckEventDetector.cs
src/Modules/Messaging.Processing/EventStatus.cs
src/Contracts/Messaging.Contracts/Ingestion/RawEventAccepted.cs
specs/002-idempotent-processing.md
.claude/skills/outbox-idempotency/SKILL.md
tests/Messaging.UnitTests/StuckEventDetectorTests.cs
```

### O que você deve conseguir explicar

- Por que o ACK após o commit é crítico
- O que acontece se o worker cair entre o commit e o ACK
- Por que `UPSERT` em vez de `INSERT` no registro interno

---

## Sessão 6 — DLQ e reprocessamento manual

**Objetivo:** simular falha, ver o evento ir para a DLQ, e reprocessar duas vezes para provar idempotência.

### O fluxo de falha

```
Evento → Worker falha → retry (backoff exponencial)
  → falhou N vezes (padrão: 5) → DLQ
  → equipe de operações acessa /admin/events
  → POST /admin/events/{id}/reprocess
  → mesmo caminho, mesma chave → mesmo resultado
```

### StuckEventDetector (já implementado)

```csharp
// Decide o que fazer com evento preso (worker caiu no meio)
detector.Decide(state, now)
// → RecoveryAction.None      (ainda no prazo)
// → RecoveryAction.Requeue   (preso, mas abaixo do limite)
// → RecoveryAction.MoveToDlq (esgotou tentativas)
```

### Simular falha e reprocessamento

```bash
# 1. Forçar falha no worker (parar o processo no meio)
# 2. Verificar evento com status Processing preso
SELECT * FROM processing_events WHERE status = 'Processing' AND started_at < NOW() - INTERVAL '5 minutes';

# 3. Job de recovery recoloca na fila automaticamente

# 4. Reprocessar manualmente via API admin
curl -X POST http://localhost:5000/admin/events/{id}/reprocess

# 5. Reprocessar de novo (deve ser idempotente)
curl -X POST http://localhost:5000/admin/events/{id}/reprocess

# 6. Verificar: exatamente 1 registro interno
SELECT COUNT(*) FROM processing_records WHERE event_id = '{id}';
-- Resultado esperado: 1
```

### Arquivos envolvidos

```
src/Modules/Messaging.Processing/StuckEventDetector.cs
src/Modules/Messaging.Operations/ReprocessEventHandler.cs
src/Contracts/Messaging.Contracts/Processing/IEventReprocessor.cs
tests/Messaging.UnitTests/StuckEventDetectorTests.cs
tests/Messaging.UnitTests/ReprocessEventHandlerTests.cs
specs/003-recovery-and-dlq.md
.claude/commands/reprocess-dlq.md
```

### O que você deve conseguir explicar

- Como o `pg_advisory_xact_lock` impede dois workers processarem o mesmo evento
- Por que o job de recovery não duplica efeitos
- Como a API admin é idempotente por design

---

## Sessão 7 — Chaos Tests: os 6 cenários de falha

**Objetivo:** provar, com testes automatizados, que o sistema mantém zero duplicatas em condições adversas reais.

### Os 6 cenários obrigatórios

| # | Cenário | Como simular | Assert |
|---|---|---|---|
| 1 | Mesmo webhook N vezes em paralelo | 20 threads enviando o mesmo `X-Event-Id` | `COUNT(eventos) = 1` e `COUNT(outbox) = 1` |
| 2 | Worker cai entre "marcar Processing" e o commit | `Thread.Sleep` + `Process.Kill` | Recovery conclui, `COUNT(registros) = 1` |
| 3 | Webhooks fora de ordem | Enviar evt_003 antes de evt_001 | Estado final correto |
| 4 | Dois workers competindo pelo mesmo evento | 2 instâncias do worker, mesmo evento na fila | Apenas 1 processa, `COUNT = 1` |
| 5 | DLQ reprocessada duas vezes | `reprocess` chamado 2× | Mesmo resultado, `COUNT = 1` |
| 6 | Broker indisponível | `docker compose stop kafka` | Outbox acumula; ao subir, drena sem perda |

### Rodar os chaos tests

```bash
dotnet test tests/Messaging.ChaosTests -v normal
```

### Inspecionar estado com MCP

```
# Durante o cenário 6 (broker fora)
rabbitmq_list_queues   → ver outbox acumulando
kafka_list_topics      → ver kafka offline

# Após subir o broker
rabbitmq_get_messages  → ver mensagens sendo consumidas
SQL: SELECT status, COUNT(*) FROM processing_events GROUP BY status;
```

### Arquivos envolvidos

```
tests/Messaging.ChaosTests/  ← a implementar
.claude/commands/chaos-test.md
.claude/agents/chaos-tester.md
.claude/rules/testing.md  (cenários obrigatórios)
```

### O que você deve conseguir explicar

- Por que cada cenário é perigoso sem as garantias do projeto
- O que o `COUNT(*)` prova em cada assert
- Por que Testcontainers é usado em vez de mocks

---

## Sessão 8 — Quality Gate e Architecture Tests

**Objetivo:** entender cada portão do quality gate e ver as fronteiras de módulo sendo testadas ao vivo.

### Rodar o quality gate completo

```bash
bash scripts/quality-gate.sh
```

### Os 5 portões explicados

```bash
# Portão 1: build com warnings-as-errors + complexidade ≤ 10
dotnet build Messaging.sln -p:QualityGate=true

# Portão 2: fronteiras entre módulos (NetArchTest)
dotnet test tests/Messaging.ArchitectureTests -v normal

# Portão 3: testes unitários
dotnet test tests/Messaging.UnitTests

# Portão 4: testes de integração
dotnet test tests/Messaging.IntegrationTests

# Portão 5: cobertura ≥ 80%
dotnet test tests/Messaging.UnitTests --collect:"XPlat Code Coverage"
```

### Quebrar uma fronteira intencionalmente

```csharp
// Adicionar em ReprocessEventHandler.cs:
using Messaging.Ingestion;  // ← PROIBIDO: Operations não pode depender de Ingestion
```

```bash
dotnet test tests/Messaging.ArchitectureTests
# FALHA: Operations_nao_depende_de_outros_modulos
```

### Ver o CI no GitHub

```bash
gh run list --limit 5
gh run view <id>
```

### Arquivos envolvidos

```
scripts/quality-gate.sh
tests/Messaging.ArchitectureTests/ModuleBoundaryTests.cs
.github/workflows/ci.yml
.github/workflows/claude-pr-review.yml
Directory.Build.props
CodeMetricsConfig.txt
```

### O que você deve conseguir explicar

- Por que o quality gate falha com `exit 1` em qualquer portão
- O que o `claude-blocking` label faz no PR
- Por que as fronteiras são testadas em código e não só documentadas

---

## Sessão 9 — Demo final: Claude Code como co-piloto

**Objetivo:** demonstrar os MCP servers, agentes e comandos ao vivo, e preparar a apresentação de 10 minutos para o chefe.

### Demo 1: MCP servers ao vivo

```
# No Claude Code, com infra rodando:

"Liste os tópicos Kafka do projeto"
→ usa kafka_list_topics

"Mostre as mensagens na fila de outbox do RabbitMQ"
→ usa rabbitmq_get_messages (peek)

"Quantos eventos estão com status Done no banco?"
→ usa MCP postgres: SELECT status, COUNT(*) FROM processing_events GROUP BY status;
```

### Demo 2: agente messaging-reviewer

```bash
# Fazer uma mudança com defeito intencional
# (ex: mover o ACK para antes do commit)
git diff

# Acionar o revisor
/review

# O agente deve apontar: "[CRÍTICO] ACK antes do commit — pode gerar duplicata"
```

### Demo 3: quality gate quebrando e sendo corrigido

```bash
# Introduzir complexidade alta em um método
/quality-gate
# Falha no Portão 1 (CA1502)
# /loop-fix itera até corrigir
```

### Roteiro de apresentação (10 minutos)

| Tempo | O que mostrar |
|---|---|
| 0–2 min | O problema: webhook reenviado gera duplicata. Mostrar o cenário de falha. |
| 2–4 min | A solução: Outbox + Chave de Idempotência + Transação Única. Diagrama do fluxo. |
| 4–6 min | Dual-broker ao vivo: trocar de Kafka para RabbitMQ com 1 variável. |
| 6–8 min | Chaos test ao vivo: enviar o mesmo evento 20 vezes em paralelo → `COUNT = 1`. |
| 8–9 min | Claude Code: MCP inspecionando o broker ao vivo, PR review automático. |
| 9–10 min | Quality gate: 5 portões, architecture tests, CI. |

### Arquivos envolvidos

```
.mcp.json
.claude/agents/messaging-reviewer.md
.claude/commands/review.md
.claude/commands/chaos-test.md
.github/workflows/claude-pr-review.yml
README.md  ← base para a apresentação
```

### O que você deve conseguir explicar

- Como os MCP servers permitem inspecionar a infraestrutura sem sair do IDE
- Por que o agente `messaging-reviewer` bloqueia o merge se achar algo crítico
- A diferença entre o que o Claude Code gerou e o que ele revisou/garantiu

---

## Checklist de conclusão

Ao final das 10 sessões, você deve conseguir:

- [ ] Subir o ambiente do zero com `bootstrap.sh` + `docker compose up -d`
- [ ] Explicar o que é IdempotencyKey e por que o formato `{provider}:{eventId}`
- [ ] Demonstrar HMAC-SHA256 com `201` e `401` no curl
- [ ] Explicar por que duplicata responde `200` e não `409`
- [ ] Desenhar o Outbox Pattern no quadro e explicar o problema que ele resolve
- [ ] Trocar de Kafka para RabbitMQ ao vivo com 1 variável
- [ ] Inspecionar tópicos/filas com os MCP servers
- [ ] Mostrar a transação única do worker e explicar o ACK após commit
- [ ] Forçar uma duplicata e provar com `COUNT(*) = 1` que não ocorreu
- [ ] Rodar o quality gate e explicar cada portão
- [ ] Mostrar um PR sendo bloqueado por achado crítico do Claude Code
- [ ] Apresentar a POC em 10 minutos para alguém não técnico

---

## Referências rápidas

| Precisa de... | Onde está |
|---|---|
| Entender cada pasta | `docs/guia-pastas.md` |
| Regras de mensageria | `.claude/rules/messaging-reliability.md` |
| Invariantes do projeto | `CLAUDE.md` (seção Invariantes) |
| Decisão arquitetural | `docs/adr/` |
| Status das specs | `specs/001`, `specs/002`, `specs/003` |
| Comandos do Claude Code | `.claude/commands/` |
