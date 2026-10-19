# Sessão 4 — Broker: Kafka e RabbitMQ com troca por variável de ambiente

---

## Objetivo

Entender por que o código de domínio nunca fala diretamente com Kafka ou RabbitMQ — e como a arquitetura garante, através de testes executáveis, que isso não acontece mesmo que alguém tente.

---

## Quando usar

Esse conceito entra em jogo quando:

- Você precisa comparar Kafka e RabbitMQ sob o mesmo fluxo e os mesmos testes.
- Alguém questiona se trocar de broker exigiria reescrever a lógica de negócio.
- Você quiser explicar como isolar tecnologia de infraestrutura do domínio.

---

## Quando NÃO usar

- **Não exponha recursos específicos de broker na abstração** — partições do Kafka, exchanges do RabbitMQ, consumer groups — esses conceitos ficam nos adapters, não em `IMessageBus`.
- **Não use a abstração para operações administrativas** (criar tópicos, purgar filas) — essas operações ficam fora do fluxo de negócio.
- **Não use um único adapter para os dois brokers** — cada broker tem seu próprio projeto (`Messaging.Brokers.Kafka`, `Messaging.Brokers.RabbitMq`) para que os pacotes de um não contaminem o outro.

---

## O que ela faz

### O problema: acoplamento com o broker

Sem abstração, o código de domínio ficaria assim:

```csharp
// ERRADO — domínio conhece Kafka:
using Confluent.Kafka;

public class WebhookIngestionService
{
    private readonly IProducer<string, string> _kafkaProducer; // ← lock-in
    ...
    await _kafkaProducer.ProduceAsync("raw-events", new Message<string,string> { ... });
}
```

Problemas:
1. Trocar para RabbitMQ exige reescrever `WebhookIngestionService`.
2. Os testes unitários precisam de um Kafka real (ou de um mock frágil).
3. A comparação entre brokers é impossível sem duplicar código.

---

### A solução: `IMessageBus` como barreira

A decisão está no ADR 0003:

> "Módulos publicam e consomem só por `IMessageBus`. Cada broker é um adapter em `Messaging.Brokers.Kafka` / `Messaging.Brokers.RabbitMq`, escolhido por `Messaging__Broker`."

**Arquitetura planejada:**

```
┌─────────────────────────────────────────────────────┐
│  Módulos de domínio (Ingestion, Processing, Ops)    │
│  só conhecem IMessageBus — sem import de Kafka/AMQP │
└─────────────────┬───────────────────────────────────┘
                  │ IMessageBus (Messaging.Contracts)
       ┌──────────┴──────────┐
       ▼                     ▼
Messaging.Brokers.Kafka   Messaging.Brokers.RabbitMq
(Confluent.Kafka)         (RabbitMQ.Client)
       │                     │
       ▼                     ▼
  apache/kafka:3.8.0    rabbitmq:3-management
  localhost:9092        localhost:5672
```

**Troca com uma variável de ambiente — sem alterar código:**

```bash
# Usar Kafka:
Messaging__Broker=Kafka dotnet run --project src/Hosts/Messaging.Api

# Usar RabbitMQ:
Messaging__Broker=RabbitMq dotnet run --project src/Hosts/Messaging.Api
```

O host (`Program.cs`) lê a variável e registra o adapter correto no contêiner de DI. O domínio não sabe qual foi escolhido.

---

### O que já existe hoje: a garantia executável

O `IMessageBus` ainda não existe em código — é spec 004, pendente. Mas **o teste de arquitetura que garante a fronteira já está implementado e já passa no quality gate**:

```csharp
// tests/Messaging.ArchitectureTests/ModuleBoundaryTests.cs
[Fact]
public void Dominio_dos_modulos_nao_conhece_kafka_nem_rabbitmq()
{
    ShouldNotDependOn(IngestionAssembly,   "Confluent.Kafka", "RabbitMQ.Client");
    ShouldNotDependOn(ProcessingAssembly,  "Confluent.Kafka", "RabbitMQ.Client");
    ShouldNotDependOn(OperationsAssembly,  "Confluent.Kafka", "RabbitMQ.Client");
    ShouldNotDependOn(SharedKernelAssembly,"Confluent.Kafka", "RabbitMQ.Client");
}
```

