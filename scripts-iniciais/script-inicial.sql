IF DB_ID('SolicitacaoCreditoDb') IS NULL
BEGIN
    CREATE DATABASE SolicitacaoCreditoDb;
END
GO

USE SolicitacaoCreditoDb;
GO

-- =====================================================================
-- Tabela de negócio: SolicitacoesCredito
-- É gravada na MESMA transação que a OutboxMessages (Transactional
-- Outbox Pattern). O índice UNIQUE em IdempotencyKey é a barreira de
-- idempotência: uma solicitação duplicada viola o índice e é tratada
-- pela aplicação como "já recebida" (sem SELECT prévio).
-- =====================================================================
IF OBJECT_ID('dbo.SolicitacoesCredito', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SolicitacoesCredito
    (
        Id              UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT PK_SolicitacoesCredito PRIMARY KEY,
        IdempotencyKey  NVARCHAR(200)    NOT NULL,
        IdCliente       BIGINT           NOT NULL,
        ValorSolicitado DECIMAL(18,2)    NOT NULL,
        PrazoMeses      INT              NOT NULL,
        TipoProduto     INT              NOT NULL,
        DataSolicitacao DATETIME2        NOT NULL,
        Status          INT              NOT NULL,
        DataCriacao     DATETIME2        NOT NULL
    );

    CREATE UNIQUE INDEX UX_SolicitacoesCredito_IdempotencyKey
        ON dbo.SolicitacoesCredito(IdempotencyKey);
END
GO

-- =====================================================================
-- Tabela de Outbox: OutboxMessages
-- Id como UNIQUEIDENTIFIER (GUID gerado pela aplicação).
-- FK para SolicitacoesCredito garante 1 evento por solicitação.
-- =====================================================================
IF OBJECT_ID('dbo.OutboxMessages', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.OutboxMessages
    (
        Id                   UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT PK_OutboxMessages PRIMARY KEY,
        SolicitacaoCreditoId UNIQUEIDENTIFIER NOT NULL,
        IdempotencyKey       NVARCHAR(200)    NOT NULL,
        TipoMensagem         NVARCHAR(100)    NOT NULL,
        Payload              NVARCHAR(MAX)    NOT NULL,
        Status               INT              NOT NULL,
        DataCriacao          DATETIME2        NOT NULL,
        DataAtualizacao      DATETIME2        NULL,
        Tentativas           INT              NOT NULL CONSTRAINT DF_OutboxMessages_Tentativas DEFAULT (0),

        CONSTRAINT FK_OutboxMessages_SolicitacoesCredito
            FOREIGN KEY (SolicitacaoCreditoId)
            REFERENCES dbo.SolicitacoesCredito(Id)
            ON DELETE CASCADE
    );

    -- Índice usado pelo worker-publicador para varrer pendentes.
    CREATE INDEX IX_Outbox_Status
        ON dbo.OutboxMessages(Status);

    CREATE INDEX IX_Outbox_IdempotencyKey
        ON dbo.OutboxMessages(IdempotencyKey);
END
GO

-- =====================================================================
-- Tabela de Propostas (worker-consumidor): resultado de solicitações
-- APROVADAS. Índice UNIQUE em IdempotencyKey torna o consumo idempotente
-- (não cria proposta duplicada em reentrega da mensagem).
-- =====================================================================
IF OBJECT_ID('dbo.Propostas', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Propostas
    (
        Id                   UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT PK_Propostas PRIMARY KEY,
        SolicitacaoCreditoId UNIQUEIDENTIFIER NOT NULL,
        IdempotencyKey       NVARCHAR(200)    NOT NULL,
        IdCliente            BIGINT           NOT NULL,
        ValorSolicitado      DECIMAL(18,2)    NOT NULL,
        PrazoMeses           INT              NOT NULL,
        TipoProduto          INT              NOT NULL,
        DataSolicitacao      DATETIME2        NOT NULL,
        ValorAprovado        DECIMAL(18,2)    NOT NULL,
        TaxaJurosAnual       DECIMAL(5,2)     NOT NULL,
        ValorParcela         DECIMAL(18,2)    NOT NULL,
        DataPrimeiraParcela  DATETIME2        NOT NULL,
        DataCriacaoProposta  DATETIME2        NOT NULL,
        StatusProposta       NVARCHAR(20)     NOT NULL
    );

    CREATE UNIQUE INDEX UX_Propostas_IdempotencyKey
        ON dbo.Propostas(IdempotencyKey);
END
GO

-- =====================================================================
-- Tabela de SolicitacoesRejeitadas (worker-consumidor): resultado de
-- solicitações REJEITADAS. Índice UNIQUE em IdempotencyKey idem acima.
-- =====================================================================
IF OBJECT_ID('dbo.SolicitacoesRejeitadas', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SolicitacoesRejeitadas
    (
        Id                   UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT PK_SolicitacoesRejeitadas PRIMARY KEY,
        SolicitacaoCreditoId UNIQUEIDENTIFIER NOT NULL,
        IdempotencyKey       NVARCHAR(200)    NOT NULL,
        IdCliente            BIGINT           NOT NULL,
        ValorSolicitado      DECIMAL(18,2)    NOT NULL,
        PrazoMeses           INT              NOT NULL,
        TipoProduto          INT              NOT NULL,
        DataSolicitacao      DATETIME2        NOT NULL,
        DataRejeicao         DATETIME2        NOT NULL,
        MensagemRejeicao     NVARCHAR(500)    NOT NULL
    );

    CREATE UNIQUE INDEX UX_SolicitacoesRejeitadas_IdempotencyKey
        ON dbo.SolicitacoesRejeitadas(IdempotencyKey);
END
GO
