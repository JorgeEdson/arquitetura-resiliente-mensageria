namespace worker_publicador.Dominio
{
    public class OutboxMessage
    {
        public Guid Id { get; set; }

        public Guid SolicitacaoCreditoId { get; set; }

        public string IdempotencyKey { get; set; } = default!;

        public string TipoMensagem { get; set; } = default!;

        public string Payload { get; set; } = default!;

        public OutboxStatus Status { get; set; } = OutboxStatus.Pendente;

        public DateTime DataCriacao { get; set; } = DateTime.UtcNow;

        public DateTime? DataAtualizacao { get; set; }

        public int Tentativas { get; set; }
    }

    public enum OutboxStatus
    {
        Pendente = 0,
        Publicada = 1
    }
}
