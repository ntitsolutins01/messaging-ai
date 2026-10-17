# Os 15 módulos do DAPIA aplicados ao projeto

Fonte dos módulos: [pythonando.com.br/dapia](https://pythonando.com.br/dapia/) (lista de títulos e resumo de cada módulo). Os artefatos abaixo são **implementação própria** a partir desses temas, não o material do curso.

**Legenda:** ✅ pronto · 🟡 pronto com ressalva (leia o doc do módulo) · ⏳ depende de configuração externa

| # | Módulo | Status | Onde está |
|---|---|---|---|
| 01 | [Claude Code e Cursor](01-claude-code-e-cursor.md) | ✅ | `CLAUDE.md`, `.claude/`, `.cursor/rules/`, `.mcp.json` |
| 02 | [Spec Driven Development](02-spec-driven-development.md) | ✅ | `specs/`, `/spec`, `/implement-spec` |
| 03 | [TDD](03-tdd.md) | 🟡 | `rules/tdd.md`, `/tdd`, `tests/` |
| 04 | [Pull Request Review](04-pull-request-review.md) | ⏳ | `.github/workflows/claude-pr-review.yml`, `pr-reviewer` |
| 05 | [Issue Analysis](05-issue-analysis.md) | ⏳ | `.github/workflows/claude-issue-analysis.yml`, `issue-analyzer` |
| 06 | [Prompt Engineering](06-prompt-engineering.md) | ✅ | skill `prompt-engineering-dev`, `docs/prompts/`, `/prompt` |
| 07 | [Context Engineering](07-context-engineering.md) | ✅ | `rules/context-engineering.md`, `docs/context/`, `docs/adr/`, `/handoff` |
| 08 | [OWASP Top 10](08-owasp-top-10.md) | ✅ | skill `owasp-top10`, `rules/security.md`, `/owasp-check` |
| 09 | [Quality Gate](09-quality-gate.md) | 🟡 | `scripts/quality-gate.sh`, `tests/load/`, `ArchitectureTests`, `CA1502` |
| 10 | [Análises de segurança](10-analises-de-seguranca.md) | ✅ | `pentest-agent`, `scripts/pentest-local.sh`, `/pentest` |
| 11 | [Loop Engineering](11-loop-engineering.md) | 🟡 | `scripts/loop.sh`, `/loop-fix`, `rules/loop-engineering.md` |
| 12 | [Hermes Agent](12-hermes-agent.md) | ⏳ | `scripts/ops/`, `ops/hermes/` |
| 13 | [Observabilidade](13-observabilidade.md) | 🟡 | `docker-compose.observability.yml`, `Messaging.Observability`, `watch-errors.sh` |
| 14 | [LLM as a judge](14-llm-as-a-judge.md) | ✅ | `evals/`, `llm-judge`, `/eval` |
| 15 | [Monólito modular](15-monolito-modular.md) | ✅ | `src/Modules/`, `ModuleBoundaryTests`, `docs/adr/0001` |

## Estado de verificação (honesto)
- O código C# foi escrito **sem acesso a um SDK .NET**, então **não foi compilado nem executado**. O primeiro passo é `bash scripts/bootstrap.sh && bash scripts/quality-gate.sh`.
- Validados aqui: sintaxe de todos os scripts bash, YAML dos workflows e compose, JSON, frontmatter de skills/agentes/comandos, e o `--dry-run` do juiz.
- **Não** executados: containers, workflows no GitHub, k6, Telegram, Elasticsearch, Hermes.

## Kit entregue (contagem)
Regras 13 · Comandos 16 · Skills 9 · Agentes 9 · Scripts 9 · Workflows 5.
