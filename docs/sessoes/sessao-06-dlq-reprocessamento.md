# Sessão 6 — DLQ e reprocessamento manual

---

## Objetivo

Entender o que acontece com um evento que falha repetidamente — onde ele vai, como é inspecionado e como é reprocessado manualmente de forma segura, sem risco de duplicar o efeito.

---

## Quando usar

Esses conceitos entram em jogo quando:

- Um evento chega à DLQ e precisa ser investigado e reprocessado.
- Alguém questiona o que acontece quando o sistema destino está fora do ar por horas.
- Você precisar mostrar que o reprocessamento manual é tão seguro quanto o processamento automático.
- A equipe de operações precisar de uma API para inspecionar e agir sobre eventos com problema.

---

## Quando NÃO usar

- **Não reprocesse diretamente pelo banco** — alterar `status` manualmente no banco bypassa toda a lógica de idempotência, logging e rastreabilidade.
- **Não crie um caminho especial para DLQ** — o reprocessamento usa exatamente o mesmo handler e a mesma chave de idempotência do fluxo normal. Caminhos especiais introduzem bugs que o fluxo normal não tem.
- **Não exponha a API admin sem autenticação** — toda rota `/admin/*` exige o perfil `operations`. Sem isso, qualquer pessoa pode reprocessar eventos arbitrários.

---

## O que ela faz

### O caminho até a DLQ

Um evento chega à DLQ quando esgota as tentativas de processamento:

```
Broker entrega evento
    │
    ▼
Worker tenta processar
    │
    ├── Sucesso → Done → ack ✅
    │
    └── Falha → incrementa attempts
                │
                ├── attempts < maxAttempts (5) → backoff exponencial → retry
                │
                └── attempts == maxAttempts
                        │
                        ▼
                   StuckEventDetector.Decide() → MoveToDlq
                        │
                        ▼
                   status = 'Failed'
                   motivo da falha gravado
                   mensagem movida para DLQ do broker
```

**Por que backoff exponencial?** Se o sistema destino estiver fora do ar, retentativas imediatas só aumentam a carga sem resultado. Com backoff (ex.: 1s, 2s, 4s, 8s, 16s), o sistema dá tempo para a dependência se recuperar antes de tentar de novo.

**Por que não tentar infinitamente?** Eventos que falham sempre têm uma causa estrutural — schema errado, sistema destino com bug, dado inválido. Tentar infinitamente não resolve o problema; apenas ocupa recursos e mascara o erro. Depois de `maxAttempts`, o evento precisa de intervenção humana.

---

### A Dead Letter Queue (DLQ)

A DLQ é um destino explícito para eventos que não puderam ser processados automaticamente. No projeto:

- **No Kafka:** Dead Letter Topic (tópico separado, ex.: `raw-events.DLQ`)
- **No RabbitMQ:** Dead Letter Exchange + fila dedicada (ex.: `raw-events-dlq`)
- **No banco:** `status = 'Failed'` com `last_error` e `failed_at`

O evento não some — ele fica acessível para inspeção e reprocessamento.

```
                 ┌─────────────────┐
                 │   DLQ           │
  falhou 5x ───▶ │  status=Failed  │◀── inspecionável via GET /admin/events
                 │  last_error     │
                 │  attempts=5     │──▶ reprocessável via POST /admin/events/{id}/reprocess
                 └─────────────────┘
```

---

### A API administrativa (Operations)

Definida em `.claude/rules/api-conventions.md`:

```
GET  /admin/events?status=Failed&page=1        lista paginada de eventos com problema
GET  /admin/events/{id}                        detalhe: payload bruto, tentativas, último erro
POST /admin/events/{id}/reprocess              reprocessa — idempotente
```

**Todos os endpoints exigem o perfil `operations`.** Uma chamada sem autenticação retorna `403`. A regra de segurança também exige que toda ação seja registrada com o usuário que agiu (auditoria).

---

### Por que o reprocessamento é seguro: mesma chave, mesmo caminho

O ponto central da Sessão 6 é este: **reprocessar não é um caminho especial**.

```
Fluxo normal:
  Broker → Consumer → IProcessingStore.ProcessAsync(event) → Done

Reprocessamento manual:
  POST /admin/events/{id}/reprocess
    → ReprocessEventHandler.HandleAsync(id)
      → IEventReprocessor.ReprocessAsync(id)           ← porta pública em Contracts
        → (implementação em Processing)
          → reenfileira na fila normal com a MESMA chave
            → Consumer → IProcessingStore.ProcessAsync(event) → Done
```

O evento entra no mesmo fluxo, com a mesma `IdempotencyKey`. O upsert no registro interno (`ON CONFLICT DO UPDATE`) garante que processar de novo não cria um segundo registro. **Reprocessar duas vezes = reprocessar uma vez.**

