namespace api_solicitacao_credito.DTOs
{   
    public record SolicitacaoCreditoCriadaEvento(
        Guid SolicitacaoCreditoId,
        string IdempotencyKey,
        long IdCliente,
        decimal ValorSolicitado,
        int PrazoMeses,
        TipoProduto TipoProduto,
        DateTime DataSolicitacao);
}
