# 04 — Pull Request Review

**Tema:** revisão automática de PR no GitHub, bloqueio de merge em falha crítica, testes automatizados.

## Onde está
- `.github/workflows/claude-pr-review.yml`: roda em todo PR de branch do mesmo repositório; o agente `pr-reviewer` comenta inline com `[CRÍTICO]/[ATENÇÃO]/[SUGESTÃO]`.
- **Bloqueio:** se há crítico, o Claude aplica o rótulo `claude-blocking`; o job `Claude review gate` falha enquanto o rótulo existir (ou se a revisão não concluir).
- **Testes automatizados:** o revisor comenta o esqueleto do teste que falta; para escrevê-los de fato, comente `@claude escreva os testes que faltam` (`.github/workflows/claude.yml`).
- `.github/workflows/ci.yml` roda o quality gate; `.github/pull_request_template.md`.

## Configuração necessária (uma vez)
1. Instalar o [Claude GitHub App](https://github.com/apps/claude) no repositório (ou `/install-github-app` no Claude Code).
2. Secret `ANTHROPIC_API_KEY` (ou trocar para `claude_code_oauth_token`).
3. **Branch protection** em `main`: exigir os checks `Claude review gate` e `quality-gate`. Sem isso o bloqueio é só visual.
4. Criar o rótulo `claude-blocking` (o `gh pr edit --add-label` falha se ele não existir).

## Ressalvas
Sintaxe do action conferida na documentação oficial (`anthropics/claude-code-action@v1`), mas os workflows **não rodaram** num repositório real. Revise `--allowedTools` e custos (`--max-turns`) para o seu uso.
