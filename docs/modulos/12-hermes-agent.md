# 12 — Hermes Agent (automações operacionais)

**Tema na página:** backup automático, análise de logs, notificações de status no Telegram e automações gerais.

## Onde está
`scripts/ops/` (`backup-postgres.sh`, `analyze-logs.sh`, `notify-telegram.sh`, `status-report.sh`) e `ops/hermes/README.md` (jobs, agendas e prompts), `.env.example`.

## O que NÃO está
**A configuração do próprio Hermes Agent.** Não conheço o formato dos jobs dele e não inventei. Os scripts são independentes de agente (funcionam com cron ou Agendador de Tarefas); o README traz os prompts prontos para colar quando você configurar o Hermes pela documentação oficial.

## Segurança
Jobs só leem e fazem backup; nada apaga dado ou reprocessa evento. O token do Telegram vai por stdin do curl (não aparece em `ps`) e só por variável de ambiente.
