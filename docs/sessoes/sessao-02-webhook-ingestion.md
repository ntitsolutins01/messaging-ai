# Sessão 2 — Webhook Ingestion: receber e gravar o evento

---

## Objetivo

Entender o caminho completo que um webhook percorre desde a chegada na API até ser gravado com segurança no banco — e por que cada detalhe dessa sequência importa para a confiabilidade do sistema.

---

## Quando usar

Esse fluxo é o ponto de entrada de tudo. Qualquer conversa sobre:

- Como o sistema garante que um evento não se perde mesmo se o worker cair.
- Por que a API responde `200` mesmo para eventos duplicados.
- Por que o corpo bruto é gravado antes de qualquer parse.
- Como o sistema protege contra JSON malformado ou provedores sem ID de evento.

---

## Quando NÃO usar

- **Não use este fluxo para comunicação interna entre módulos** — eventos entre módulos trafegam pelo outbox, não pelo endpoint de webhook.
- **Não valide o schema do payload aqui** — o parse e validação do conteúdo acontecem no worker, atrás de um adaptador versionado. A API só grava o bruto.
- **Não chame `IRawEventStore` diretamente de outro módulo** — ele é interno ao módulo Ingestion. Outros módulos consomem `RawEventAccepted` via outbox.

---

## O que ela faz

### Visão geral do fluxo

```
POST /webhooks/{provider}
        │
        ▼
  1. Validar provider (regex [a-z0-9-]{1,50})
        │
        ▼
  2. Ler corpo inteiro (limite 1 MiB)
        │
        ▼
  3. WebhookIngestionService.IngestAsync()
        │
        ├─── 3a. Validar assinatura HMAC-SHA256
        │         └── inválida → retorna 401 (sem gravar nada)
        │
        ├─── 3b. Derivar IdempotencyKey
        │         ├── tem X-Event-Id → usa o ID do provedor
        │         └── não tem       → SHA-256 do corpo como fallback
        │
        ├─── 3c. Montar RawEvent + RawEventAccepted (outbox)
        │
        └─── 3d. IRawEventStore.TryAddAsync (1 transação)
                  ├── novo  → grava os dois, retorna Accepted  → 200
                  └── dupl. → não grava nada, retorna Duplicate → 200
```

**Por que duplicata também retorna 200?** O provedor (Stripe, GitHub, etc.) continua reenviando até receber `200`. Se retornássemos `409 Conflict`, ele retentaria para sempre. Com `200`, ele para — e nós simplesmente ignoramos o evento silenciosamente.

---

### Camada 1 — endpoint HTTP (`Program.cs`)

**Arquivo:** `src/Hosts/Messaging.Api/Program.cs`

Três validações acontecem antes de qualquer lógica de negócio:

```csharp
// 1. Nome do provider: só letras minúsculas, números e hífens, 1-50 chars
var providerPattern = new Regex("^[a-z0-9-]{1,50}$", ...);
if (!providerPattern.IsMatch(provider)) return Results.BadRequest();

// 2. Limite de tamanho do corpo
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 1_048_576); // 1 MiB

// 3. Lê o corpo inteiro antes de passar para o serviço (necessário para o HMAC)
using var buffer = new MemoryStream();
await request.Body.CopyToAsync(buffer, cancellationToken);
```

**Por que o provider é validado por regex?** Evita injeção via nome do provider (ex.: `../../admin`). Um atacante poderia tentar usar o nome do provider como vetor. O regex restringe ao conjunto seguro.

**Por que ler o corpo inteiro antes de validar a assinatura?** O HMAC precisa do corpo completo para calcular o hash. Não é possível validar parcialmente.

O endpoint lê dois cabeçalhos do provedor:
- `X-Signature` → assinatura HMAC
- `X-Event-Id` → ID único do evento (opcional)

---

### Camada 2 — `WebhookIngestionService`

**Arquivo:** `src/Modules/Messaging.Ingestion/WebhookIngestionService.cs`

É o caso de uso central do módulo Ingestion. Coordena: validação → derivação de chave → gravação.

```csharp
public async Task<IngestionOutcome> IngestAsync(...)
{
    // Passo 1 — assinatura
    if (_validator.Validate(body, signatureHeader) != SignatureResult.Valid)
        return IngestionOutcome.InvalidSignature;  // nada gravado

    // Passo 2 — chave de idempotência
    var eventId = string.IsNullOrWhiteSpace(providerEventId)
        ? HashOf(body)       // fallback: SHA-256 do corpo
        : providerEventId;
    var key = IdempotencyKey.From(provider, eventId);

    // Passo 3 — gravar evento bruto + outbox na mesma transação
    var raw     = new RawEvent(id, key, provider, body_como_string, now);
    var message = new RawEventAccepted(id, key.Value, provider, now);
    var inserted = await _store.TryAddAsync(raw, message, cancellationToken);

    return inserted ? IngestionOutcome.Accepted : IngestionOutcome.Duplicate;
}
```

