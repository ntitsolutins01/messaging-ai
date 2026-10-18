# Sessão 1 — IdempotencyKey e assinatura HMAC-SHA256

---

## Objetivo

Entender como o sistema garante que **dois webhooks idênticos não produzem efeito duplo** e como ele verifica que a requisição foi enviada por quem diz ter enviado.

---

## Quando usar

Esses dois conceitos entram em jogo sempre que:

- Um provedor reenviar o mesmo evento por causa de timeout, queda de rede ou política de retry (ex.: o Stripe reenvia por até 72 horas).
- Você precisar explicar por que o sistema não processa o mesmo evento duas vezes.
- Alguém questionar como a API sabe que a requisição não foi forjada por um atacante.

---

## Quando NÃO usar

- **Não use `IdempotencyKey` para controlar duplicatas de UX** (ex.: duplo clique em botão). Essa chave é para deduplicação de eventos de integração entre sistemas.
- **Não use HMAC-SHA256 como autenticação de usuário.** Ele autentica a *origem* (qual sistema enviou), não a *identidade* (quem está logado).
- **Não derive a chave de idempotência a partir de timestamp** — timestamps não são únicos sob retry. Use o ID do evento que o provedor já fornece.

---

## O que ela faz

### IdempotencyKey — passo a passo

**Arquivo:** `src/Shared/Messaging.SharedKernel/IdempotencyKey.cs`

```
1. Recebe: provider = "ACME", providerEventId = "evt_123"
2. Valida que nenhum dos dois está vazio
3. Normaliza o provider para lowercase e remove espaços: "acme"
4. Monta o valor: "acme:evt_123"
5. Garante que o resultado tem no máximo 200 caracteres
6. Retorna um record imutável com esse valor
```

O resultado `"acme:evt_123"` vai para o banco como chave única. Se o mesmo evento chegar duas vezes, as duas tentativas de gravar produzem a mesma chave — o banco rejeita a segunda com violação de `UNIQUE` e o sistema responde `200` sem processar nada de novo.

```
Chegada 1: "acme" + "evt_123" → "acme:evt_123" → GRAVA ✅
Chegada 2: "acme" + "evt_123" → "acme:evt_123" → UNIQUE violation → IGNORA ✅ (ainda responde 200)
```

**Por que `record` imutável?** Uma chave de idempotência nunca deve mudar depois de criada. `record` no C# fornece imutabilidade e `Equals` por valor automaticamente — dois `IdempotencyKey` com o mesmo `Value` são iguais (`==`).

**Por que normalizar o provider para lowercase?** Evita que `"ACME"` e `"acme"` gerem chaves diferentes para o mesmo evento. A normalização acontece uma vez, na criação, e nunca mais.

---

### WebhookSignatureValidator — passo a passo

**Arquivo:** `src/Modules/Messaging.Ingestion/WebhookSignatureValidator.cs`

O provedor assina cada webhook que envia. A API verifica essa assinatura antes de fazer qualquer coisa.

**Formato do cabeçalho recebido:**
```
X-Signature: t=1760000000,v1=a3f8...
             ↑ unix timestamp  ↑ HMAC-SHA256 em hexadecimal
```

**Como o provedor gera a assinatura** (mesma lógica usada nos testes em `WebhookSigner.cs`):
```
mensagem = "{timestamp}.{corpo_do_webhook}"
assinatura = HMAC-SHA256(segredo_compartilhado, mensagem)
cabeçalho = "t={timestamp},v1={assinatura_em_hex}"
```

**Como o validador verifica** (4 verificações independentes):

```
Passo 1 — Presença:   cabeçalho existe?                         → Missing se não
Passo 2 — Formato:    "t=<número>,v1=<hex válido>"?             → Malformed se não
Passo 3 — Conteúdo:   HMAC calculado == HMAC recebido?          → Mismatch se não
Passo 4 — Frescor:    timestamp está dentro da janela de 5 min? → Expired se não
```

**Por que `CryptographicOperations.FixedTimeEquals` e não `==`?**

```csharp
// ERRADO — vulnerável a timing attack:
if (computed == provided)

// CORRETO — tempo constante independente de onde diverge:
if (CryptographicOperations.FixedTimeEquals(Compute(timestamp, body), provided))
```

Um atacante pode medir o tempo de comparação byte a byte: se a API retorna mais rápido quando o primeiro byte está errado, ele descobre a assinatura correta por tentativa e erro em poucos milhares de requisições. `FixedTimeEquals` sempre compara todos os bytes e não vaza informação por tempo.

**Por que verificar o timestamp (janela de 5 minutos)?**

Sem janela de tempo, um atacante que capturar um webhook legítimo pode reenviá-lo indefinidamente — o corpo e a assinatura são sempre válidos. Com a janela, uma requisição com mais de 5 minutos é rejeitada como `Expired`, mesmo que a assinatura seja matematicamente correta.

```
t=agora-4min  → Valid   ✅ (dentro da janela)
t=agora-6min  → Expired ❌ (fora da janela, mesmo com assinatura válida)
t=agora+6min  → Expired ❌ (no futuro além da tolerância)
```

**Por que verificar também o futuro?** Protege contra relógio desincronizado no servidor do provedor que envie eventos com timestamp futuro — sem esse check, um evento poderia ter uma janela de validade muito maior que 5 minutos.

---

### IClock — por que existe?

**Arquivo:** `src/Shared/Messaging.SharedKernel/IClock.cs`

```csharp
public interface IClock { DateTimeOffset UtcNow { get; } }
public sealed class SystemClock : IClock { ... }
```

