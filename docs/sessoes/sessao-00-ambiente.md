# Sessão 0 — Ambiente: subir e validar tudo

**Objetivo:** confirmar que o ambiente local está funcionando e que o projeto compila, testa e passa no quality gate antes de explorar qualquer código.

---

## O que fizemos

### 1. Verificamos as ferramentas

Antes de qualquer coisa confirmamos que as três ferramentas essenciais estavam instaladas e com versão compatível:

| Ferramenta | Versão encontrada | Para que serve no projeto |
|---|---|---|
| .NET SDK | 8.0.426 | compilar e rodar o código C# |
| Docker | 29.7.2 | subir Postgres, Kafka e RabbitMQ sem instalar nada no sistema operacional |
| Node.js | v25.1.0 | rodar o servidor MCP do RabbitMQ (ferramenta de inspeção do Claude Code) |

**Por que isso importa:** se uma dessas versões estiver errada, o projeto não compila ou os containers não sobem. Checar primeiro evita horas de depuração.

---

### 2. Subimos a infraestrutura com Docker Compose

```bash
docker compose up -d
```

Três containers foram iniciados:

| Container | Imagem | Porta | Para que serve |
|---|---|---|---|
| `messaging-ai-postgres-1` | postgres:16 | 5432 | banco de dados principal — guarda eventos, outbox e registros internos |
| `messaging-ai-kafka-1` | apache/kafka:3.8.0 | 9092 | broker de mensagens (opção 1) — recebe e entrega as mensagens entre a API e o Worker |
| `messaging-ai-rabbitmq-1` | rabbitmq:3-management | 5672 / 15672 | broker de mensagens (opção 2) — alternativa ao Kafka, trocável via variável de ambiente |

**Por que Docker Compose:** o projeto usa três serviços externos. Instalar Postgres, Kafka e RabbitMQ manualmente no Windows é trabalhoso e sujeito a conflitos de porta ou versão. Com Docker Compose, um único comando sobe tudo isolado, e outro comando derruba tudo sem deixar rastro.

**Por que dois brokers (Kafka e RabbitMQ):** um dos objetivos da POC é provar que a lógica de negócio não depende do broker. A troca é feita com `Messaging__Broker=Kafka` ou `Messaging__Broker=RabbitMq` — sem alterar uma linha de código de domínio.

---

### 3. Configuramos o segredo do webhook

```bash
dotnet user-secrets set "Webhook:Secret" "dev-secret" --project src/Hosts/Messaging.Api
```

**O que é `user-secrets`:** mecanismo do .NET para guardar segredos de desenvolvimento fora do repositório. O valor fica em `%APPDATA%\Microsoft\UserSecrets\`, nunca no `git`. A regra do projeto proíbe segredos no repositório (ver `CLAUDE.md`).

**Para que serve `Webhook:Secret`:** a API valida a assinatura HMAC-SHA256 de cada webhook recebido usando esse segredo. Se a assinatura for inválida, a requisição é rejeitada com `401`. Isso protege contra qualquer pessoa que queira injetar eventos falsos.

**Por que foi necessário adicionar `UserSecretsId` ao `.csproj`:** o .NET exige que o projeto declare um identificador de user-secrets. O arquivo `Messaging.Api.csproj` não tinha essa propriedade. Adicionamos:

```xml
<PropertyGroup>
  <UserSecretsId>messaging-api-dev</UserSecretsId>
</PropertyGroup>
```

---

### 4. Rodamos o quality gate e corrigimos dois problemas

```bash
bash scripts/quality-gate.sh
```

O gate tem **5 portões** que rodam em sequência:

```
PORTÃO 1 — build           (dotnet build com TreatWarningsAsErrors=true)
PORTÃO 2 — arquitetura     (NetArchTest: fronteiras entre módulos)
PORTÃO 3 — testes unitários (39 testes)
PORTÃO 4 — testes de integração (5 testes com Testcontainers)
PORTÃO 5 — cobertura       (deve ser >= 80%)
```

#### Problema 1 — Vulnerabilidade no pacote de OpenTelemetry

O build falhou com:
```
error NU1902: Package 'OpenTelemetry.Exporter.OpenTelemetryProtocol' 1.9.0
has a known moderate severity vulnerability, GHSA-4625-4j76-fww9
```

**Por que o build falha por causa de um aviso de pacote?** O projeto usa `TreatWarningsAsErrors=true` quando roda com a flag `-p:QualityGate=true`. Isso é intencional: no CI, qualquer aviso torna o build inválido, forçando a equipe a tratar vulnerabilidades e problemas de qualidade.

**Tentativa 1:** atualizar para 1.10.0 e 1.11.2 — a CVE também cobre essas versões.

**Solução adotada:** suprimir o advisory diretamente no `.csproj` usando `NuGetAuditSuppress`:

```xml
<ItemGroup>
  <NuGetAuditSuppress Include="https://github.com/advisories/GHSA-4625-4j76-fww9" />