**Por que não fazer o parse do JSON aqui?** O payload bruto é gravado como string antes de qualquer parse. Isso garante que:
1. Nunca perdemos um evento por erro de parse (JSON malformado ainda chega ao banco).
2. Se o schema do provedor mudar, podemos reparsar o bruto depois — sem perda de dados.
3. O parse fica isolado no worker, atrás de um adaptador versionado (`v1`, `v2`...).

**Fallback de ID — por que SHA-256 do corpo?**

Alguns provedores não enviam um ID de evento no cabeçalho. Sem ID, a deduplicação por chave seria impossível. O SHA-256 do corpo garante que envios do **mesmo conteúdo** geram a mesma chave — se o provedor reenviar byte-a-byte o mesmo corpo (que é o que acontece num retry normal), a deduplicação funciona.

```csharp
private static string HashOf(byte[] body) =>
    "sha256-" + Convert.ToHexString(SHA256.HashData(body)).ToLowerInvariant();
// Exemplo: "sha256-a3f8c2..."
```

**Por que o trace nunca loga o payload?**

```csharp
// Só provedor e resultado vão para o trace: nunca payload, segredo ou assinatura.
activity?.SetTag("messaging.provider", provider);
activity?.SetTag("messaging.outcome", outcome.ToString());
```

Regra de segurança: payload pode conter dados pessoais ou financeiros. Logar o corpo seria uma violação de privacidade e uma porta de entrada para vazamento de dados.

---

### Camada 3 — `RawEvent` e `RawEventAccepted`

**`RawEvent`** (`src/Modules/Messaging.Ingestion/RawEvent.cs`) — tipo interno do módulo Ingestion:

```csharp
public sealed record RawEvent(
    Guid Id,
    IdempotencyKey Key,
    string Provider,
    string Payload,       // corpo bruto como string — sem parse
    DateTimeOffset ReceivedAt);
```

**`RawEventAccepted`** (`src/Contracts/Messaging.Contracts/Ingestion/RawEventAccepted.cs`) — contrato público:

```csharp
public sealed record RawEventAccepted(
    Guid EventId,
    string IdempotencyKey,
    string Provider,
    DateTimeOffset ReceivedAt);
```

**Por que dois tipos diferentes para o mesmo evento?**

| | `RawEvent` | `RawEventAccepted` |
|---|---|---|
| **Onde fica** | módulo Ingestion (interno) | `Messaging.Contracts` (público) |
| **Quem vê** | só Ingestion | qualquer módulo |
| **Tem payload** | sim | **não** |
| **Para que serve** | gravar no banco | notificar outros módulos via outbox |

O módulo Processing nunca vê o payload bruto diretamente — ele recebe `RawEventAccepted` (sem payload) e vai ao banco buscar o evento pelo ID quando precisar. Isso enforça a fronteira entre módulos.

---

### Camada 4 — `IRawEventStore` e a implementação em memória

**`IRawEventStore`** (`src/Modules/Messaging.Ingestion/IRawEventStore.cs`):

```csharp
Task<bool> TryAddAsync(RawEvent rawEvent, RawEventAccepted outboxMessage, CancellationToken ct);
// Retorna false sem efeito algum quando a chave já existe.
// Em produção: INSERT ... ON CONFLICT (idempotency_key) DO NOTHING
```

A assinatura do método força a invariante: **evento bruto e mensagem de outbox sempre entram juntos ou não entram**. Não existe `AddRawEvent` separado de `AddToOutbox`.

**`InMemoryRawEventStore`** — implementação de teste:

```csharp
// ConcurrentDictionary.TryAdd é atômico:
// só uma chamada concorrente por chave vence, as demais retornam false
var inserted = _events.TryAdd(rawEvent.Key.Value, rawEvent);
if (inserted) _outbox.Enqueue(outboxMessage);
```

Em produção, essa implementação é substituída por PostgreSQL com `UNIQUE(idempotency_key)`. O `ConcurrentDictionary` simula esse comportamento em memória para os testes unitários — sem precisar de banco.

---

## O que ela NÃO faz

- Não faz parse do JSON — isso é responsabilidade do worker.
- Não publica direto no broker — a mensagem entra na outbox; quem publica no broker é o relay de outbox.
- Não envia resposta `409` para duplicatas — sempre `200` para que o provedor pare de reenviar.
- Não valida o schema do evento — campos desconhecidos são guardados junto com o bruto.
- Não grava payload em logs ou traces — nunca.

---

## Argumentos / configuração

| Configuração | Onde | Valor de dev |
|---|---|---|
| `Webhook:Secret` | user-secrets / `Webhook__Secret` env var | `dev-secret` |
| Limite do corpo | `Program.cs` (hardcoded) | 1 MiB |
| Tolerância de assinatura | `Program.cs` (hardcoded) | 5 minutos |
| Cabeçalho de assinatura | `X-Signature` | — |
| Cabeçalho de ID do evento | `X-Event-Id` | — |

