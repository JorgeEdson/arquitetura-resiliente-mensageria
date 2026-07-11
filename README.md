# Arquitetura Resiliente com Mensageria — Solicitação de Crédito

> ⚠️ **Projeto didático.** Este repositório é um **exemplo básico**, criado para **ensinar os conceitos de uma arquitetura resiliente com mensageria** (Transactional Outbox, idempotência, retry/circuit breaker, dead-letter queue e expurgo). Ele **não** é uma referência pronta para produção — várias simplificações foram feitas de propósito para manter o foco no aprendizado (ver [Limitações](#limitações-por-ser-didático)).

Demonstra, ponta a ponta, como uma solicitação de crédito entra por uma API e é processada de forma **assíncrona e confiável** através de filas, com os principais mecanismos de resiliência que se espera de um sistema de mensageria.

---

## Visão geral do fluxo

```
Agente/Canal → [API] --(grava atômico)--> [SQL Server: Outbox]
                                              │
                              [Worker Publicador] --(publica)--> [RabbitMQ: fila]
                                                                      │
                                              [Worker Consumidor] <--(consome)
                                                    │
                                    aprovada → Proposta | rejeitada → SolicitacaoRejeitada  (SQL Server)
                                                    │
                                    falha 3x → [RabbitMQ: DLQ]

[Worker Expurgo] --(limpa Outbox publicada > 48h)--> [SQL Server]
```

1. **API** recebe a requisição e, garantindo atomicidade (ACID), grava a **entidade de negócio + a mensagem de Outbox na mesma transação**.
2. **Worker Publicador** lê a tabela de Outbox e publica a mensagem em uma fila do RabbitMQ.
3. **Worker Consumidor** consome a fila e simula a criação da proposta de crédito (aprovação/rejeição).
4. **Worker Expurgo** limpa da Outbox as mensagens já publicadas após 48 horas.

Os diagramas C4 (C1 contexto, C2 containers, C3 componentes) estão em [`/diagramas`](./diagramas) em PlantUML.

---

## Padrões e mecanismos de resiliência

| Padrão / mecanismo | Onde | O que resolve |
|---|---|---|
| **Transactional Outbox** | API + SQL Server | Atomicidade entre gravar o dado de negócio e o evento a publicar (nada de "gravou no banco mas não publicou"). |
| **Idempotência (produtor)** | API — índice `UNIQUE` em `IdempotencyKey` | Requisições duplicadas não geram solicitações duplicadas (INSERT + tratamento de violação, sem `SELECT` prévio). |
| **Idempotência (consumidor)** | Consumidor — índice `UNIQUE` em `IdempotencyKey` | Reentrega (at-least-once) não gera Proposta/Rejeição duplicada. |
| **Retry + Circuit Breaker** | Publicador — Polly | Tolera falhas transitórias do broker ao publicar. |
| **Publisher Confirms** | Publicador — RabbitMQ | Só marca como publicada após o ACK do broker. |
| **Filas duráveis (quorum) + mensagens persistentes** | RabbitMQ | Sobrevivem a restart do broker. |
| **Dead-Letter Queue (DLQ) com 3 tentativas** | RabbitMQ (quorum `x-delivery-limit=3` + DLX) | Mensagens "envenenadas" saem da fila principal após 3 tentativas e vão para a DLQ, sem travar o consumo. |
| **Expurgo / retenção** | Worker Expurgo | Mantém a Outbox enxuta (remove publicadas com +48h). |

---

## Projetos / containers

| # | Projeto | Tipo | Responsabilidade |
|---|---|---|---|
| 1 | `1-api-solicitacao-credito` | ASP.NET Core 8 (REST) | Recebe a solicitação; grava `SolicitacaoCredito` + `OutboxMessage` atomicamente; idempotência; responde `202 Accepted`. |
| 2 | `2-worker-publicador` | .NET 8 Worker | Lê a Outbox e publica no RabbitMQ (Polly + confirms). |
| 3 | `3-worker-expurgo-outbox` | .NET 8 Worker | Job diário (UTC) que expurga a Outbox publicada com +48h. |
| 4 | `4-worker-consumidor` | .NET 8 Worker | Consome a fila, aplica a regra de crédito e persiste Proposta/Rejeição (idempotente; retry → DLQ). |
| — | RabbitMQ | Infra (container) | Broker de mensageria (fila quorum + DLX/DLQ). |
| — | SQL Server | Infra (container) | Banco `SolicitacaoCreditoDb`. |

---

## Requisitos funcionais (RF)

- **RF01** — Receber solicitações de crédito via API REST (`POST /api/solicitacoes-credito`) e persisti-las de forma atômica.
- **RF02** — Garantir idempotência: solicitações duplicadas (mesmo cliente, valor, prazo e produto dentro de uma janela de 48h) não geram processamento duplicado.
- **RF03** — Publicar de forma assíncrona e confiável cada solicitação recebida (via Outbox → fila).
- **RF04** — Consumir a fila e aplicar a regra de crédito:
  - **Aprovada** (valor ≤ R$ 20.000 **e** prazo ≤ 36 meses): gera uma **Proposta** com valor aprovado, taxa de juros por produto e parcela (Tabela Price).
  - **Rejeitada** (caso contrário): gera uma **SolicitacaoRejeitada** com o motivo.
- **RF05** — Encaminhar para uma **DLQ** as mensagens que falharem no processamento após 3 tentativas.
- **RF06** — Expurgar da Outbox as mensagens já publicadas após 48 horas.
- **RF07** — Permitir inspeção das filas (principal e DLQ) pelo painel de gerenciamento do RabbitMQ.

## Como rodar

Na raiz do repositório:

```bash
docker compose up --build
```

Isso sobe: **SQL Server**, um init que cria o banco/esquema (`scripts-iniciais/script-inicial.sql`), **RabbitMQ**, a **API** e os três **workers**.

Endereços padrão:

| Serviço | URL / Porta | Credenciais |
|---|---|---|
| API (Swagger) | http://localhost:8080/swagger | — |
| RabbitMQ (painel) | http://localhost:15672 | `guest` / `guest` |
| RabbitMQ (AMQP) | `localhost:5672` | `guest` / `guest` |
| SQL Server | `localhost:1433` | `sa` / `SenhaForte!123` |

Para parar:

```bash
docker compose down
```

> Como o RabbitMQ **não** tem volume persistente neste compose, reiniciar o container **zera as filas** — útil ao trocar a topologia (evita `PRECONDITION_FAILED`).

---

## Testando com a collection do Insomnia

O arquivo está em [`request collection/insomnia-collection-credito.json`](./request%20collection/insomnia-collection-credito.json).

1. No Insomnia: **Import → From File** e selecione o arquivo.
2. Use o **Base Environment** (já traz `base_url`, `rabbit_url`, credenciais, etc.).
3. Requests disponíveis:

   **01 - Fluxo**
   - `Criar Solicitação - APROVADA` → gera uma **Proposta**.
   - `Reenvio IDÊNTICO (idempotência)` → repete o corpo anterior; deve retornar `jaExistia: true` sem duplicar.
   - `Criar Solicitação - REJEITADA por valor` → gera **SolicitacaoRejeitada**.
   - `Criar Solicitação - REJEITADA por prazo` → gera **SolicitacaoRejeitada**.
   - `Poison message - FORÇA DLQ (IdCliente 666)` → aciona uma falha simulada; a mensagem é reentregue 3x e vai para a **DLQ**.

   **02 - Verificação**
   - `RabbitMQ - Estado da fila` e `RabbitMQ - Estado da DLQ` (via API de gerenciamento).
   - `API - Swagger`.

### Demonstração da DLQ (roteiro)

1. Envie o request **Poison message (IdCliente 666)**.
2. Acompanhe os logs do container `worker_consumidor` (`Tentativa 1/3`, `2/3`, `3/3`).
3. Abra o painel do RabbitMQ (ou rode o request **Estado da DLQ**) e veja a mensagem em `solicitacoes-credito.dlq`.

---

## Verificação no banco

```sql
USE SolicitacaoCreditoDb;

SELECT * FROM SolicitacoesCredito;      -- solicitações recebidas
SELECT * FROM OutboxMessages;           -- Status: 0=Pendente, 1=Publicada
SELECT * FROM Propostas;                -- aprovadas
SELECT * FROM SolicitacoesRejeitadas;   -- rejeitadas
```

---
## Limitações (por ser didático)

Estas simplificações são **intencionais** para focar no aprendizado; em produção você trataria cada uma:

- **Segredos em texto claro** no `docker-compose.yml` / `appsettings.json` (use secrets/variáveis seguras).
- **API sem autenticação/autorização**.
- **NACK sempre com requeue:** o consumidor não distingue erro transitório de permanente — um erro permanente consome as 3 tentativas antes de ir à DLQ. O ideal é mandar erro permanente direto à DLQ (`requeue:false`).
- **Schema criado por script SQL** (sem EF Migrations).
- **Integração com Backoffice/Core de crédito é apenas o alvo** (não implementada) — o consumidor persiste as decisões no próprio banco.
- **Sem reprocessador de DLQ**, sem métricas/tracing distribuído e sem política de retry com atraso (delayed retry).