Isso é uma **regra executável**: qualquer desenvolvedor que adicionar `using Confluent.Kafka` em um módulo de domínio vai quebrar o quality gate imediatamente. A fronteira não depende de disciplina — depende de CI.

---

### O que os dois brokers fazem (ambos já rodando localmente)

Ambos estão definidos no `docker-compose.yml` e sobem com `docker compose up -d`:

**Kafka** — `apache/kafka:3.8.0` — porta 9092

```yaml
kafka:
  image: apache/kafka:3.8.0
  environment:
    KAFKA_PROCESS_ROLES: broker,controller       # KRaft: sem ZooKeeper
    KAFKA_ADVERTISED_LISTENERS: PLAINTEXT://localhost:9092
    KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR: 1   # single-node para dev
```

Kafka é um **log distribuído imutável**. Mensagens são escritas em tópicos e cada consumidor mantém seu próprio offset — pode reler mensagens antigas, ter múltiplos grupos independentes e replay a partir de qualquer ponto.

**RabbitMQ** — `rabbitmq:3-management` — porta 5672 / management 15672

```yaml
rabbitmq:
  image: rabbitmq:3-management
  environment:
    RABBITMQ_DEFAULT_USER: poc
    RABBITMQ_DEFAULT_PASS: poc
```

RabbitMQ é um **message broker tradicional** (AMQP). Mensagens são entregues a filas via exchanges e removidas após o ack. Mais simples para padrões de roteamento (direct, fanout, topic, headers) mas sem replay nativo.

---

### Comparação relevante para a POC

| Característica | Kafka | RabbitMQ |
|---|---|---|
| Modelo | log imutável | fila com ack |
| Replay de mensagens | sim (por offset) | não (mensagem é removida após ack) |
| Múltiplos consumidores | sim (consumer groups independentes) | sim (via fanout exchange) |
| Retenção configurável | sim (por tempo/tamanho) | não (a mensagem some após ack) |
| Roteamento avançado | não (só por tópico/partição) | sim (exchanges: direct, topic, headers) |
| Throughput | muito alto (log sequencial em disco) | alto (memória + disco) |
| Operação | mais complexo (tópicos, partições, offsets) | mais simples |
| DLQ nativa | sim (Dead Letter Topic) | sim (Dead Letter Exchange) |

**Para esta POC:** a abstração `IMessageBus` expõe só o mínimo comum — publish, subscribe, ack, DLQ. Recursos específicos (replay por offset do Kafka, routing keys do RabbitMQ) ficam fora da abstração. O ADR 0003 documenta que isso pode ser revisitado se os números da POC mostrarem que um recurso específico é indispensável.

---

### Os outros testes de fronteira (que já passam)

O mesmo arquivo garante também a separação entre módulos:

```csharp
[Fact] void Ingestion_nao_depende_de_outros_modulos() // Processing, Operations
[Fact] void Processing_nao_depende_de_outros_modulos() // Ingestion, Operations
[Fact] void Operations_nao_depende_de_outros_modulos() // Ingestion, Processing
[Fact] void SharedKernel_nao_depende_de_modulos_nem_de_contratos()
[Fact] void Contracts_nao_dependem_de_modulos()
```

São 6 regras, todas executadas como testes pelo `dotnet test`. Se qualquer uma falhar, o quality gate reprova. O monólito modular não depende de disciplina — depende de gate.

---

## O que ela NÃO faz

- `IMessageBus` ainda não existe em código — é pendente (spec 004). O que existe hoje são os adapters MCP (para Claude Code inspecionar as filas) e os containers Docker.
- A abstração não expõe recursos específicos de cada broker — replay por offset, routing keys, partições. Esses ficam no adapter.
- A troca por env var ainda não está implementada no `Program.cs` — faz parte da spec 004.
- Os adapters `Messaging.Brokers.Kafka` e `Messaging.Brokers.RabbitMq` ainda não existem como projetos — serão criados na spec 004.

---

## Argumentos / configuração

