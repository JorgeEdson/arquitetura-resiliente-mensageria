using api_solicitacao_credito.DTOs;

namespace api_solicitacao_credito.Dominio
{  
    public class SolicitacaoCredito
    {
        public Guid Id { get; set; } = Guid.NewGuid();
       
        public string IdempotencyKey { get; set; } = default!;

        public long IdCliente { get; set; }

        public decimal ValorSolicitado { get; set; }

        public int PrazoMeses { get; set; }

        public TipoProduto TipoProduto { get; set; }

        public DateTime DataSolicitacao { get; set; }

        public StatusSolicitacao Status { get; set; } = StatusSolicitacao.Recebida;

        public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
    }

    public enum StatusSolicitacao
    {
        Recebida = 0,
        EmProcessamento = 1,
        Concluida = 2
    }
}
