---
description: Audita o diff (ou um caminho) contra o OWASP Top 10, com evidência e correção
argument-hint: [caminho opcional; padrão = diff atual]
---

Audite `$ARGUMENTS` (se vazio, `git diff` e arquivos alterados na sessão) usando a skill `owasp-top10` e `.claude/rules/security.md`.

Para cada categoria A01–A10 relevante ao código auditado, responda **Aplica / Não aplica** e, se aplica, **Achado** com:
`arquivo:linha`, evidência (trecho curto), impacto e correção. Severidade: Crítico, Alto, Médio, Baixo.

Rode também `dotnet list package --vulnerable --include-transitive` (se houver solution) e inclua o resultado.
Termine com a lista de testes que faltam para provar cada defesa. Não edite arquivos.
