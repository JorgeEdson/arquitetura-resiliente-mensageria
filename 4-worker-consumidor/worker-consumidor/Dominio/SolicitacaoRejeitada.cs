using worker_consumidor.DTOs;

namespace worker_consumidor.Dominio
{
    public class SolicitacaoRejeitada
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>Solicitação de crédito de origem (rastreabilidade).</summary>
        public Guid SolicitacaoCreditoId { get; set; }

        /// <summary>Chave de idempotência do consumo (índice UNIQUE): evita rejeição duplicada em reprocessamento.</summary>
        public string IdempotencyKey { get; set; } = default!;

        public long IdCliente { get; set; }
        public decimal ValorSolicitado { get; set; }
        public int PrazoMeses { get; set; }
        public TipoProduto TipoProduto { get; set; }
        public DateTime DataSolicitacao { get; set; }

        public DateTime DataRejeicao { get; set; }

        public string MensagemRejeicao { get; set; } = default!;
    }
}