---

### `IEventReprocessor` — a porta entre módulos

**Arquivo:** `src/Contracts/Messaging.Contracts/Processing/IEventReprocessor.cs`

```csharp
public interface IEventReprocessor
{
    Task<ReprocessOutcome> ReprocessAsync(Guid eventId, CancellationToken cancellationToken);
}

public enum ReprocessOutcome
{
    Requeued,     // evento reenfileirado com sucesso
    AlreadyDone,  // evento já estava Done — reenfileirar seria redundante
    NotFound,     // evento não existe com esse ID
}
```

**Por que está em `Messaging.Contracts` e não em `Messaging.Processing`?**

O módulo Operations precisa pedir ao Processing para reprocessar. Mas Operations não pode depender de Processing diretamente — isso violaria a fronteira de módulos (e quebraria o `ModuleBoundaryTests`). A solução é a porta pública em `Contracts`: Operations depende da interface, Processing implementa a interface. Nenhum módulo conhece o interno do outro.

```
Operations ──▶ IEventReprocessor (Contracts) ◀── Processing (implementa)
              sem acoplamento direto
```

---

### `ReprocessEventHandler` — caso de uso do módulo Operations

**Arquivo:** `src/Modules/Messaging.Operations/ReprocessEventHandler.cs`

```csharp
public sealed class ReprocessEventHandler
{
    private readonly IEventReprocessor _reprocessor;

    public Task<ReprocessOutcome> HandleAsync(Guid eventId, CancellationToken ct)
    {
        if (eventId == Guid.Empty)
            throw new ArgumentException("eventId is required", nameof(eventId));

        return _reprocessor.ReprocessAsync(eventId, ct);
    }
}
```

Simples e intencional: valida o `eventId` na borda e delega tudo ao módulo Processing via contrato. O módulo Operations não sabe nada sobre como o Processing reenfileira — só sabe que pode pedir.

---

### Os três resultados possíveis de um reprocessamento

| `ReprocessOutcome` | O que aconteceu | Resposta HTTP sugerida |
|---|---|---|
| `Requeued` | evento reenfileirado com sucesso | `202 Accepted` |
| `AlreadyDone` | evento já estava `Done` — nada a fazer | `200 OK` |
| `NotFound` | ID não existe no banco | `404 Not Found` |

O `AlreadyDone` é especialmente importante: se o operador chamar `/reprocess` duas vezes rapidamente, a segunda chamada encontra o evento já `Done` e retorna `200` sem efeito. **A API de reprocessamento é idempotente.**

---

### Comparação: backoff exponencial planejado

A spec 003 e a regra `messaging-reliability.md` definem backoff exponencial com `MaxAttempts = 5` (padrão). Sequência de espera entre tentativas:

| Tentativa | Espera antes de tentar |
|---|---|
| 1 | imediata (primeira entrega) |
| 2 | ~1 segundo |
| 3 | ~2 segundos |
| 4 | ~4 segundos |
| 5 | ~8 segundos → se falhar → DLQ |

O backoff é implementado no consumer (Kafka/RabbitMQ), não no handler — o handler falha normalmente, o consumer decide quando retentar.

---

### O que já existe vs. o que está pendente

| Componente | Status | Arquivo |
|---|---|---|
| `StuckEventDetector` (decisão de MoveToDlq) | ✅ implementado | `src/Modules/Messaging.Processing/StuckEventDetector.cs` |
| `IEventReprocessor` (porta pública) | ✅ implementado | `src/Contracts/.../IEventReprocessor.cs` |
| `ReprocessEventHandler` (caso de uso) | ✅ implementado | `src/Modules/Messaging.Operations/ReprocessEventHandler.cs` |
| Job de recuperação com lock por chave | ⏳ spec 003, T-02 | — |
| Implementação de `IEventReprocessor` | ⏳ spec 003, T-03 | — |
| Endpoints `/admin/events` | ⏳ spec 003, T-03 | — |
| Backoff exponencial no consumer | ⏳ spec 002, T-03 | — |
| Alertas para DLQ | ⏳ spec 003, T-04 | — |

---

## O que ela NÃO faz

- Não altera o banco diretamente — o reprocessamento passa pelo mesmo handler transacional do fluxo normal.
- Não cria um novo evento — reenfileira o evento original com a mesma `IdempotencyKey`.
- Não bypassa a validação de assinatura — o evento já passou por ela na ingestão; o reprocessamento começa do `RawEventAccepted`.
- Não envia notificação ao provedor original — o reprocessamento é interno; o provedor já recebeu `200` na ingestão.
- Não expõe o payload em logs — nem no reprocessamento. `EventId` e `CorrelationId` são o que aparece no trace.

