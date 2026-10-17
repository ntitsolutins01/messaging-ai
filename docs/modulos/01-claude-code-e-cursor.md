# 01 — Claude Code e Cursor

**Tema do módulo:** instalação e configuração; context engineering, skills, rules, MCP servers e subagents.

## Onde está
| Peça | Arquivo |
|---|---|
| Contexto | `CLAUDE.md` (curto), `CLAUDE.local.md` (pessoal, fora do git) |
| Regras | `.claude/rules/*.md` |
| Comandos | `.claude/commands/*.md` |
| Skills | `.claude/skills/*/SKILL.md` |
| Subagents | `.claude/agents/*.md` |
| MCP | `.mcp.json` (PostgreSQL somente leitura sugerido) |
| Hooks | `.claude/settings.json` + `.claude/hooks/` |
| Cursor | `.cursor/rules/*.mdc` — apontam para as mesmas regras, sem duplicar |

## Como usar
Abra a pasta no Claude Code (`claude` na raiz) ou no Cursor. As rules do Cursor referenciam `.claude/rules/` para manter **uma fonte de verdade**.

## Ressalvas
- Confira o pacote do MCP Postgres em `.mcp.json` antes de usar.
- Os hooks são scripts `bash` e precisam de `bash` e `python3` no PATH (Git Bash/WSL no Windows).
