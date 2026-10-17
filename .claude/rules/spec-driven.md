# Regra: Spec Driven Development

- Funcionalidade sem spec **aprovada** em `specs/` não começa. Se não existe, rode `/spec` e pare para revisão.
- A spec manda: se o código precisa divergir, atualize a spec no mesmo PR e explique.
- Todo critério de aceite (CA) tem ao menos um teste com o ID no nome ou em comentário (`// CA3`).
- Pergunta em aberto bloqueia a tarefa que depende dela. Não preencha com suposição silenciosa: registre a assunção na spec (como em `WebhookSignatureValidator`).
- Tarefas pequenas: uma tarefa = um ciclo TDD = um commit.
- Ao concluir uma tarefa, marque `[x]` na spec e atualize o `Status` quando todas fecharem.
