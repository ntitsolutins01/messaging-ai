# Regra: Context Engineering

Contexto é o recurso mais caro da sessão. Carregue o mínimo correto, na hora certa.

1. **Comece pelo mapa**: leia `docs/context/CONTEXT-MAP.md` e carregue só o que a tarefa pede.
2. **Spec antes de código**: o contrato da tarefa está em `specs/NNN-*.md`. Não reconstrua requisito de memória.
3. **Um assunto por sessão.** Mudou de assunto: `/handoff` e sessão nova.
4. **Não leia o que o build/teste já diz.** Rode o teste e leia a falha, em vez de ler arquivos "para entender".
5. **Busque, não despeje**: `Grep`/`Glob` primeiro; leia só o trecho necessário (offset/limit). Nunca cole arquivo grande no chat.
6. **Decisões duráveis viram ADR** (`docs/adr/`), não ficam só na conversa.
7. **CLAUDE.md fica curto** (< 100 linhas): invariantes, comandos, ponteiros. Detalhe vai para rules/skills carregadas sob demanda.
8. **Skills sob demanda**: expertise de domínio mora em `.claude/skills/` e só entra quando a descrição casa com a tarefa.
9. **Fonte de verdade única**: se duas docs divergem, corrija a mais nova e delete a duplicata. O Cursor aponta para o `.claude/rules/`, não copia.
10. **Subagents para ler muito**: varreduras amplas e revisões vão para um agente; o contexto principal recebe só a conclusão.

Sinais de contexto ruim: respostas que ignoram invariantes, releitura repetida do mesmo arquivo, sessão com mais de ~2 assuntos.
