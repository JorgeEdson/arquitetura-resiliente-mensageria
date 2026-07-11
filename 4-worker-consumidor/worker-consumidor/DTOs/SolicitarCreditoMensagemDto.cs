namespace worker_consumidor.DTOs
{
    /// <summary>
    /// Espelha o evento SolicitacaoCreditoCriadaEvento publicado pela API/publicador.
    /// Os nomes das propriedades batem com o payload serializado (System.Text.Json).
    /// </summary>
    public class SolicitarCreditoMensagemDto
    {
        public Guid SolicitacaoCreditoId { get; set; }
        public string IdempotencyKey { get; set; } = default!;
        public long IdCliente { get; set; }
        public decimal ValorSolicitado { get; set; }
        public int PrazoMeses { get; set; }
        public TipoProduto TipoProduto { get; set; }
        public DateTime DataSolicitacao { get; set; }
    }

    public enum TipoProduto
    {
        CreditoPessoal = 1,
        EmprestimoConsignado = 2,
        FinanciamentoVeicular = 3,
        FinanciamentoImobiliario = 4,
        CartaoCredito = 5,
        AntecipacaoFGTS = 6,
        CreditoEmpresarial = 7
    }
}