---

## Exemplo de uso

### Subir a API localmente

```bash
# Em um terminal separado:
dotnet run --project src/Hosts/Messaging.Api
# API sobe em http://localhost:5080
```

### Enviar um webhook válido

```bash
# Gerar a assinatura:
python - <<'EOF'
import hmac, hashlib, time
secret = b"dev-secret"
body   = b'{"id":"evt_123","type":"payment.created","amount":100}'
ts     = int(time.time())
msg    = f"{ts}.".encode() + body
sig    = hmac.new(secret, msg, hashlib.sha256).hexdigest()
print(f"t={ts},v1={sig}")
EOF

# Enviar (substituir o valor do X-Signature pela saída acima):
curl -s -w "\nHTTP %{http_code}" -X POST http://localhost:5080/webhooks/acme \
  -H "Content-Type: application/json" \
  -H "X-Event-Id: evt_123" \
  -H "X-Signature: t=TIMESTAMP,v1=HASH" \
  -d '{"id":"evt_123","type":"payment.created","amount":100}'
# Resposta: HTTP 200

# Enviar exatamente igual de novo:
# Resposta: HTTP 200 (duplicata ignorada silenciosamente)
```

### Testar assinatura inválida

```bash
curl -s -o /dev/null -w "HTTP %{http_code}" -X POST http://localhost:5080/webhooks/acme \
  -H "X-Signature: t=1,v1=00" \
  -d '{}'
# Resposta: HTTP 401
```

### Testar provider inválido

```bash
curl -s -o /dev/null -w "HTTP %{http_code}" -X POST http://localhost:5080/webhooks/ACME_INVALIDO \
  -d '{}'
# Resposta: HTTP 400
```

---

## Instruções operacionais — como rodar esta sessão

```bash
# 1. Rodar só os testes desta sessão
dotnet test tests/Messaging.UnitTests -c Release --no-build --nologo \
  --filter "WebhookIngestionService" -v normal

# 2. Resultado esperado: 6 testes passando
#    - assinatura inválida → não grava nada
#    - evento novo → grava evento + outbox
#    - evento repetido → duplicata, sem novo efeito
#    - provedor sem ID → deduplicação por hash do corpo
#    - 20 envios concorrentes → 1 Accepted, 19 Duplicate
#    - corpo não-JSON → grava o bruto mesmo assim

# 3. Arquivos-chave para walkthrough ao vivo:
#    src/Modules/Messaging.Ingestion/WebhookIngestionService.cs   ← caso de uso central
#    src/Modules/Messaging.Ingestion/IRawEventStore.cs            ← contrato da persistência
#    src/Modules/Messaging.Ingestion/RawEvent.cs                  ← tipo interno
#    src/Contracts/Messaging.Contracts/Ingestion/RawEventAccepted.cs ← contrato público
#    src/Hosts/Messaging.Api/Program.cs                           ← endpoint HTTP
#    tests/Messaging.UnitTests/WebhookIngestionServiceTests.cs    ← 6 cenários
```

---

## O que você deve ser capaz de explicar ao seu chefe

**"Por que a API responde 200 mesmo quando o evento já chegou antes?"**
— O provedor (Stripe, GitHub) interpreta qualquer resposta diferente de 2xx como falha e fica reenviando. Respondendo `200` para duplicatas, dizemos "recebi, pode parar" — e simplesmente descartamos o segundo processamento. Do ponto de vista do provedor, foi um sucesso.

**"O que acontece se o JSON do webhook estiver malformado?"**
— A API grava o corpo bruto assim mesmo e responde `200`. O parse acontece depois, no worker. Se o schema estiver errado, o worker coloca o evento na DLQ com o erro de parse — e o corpo bruto ainda está lá para ser inspecionado ou reprocessado quando o adaptador for corrigido.

**"Por que dois tipos — `RawEvent` e `RawEventAccepted` — para o mesmo evento?"**
— `RawEvent` é o tipo interno do módulo Ingestion e carrega o payload bruto. `RawEventAccepted` é o contrato público (em `Messaging.Contracts`) que outros módulos consomem — sem payload, só metadados. Isso enforça a fronteira: o módulo Processing nunca lê a tabela do Ingestion diretamente.

**"Por que gravar evento bruto E outbox na mesma operação?"**
— Se gravassem separado, haveria uma janela de falha entre os dois: o evento existe no banco mas a outbox não foi publicada. O worker nunca saberia que esse evento chegou. Com `TryAddAsync` atômico, ou os dois entram ou nenhum entra — nunca metade.

---

## Próxima sessão

**Sessão 3 — Outbox Pattern: por que existe e como funciona**

Agora que sabemos que a mensagem entra na outbox junto com o evento bruto, vamos entender por que não publicamos direto no Kafka/RabbitMQ e o que o relay de outbox faz para garantir que nenhuma mensagem se perde.