Em produção, `SystemClock` retorna `DateTimeOffset.UtcNow`. Em teste, `FakeClock` retorna uma data fixa que o teste controla.

**Sem `IClock`, os testes de expiração falhariam de forma não determinística** — o tempo real avança enquanto o teste roda, e uma assinatura "de 4 minutos atrás" poderia ter expirado antes do assert. Com `FakeClock`, o relógio nunca avança a não ser que o teste mova explicitamente.

---

## O que ela NÃO faz

- `IdempotencyKey` não grava nada — é um value object puro. A gravação com restrição `UNIQUE` acontece em `IRawEventStore`.
- `WebhookSignatureValidator` não faz nada com o corpo do webhook — só valida a assinatura. O parse do conteúdo acontece depois, no worker, atrás de um adaptador versionado.
- A janela de 5 minutos não é configurável por provedor nesta implementação — é um único `TimeSpan` passado no construtor.
- Não há lista de segredos por provedor nesta versão — um único segredo compartilhado para todos os provedores.

---

## Argumentos / configuração

| Parâmetro | Onde configurar | Valor de dev |
|---|---|---|
| `Webhook:Secret` | `dotnet user-secrets` / env var | `dev-secret` |
| Tolerância de tempo | hardcoded no `Program.cs` | `TimeSpan.FromMinutes(5)` |

O segredo nunca vai para o repositório. Em produção, vem de um cofre de segredos (Azure Key Vault, AWS Secrets Manager, etc.).

---

## Exemplo de uso

### Gerar uma assinatura válida manualmente (para testar com curl)

```bash
# No terminal — Python para gerar o cabeçalho:
python3 - <<'EOF'
import hmac, hashlib, time

secret = b"dev-secret"
body   = b'{"id":"evt_123","type":"payment.created"}'
ts     = int(time.time())
msg    = f"{ts}.".encode() + body
sig    = hmac.new(secret, msg, hashlib.sha256).hexdigest()
print(f"t={ts},v1={sig}")
EOF
```

### Enviar o webhook com curl

```bash
# Cole a saída do comando anterior no cabeçalho X-Signature:
curl -s -X POST http://localhost:5080/webhooks/acme \
  -H "Content-Type: application/json" \
  -H "X-Signature: t=1760000000,v1=a3f8..." \
  -d '{"id":"evt_123","type":"payment.created"}'

# Resposta esperada: 200
# Enviar exatamente igual novamente: ainda 200, mas sem efeito duplicado
```

### Testar assinatura inválida

```bash
curl -s -o /dev/null -w "%{http_code}" -X POST http://localhost:5080/webhooks/acme \
  -H "Content-Type: application/json" \
  -H "X-Signature: t=0,v1=invalido" \
  -d '{"id":"evt_999"}'
# Resposta esperada: 401
```

---

## Instruções operacionais — como rodar esta sessão

```bash
# 1. Garantir que os containers ainda estão de pé
docker compose ps

# 2. Rodar só os testes desta sessão e ver os nomes dos cenários
dotnet test tests/Messaging.UnitTests -c Release --no-build --nologo \
  --filter "IdempotencyKey|WebhookSignature" -v normal

# 3. Resultado esperado: 23 testes passando
#    IdempotencyKeyTests         →  5 testes
#    WebhookSignatureValidatorTests → 10 testes (com 6 inline data cases)

# 4. Abrir os arquivos-chave para walkthrough ao vivo:
#    src/Shared/Messaging.SharedKernel/IdempotencyKey.cs
#    src/Modules/Messaging.Ingestion/WebhookSignatureValidator.cs
#    src/Modules/Messaging.Ingestion/SignatureResult.cs
#    tests/Messaging.TestSupport/WebhookSigner.cs
#    tests/Messaging.UnitTests/IdempotencyKeyTests.cs
#    tests/Messaging.UnitTests/WebhookSignatureValidatorTests.cs
```

---

## O que você deve ser capaz de explicar ao seu chefe

**"Por que vocês não usam só uma senha ou token para validar o webhook?"**
— Senha ou Bearer token não assinam o corpo da mensagem — um atacante pode roubar o token e enviar qualquer conteúdo. HMAC assina o corpo inteiro mais o timestamp: alterar qualquer byte do corpo invalida a assinatura, e um token capturado expira em 5 minutos.

**"O que acontece se o mesmo evento chegar duas vezes?"**
— A chave de idempotência `"acme:evt_123"` é derivada do ID do evento, que é sempre o mesmo. O banco tem uma restrição `UNIQUE` nessa chave. A segunda tentativa de gravação falha silenciosamente e a API retorna `200` como se tudo estivesse bem — o provedor não precisa saber que ignoramos o duplicado.

**"Por que vocês guardam o timestamp na assinatura?"**
— Para evitar replay attack: um atacante que capture um webhook legítimo poderia reenviá-lo no futuro. Com o timestamp dentro da assinatura, qualquer requisição com mais de 5 minutos é rejeitada mesmo que a assinatura seja matematicamente correta.

**"O que é timing attack e por que isso importa?"**
— Se você compara assinaturas byte a byte e para quando encontra uma diferença, o tempo de resposta da API vaza informação sobre onde a assinatura diverge. Um atacante pode usar isso para descobrir o segredo em poucos milhares de tentativas. `CryptographicOperations.FixedTimeEquals` sempre compara todos os bytes e retorna em tempo constante.

---

## Próxima sessão

**Sessão 2 — Webhook Ingestion: receber e gravar o evento**

Vamos ver o que acontece após a validação da assinatura: como o corpo bruto do webhook é gravado no banco antes de qualquer parse, e por que isso é importante para a confiabilidade do sistema.
