using worker_consumidor.Infraestrutura.Mensageria;

namespace worker_consumidor
{
    public class ConsumidorSolicitacaoCreditoWorker : BackgroundService
    {
        private readonly ILogger<ConsumidorSolicitacaoCreditoWorker> _logger;
        private readonly IConsumidorSolicitacaoCredito _consumidor;

        public ConsumidorSolicitacaoCreditoWorker(ILogger<ConsumidorSolicitacaoCreditoWorker> logger,
            IConsumidorSolicitacaoCredito consumidor)
        {
            _logger = logger;
            _consumidor = consumidor;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ConsumidorSolicitacaoCreditoWorker iniciado.");
            await _consumidor.IniciarAsync(stoppingToken);
            _logger.LogInformation("ConsumidorSolicitacaoCreditoWorker finalizado.");
        }
    }
}