---

## Argumentos / configuração

| Parâmetro | Padrão | Onde configurar |
|---|---|---|
| Máximo de tentativas | 5 | `StuckEventDetector(maxAttempts: 5)` |
| Timeout para considerar preso | 5 minutos | `StuckEventDetector(TimeSpan.FromMinutes(5))` |
| Perfil necessário para admin | `operations` | autenticação/autorização (pendente) |
| Formato de erro | `application/problem+json` (RFC 7807) | convenção da API |

---

## Exemplo de uso

### Rodar os testes do reprocessamento

```bash
dotnet test tests/Messaging.UnitTests -c Release --no-build --nologo \
  --filter "ReprocessEventHandler|StuckEventDetector" -v normal

# 8 testes:
# ReprocessEventHandlerTests → 2 testes
#   - evento válido → delega ao Processing e retorna Requeued
#   - id vazio → lança ArgumentException sem chamar Processing
# StuckEventDetectorTests    → 6 testes
#   - (já vistos na Sessão 5)
```

### Fluxo completo de reprocessamento (quando implementado)

```bash
# 1. Listar eventos com falha
curl -s http://localhost:5080/admin/events?status=Failed \
  -H "Authorization: Bearer <token-operations>"
# → [{"id": "...", "attempts": 5, "lastError": "timeout connecting to payment-service", ...}]

# 2. Ver o detalhe com o payload bruto
curl -s http://localhost:5080/admin/events/{id} \
  -H "Authorization: Bearer <token-operations>"
# → {"id": "...", "payload": "{...}", "attempts": 5, "lastError": "..."}

# 3. Reprocessar
curl -s -X POST http://localhost:5080/admin/events/{id}/reprocess \
  -H "Authorization: Bearer <token-operations>"
# → 202 Accepted (Requeued)

# 4. Reprocessar de novo (idempotente)
curl -s -X POST http://localhost:5080/admin/events/{id}/reprocess \
  -H "Authorization: Bearer <token-operations>"
# → 200 OK (AlreadyDone) — sem efeito duplicado
```

---

## Instruções operacionais — como rodar esta sessão

```bash
# 1. Rodar os testes desta sessão
dotnet test tests/Messaging.UnitTests -c Release --no-build --nologo \
  --filter "ReprocessEventHandler|StuckEventDetector" -v normal

# 2. Arquivos-chave para walkthrough ao vivo:
#    src/Contracts/Messaging.Contracts/Processing/IEventReprocessor.cs  ← porta pública
#    src/Modules/Messaging.Operations/ReprocessEventHandler.cs          ← caso de uso
#    src/Modules/Messaging.Processing/StuckEventDetector.cs             ← decisão MoveToDlq
#    specs/003-recovery-and-dlq.md                                       ← requisitos completos
#    .claude/rules/api-conventions.md                                    ← endpoints admin
#    .claude/rules/messaging-reliability.md                              ← regras de retry/DLQ
```

---

## O que você deve ser capaz de explicar ao seu chefe

**"O que é a DLQ e por que ela existe?"**
— Dead Letter Queue é o destino de eventos que falharam mais vezes do que o limite configurado (5 por padrão). Em vez de tentar para sempre — o que desperdiçaria recursos sem resultado — o sistema para e coloca o evento num lugar onde um operador pode inspecionar, entender a causa e decidir o que fazer.

**"O que acontece com um evento na DLQ?"**
— Ele fica com `status = 'Failed'` no banco, acessível pela API admin com o payload bruto, o número de tentativas e o último erro. Um operador com o perfil `operations` pode chamar `POST /admin/events/{id}/reprocess` para reenfileirá-lo.

**"Reprocessar não duplica o efeito?"**
— Não. O reprocessamento usa exatamente o mesmo caminho do fluxo normal — mesma `IdempotencyKey`, mesmo handler, mesmo upsert. Se o evento já foi processado com sucesso enquanto esperava na DLQ, a resposta é `AlreadyDone` e nada acontece. Chamar reprocessar duas vezes também é seguro.

**"Por que o módulo Operations não chama o Processing diretamente?"**
— Porque módulos só se comunicam por contratos em `Messaging.Contracts`. O módulo Operations usa a interface `IEventReprocessor` — não sabe nada sobre como o Processing funciona internamente. Isso é o monólito modular na prática: fronteiras reais, verificadas por testes automatizados.

---

## Próxima sessão

**Sessão 7 — Chaos Tests: os 6 cenários de falha**

Vamos ver os cenários de falha obrigatórios do projeto e como cada um prova uma garantia específica do sistema — desde webhooks enviados em paralelo até o broker ficando fora do ar no pior momento.