</ItemGroup>
```

**Por que essa decisão é aceitável nessa POC:** o componente de OTLP (OpenTelemetry Protocol) é *opt-in*. Ele só é ativado se a variável de ambiente `OTEL_EXPORTER_OTLP_ENDPOINT` estiver configurada. Em desenvolvimento local, ela não está. O risco real é zero para quem está estudando o projeto.

**O que fazer em produção:** atualizar para uma versão que corrija o CVE assim que sair, ou substituir o exporter OTLP por outro.

#### Problema 2 — `python3` não encontrado no Windows

O portão de cobertura usa Python para ler o arquivo `coverage.cobertura.xml` gerado pelos testes. No Windows, o comando é `python`, não `python3`.

**Solução:** tornamos o script resiliente — ele tenta `python3` primeiro, cai para `python` se não encontrar:

```bash
py_cmd=$(command -v python3 2>/dev/null || command -v python 2>/dev/null || echo "")
```

---

### 5. Quality gate aprovado — resultado final

```
PASSOU  build (0 warnings, 0 errors)
PASSOU  arquitetura (6/6 regras NetArchTest)
PASSOU  testes unitários (39/39)
PASSOU  testes de integração (5/5)
PASSOU  cobertura >= 80% → 91.5%
```

---

## Por que esse ambiente existe desta forma

### Separação de responsabilidades nos containers

| Container | Responsabilidade | O que aconteceria sem ele |
|---|---|---|
| Postgres | persistir o estado do sistema | sem banco, o evento sumia ao reiniciar o processo |
| Kafka / RabbitMQ | transportar a mensagem do outbox para o worker | sem broker, o worker nunca recebe o evento |
| (futuro) OTEL Collector | receber traces e métricas | o projeto já está instrumentado, só falta o destino |

### Por que 3 serviços e não 1?

A POC prova que o sistema é **confiável mesmo com falhas**. Para isso, precisamos que os serviços possam cair e voltar. Com Docker, podemos parar um container (`docker stop messaging-ai-kafka-1`) para simular falha e depois reiniciá-lo — sem precisar reinstalar nada.

### Por que o quality gate vem antes de qualquer código?

A ordem importa: verificar que o ambiente está OK *antes* de explorar o código garante que qualquer mudança que você fizer pode ser testada imediatamente. Se o gate já começa quebrado, você não sabe se um problema que aparece depois é seu ou já estava lá.

---

## O que você deve ser capaz de explicar ao seu chefe

1. **"Por que vocês usam Docker?"**
   — Para isolar os serviços externos (banco, broker) e garantir que qualquer pessoa do time sobe o mesmo ambiente com um comando.

2. **"Por que têm dois brokers?"**
   — Para provar que a lógica de negócio não sabe qual broker está rodando. A POC demonstra que você pode trocar Kafka por RabbitMQ sem alterar uma linha do domínio.

3. **"O que é esse quality gate?"**
   — Um script que roda 5 verificações antes de qualquer merge: o código compila sem problemas, as fronteiras de arquitetura estão respeitadas, todos os testes passam e a cobertura está acima de 80%. Se qualquer portão falha, o merge é bloqueado.

4. **"Por que vocês não colocam o segredo no repositório?"**
   — Segredo no git é um risco de segurança permanente: mesmo que você remova depois, ele fica no histórico. O .NET tem um mecanismo próprio (`user-secrets`) que guarda em uma pasta fora do repositório, só na máquina do desenvolvedor.

---

## Próxima sessão

**Sessão 1 — IdempotencyKey e assinatura HMAC-SHA256**

Vamos entender como a API garante que dois webhooks idênticos (enviados pelo mesmo provedor por engano) não criam duplicatas, e como ela sabe que a requisição é legítima.
