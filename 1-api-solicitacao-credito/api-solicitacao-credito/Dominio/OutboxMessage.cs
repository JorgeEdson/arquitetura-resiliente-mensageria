namespace api_solicitacao_credito.Dominio
{ 
    public class OutboxMessage
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        
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
