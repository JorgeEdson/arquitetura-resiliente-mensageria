using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using worker_expurgo_outbox.Dominio;
using worker_expurgo_outbox.Infraestrutura.Persistencia;

namespace worker_expurgo_outbox
{
    public class OutboxCleanupWorker : BackgroundService
    {
        private readonly ILogger<OutboxCleanupWorker> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly OutboxCleanupSettings _settings;

        public OutboxCleanupWorker(
            ILogger<OutboxCleanupWorker> logger,
            IServiceScopeFactory scopeFactory,
            IOptions<OutboxCleanupSettings> settings)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _settings = settings.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "OutboxCleanupWorker iniciado. DaysToKeep={DaysToKeep}, execução diária às {HoraExecucaoUtc}h (UTC).",
                _settings.DaysToKeep,
                _settings.HoraExecucaoUtc);

            while (!stoppingToken.IsCancellationRequested)
            {
                var agoraUtc = DateTime.UtcNow;
                var proximaExecucaoUtc = CalcularProximaExecucaoUtc(agoraUtc, _settings.HoraExecucaoUtc);
                var delay = proximaExecucaoUtc - agoraUtc;

                _logger.LogInformation(
                    "Próxima execução do expurgo agendada para {ProximaExecucaoUtc:u} (em {DelayHoras:F1} horas).",
                    proximaExecucaoUtc,
                    delay.TotalHours);

                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("OutboxCleanupWorker cancelado durante o agendamento.");
                    break;
                }

                try
                {
                    await ExecutarExpurgoAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao executar expurgo da Outbox.");
                }
            }
        }

        private static DateTime CalcularProximaExecucaoUtc(DateTime agoraUtc, int horaExecucaoUtc)
        {
            // Alvo: hoje às HoraExecucaoUtc:00:00 em UTC.
            var alvoUtc = new DateTime(
                agoraUtc.Year, agoraUtc.Month, agoraUtc.Day,
                horaExecucaoUtc, 0, 0, DateTimeKind.Utc);

            // Se já passou do horário de hoje, agenda para amanhã.
            if (agoraUtc >= alvoUtc)
                alvoUtc = alvoUtc.AddDays(1);

            return alvoUtc;
        }

        private async Task ExecutarExpurgoAsync(CancellationToken cancellationToken)
        {
            
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<CreditoDbContext>();

            
            var limiteUtc = DateTime.UtcNow.AddDays(-_settings.DaysToKeep);

            _logger.LogInformation(
                "Iniciando expurgo da Outbox. Serão removidas mensagens com Status=Publicada e DataAtualizacao < {LimiteUtc:u}.",
                limiteUtc);

            var removidos = await dbContext.OutboxMessages
                .Where(o =>
                    o.Status == OutboxStatus.Publicada &&
                    o.DataAtualizacao.HasValue &&
                    o.DataAtualizacao < limiteUtc)
                .ExecuteDeleteAsync(cancellationToken);

            _logger.LogInformation(
                "Expurgo da Outbox concluído. Registros removidos={Quantidade}.",
                removidos);
        }
    }
}
