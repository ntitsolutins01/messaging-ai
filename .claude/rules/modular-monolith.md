# Regra: Criar ou alterar módulos

## Novo módulo
1. Pasta `src/Modules/Messaging.<Nome>/` com `.csproj` referenciando só `SharedKernel` e `Contracts`.
2. Tudo que outro módulo precisa vira tipo em `Messaging.Contracts/<Nome>/`. O resto fica interno.
3. Tabelas com prefixo do módulo (`ingestion_*`, `processing_*`). Nunca leia tabela de outro módulo.
4. Atualize `ModuleBoundaryTests`: o novo módulo não depende dos outros, e os outros não dependem dele.
5. Registre no host por uma extensão `Add<Nome>Module(IServiceCollection)`.
6. Atualize `CLAUDE.md` (estrutura) e escreva um ADR se a decisão for não óbvia.

## Comunicação entre módulos
- Assíncrona por evento de integração (via outbox) é o padrão.
- Síncrona só por porta em `Contracts` (ex.: `IEventReprocessor`), implementada pelo módulo dono.
- Nunca compartilhe entidade de domínio entre módulos; compartilhe DTO de contrato.

## Quando extrair um módulo para serviço
Só com motivo medido (escala, equipe, ciclo de deploy). O `Contracts` vira API/mensagem; o resto não muda.
