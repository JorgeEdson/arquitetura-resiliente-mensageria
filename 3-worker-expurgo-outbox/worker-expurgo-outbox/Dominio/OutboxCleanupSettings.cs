namespace worker_expurgo_outbox.Dominio
{
    public class OutboxCleanupSettings
    {   
        public int DaysToKeep { get; set; } = 2;

        
        public int HoraExecucaoUtc { get; set; } = 23;
    }
}
