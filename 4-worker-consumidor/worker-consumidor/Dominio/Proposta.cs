using worker_consumidor.DTOs;

namespace worker_consumidor.Dominio
{
    public class Proposta
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>Solicitação de crédito de origem (rastreabilidade).</summary>
        public Guid SolicitacaoCreditoId { get; set; }

        /// <summary>Chave de idempotência do consumo (índice UNIQUE): evita proposta duplicada em reprocessamento.</summary>
        public string IdempotencyKey { get; set; } = default!;

        public long IdCliente { get; set; }
        public decimal ValorSolicitado { get; set; }
        public int PrazoMeses { get; set; }
        public TipoProduto TipoProduto { get; set; }
        public DateTime DataSolicitacao { get; set; }
        public decimal ValorAprovado { get; set; }
        public decimal TaxaJurosAnual { get; set; }
        public decimal ValorParcela { get; set; }
        public DateTime DataPrimeiraParcela { get; set; }
        public DateTime DataCriacaoProposta { get; set; }
        public string StatusProposta { get; set; } = default!;
    }
}
