namespace api_solicitacao_credito.DTOs
{
    public class SalvarSolicitacaoCreditoRequest
    {
        public long IdCliente { get; set; } = default!;
        public decimal ValorSolicitado { get; set; }
        public int PrazoMeses { get; set; }
        public  TipoProduto TipoProduto { get; set; }
        public DateTime DataSolicitacao { get; set; } = DateTime.UtcNow;
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