| Configuração | Onde | Valores aceitos |
|---|---|---|
| Broker ativo | `Messaging__Broker` (env var) | `Kafka` \| `RabbitMq` |
| Kafka | `docker-compose.yml` | localhost:9092 |
| RabbitMQ | `docker-compose.yml` | localhost:5672 (management: 15672) |

---

## Exemplo de uso

### Verificar que os dois brokers estão rodando

```bash
docker compose ps
# messaging-ai-kafka-1     Up   0.0.0.0:9092->9092/tcp
# messaging-ai-rabbitmq-1  Up   0.0.0.0:5672->5672/tcp, 0.0.0.0:15672->15672/tcp
```

### Acessar o management do RabbitMQ (interface visual)

```
http://localhost:15672
usuário: poc  senha: poc
```

### Rodar os testes de arquitetura que garantem a fronteira

```bash
dotnet test tests/Messaging.ArchitectureTests -c Release --no-build --nologo -v normal
# 6 testes passando — incluindo "Dominio_dos_modulos_nao_conhece_kafka_nem_rabbitmq"
```

### Tentar violar a regra (demonstração)

Se você adicionar em qualquer módulo:
```csharp
using Confluent.Kafka; // ou using RabbitMQ.Client;
```
O teste `Dominio_dos_modulos_nao_conhece_kafka_nem_rabbitmq` falha imediatamente — e o quality gate reprova o build.

---

## Instruções operacionais — como rodar esta sessão

```bash
# 1. Confirmar que os dois brokers estão de pé
docker compose ps

# 2. Rodar os testes de arquitetura
dotnet test tests/Messaging.ArchitectureTests -c Release --no-build --nologo -v normal

# 3. Ler o ADR da decisão
cat docs/adr/0003-broker-behind-abstraction.md

# 4. Ler o teste que enforça a regra
# tests/Messaging.ArchitectureTests/ModuleBoundaryTests.cs — linha 59

# 5. Arquivos-chave para walkthrough ao vivo:
#    docs/adr/0003-broker-behind-abstraction.md        ← decisão e trade-offs
#    tests/Messaging.ArchitectureTests/ModuleBoundaryTests.cs ← 6 regras executáveis
#    docker-compose.yml                                ← os dois brokers configurados
```

---

## O que você deve ser capaz de explicar ao seu chefe

**"Por que vocês têm dois brokers no projeto?"**
— Para comparar Kafka e RabbitMQ sob exatamente o mesmo fluxo e os mesmos testes. A POC termina com uma recomendação baseada em medição real — não em opinião. A troca é feita por variável de ambiente, sem alterar código.

**"Se trocar de Kafka para RabbitMQ, o que precisa ser alterado?"**
— Só a variável de ambiente `Messaging__Broker`. O código de domínio (Ingestion, Processing, Operations) não sabe qual broker está rodando — eles falam com `IMessageBus`, que é uma abstração. O adapter correto é injetado pelo host na inicialização.

**"Como vocês garantem que ninguém coloca código do Kafka dentro do domínio?"**
— Com um teste executável: `ModuleBoundaryTests.Dominio_dos_modulos_nao_conhece_kafka_nem_rabbitmq`. Se alguém adicionar `using Confluent.Kafka` em qualquer módulo de domínio, o quality gate quebra imediatamente. A fronteira é mantida por CI, não por disciplina.

**"Qual é a diferença entre Kafka e RabbitMQ?"**
— Kafka é um log imutável: a mensagem fica gravada mesmo depois de consumida, permite replay e múltiplos grupos de consumidores independentes. RabbitMQ é uma fila tradicional: a mensagem some após o ack, mas tem roteamento mais flexível (exchanges). Para alta vazão e necessidade de replay, Kafka. Para roteamento complexo e simplicidade operacional, RabbitMQ.

---

## Próxima sessão

**Sessão 5 — Worker: transação única e idempotência garantida**

Vamos ver o que acontece quando a mensagem sai do broker e chega ao worker — e como a transação única (estado + registro interno + outbox de saída) garante que processar duas vezes produz o mesmo resultado que processar uma vez.
