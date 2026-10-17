# 05 — Issue Analysis

**Tema:** analisar e comentar issues automaticamente: story points, subtarefas, arquivos afetados.

## Onde está
- `.github/workflows/claude-issue-analysis.yml`: em toda issue nova, o agente `issue-analyzer` comenta com resumo, **story points (Fibonacci)**, **arquivos prováveis**, **subtarefas** em checklist (um ciclo TDD cada), riscos e perguntas; aplica o rótulo `sp-<n>` (ou `needs-info`).
- Templates `.github/ISSUE_TEMPLATE/feature.yml` e `bug.yml` (mais contexto de entrada = estimativa melhor e menos custo).

## Segurança
O texto da issue é tratado como **dado**; o prompt manda ignorar instruções dentro dela, e as ferramentas permitidas são só leitura + `gh issue comment/edit`.

## Configuração necessária
Mesmos itens do módulo 04 (App, secret). Criar rótulos `sp-1, sp-2, sp-3, sp-5, sp-8, sp-13` e `needs-info`.

## Ressalva
Não executado num repositório real.
