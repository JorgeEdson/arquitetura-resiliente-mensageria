using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using worker_publicador.Dominio;
using worker_publicador.Infraestrutura.Mensageria;
using worker_publicador.Infraestrutura.Persistencia;
using worker_publicador.Infraestrutura.Resiliencia;

namespace worker_publicador
{
    public class OutboxPublisherWorker : BackgroundService
    {
        private readonly ILogger<OutboxPublisherWorker> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IPublicadorMensagem _publicador;
        private readonly IAsyncPolicy _politicasDeResiliencia;

        public OutboxPublisherWorker(
            ILogger<OutboxPublisherWorker> logger,
            IServiceScopeFactory scopeFactory,
            IPublicadorMensagem publisher,
            ILoggerFactory loggerFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _publicador = publisher;

            _politicasDeResiliencia = PollyPoliticas.CriarPoliticasDeResiliencia(
                loggerFactory.CreateLogger("Polly.OutboxPublisher"));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("OutboxPublisherWorker iniciado.");

            var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    await ProcessarMensagensPendentesAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("OutboxPublisherWorker cancelado.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado no OutboxPublisherWorker.");
            }
        }

        private async Task ProcessarMensagensPendentesAsync(CancellationToken cancellationToken)
        {
            // Abre um escopo por iteração e resolve o DbContext (scoped) dentro dele.
            // Evita a "captive dependency" de injetar um DbContext scoped num
            // BackgroundService singleton (que viveria por toda a aplicação).
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<CreditoDbContext>();

            var pendentes = await dbContext.OutboxMessages
                .Where(o => o.Status == OutboxStatus.Pendente)
                .OrderBy(o => o.DataCriacao)
                .Take(50)
                .ToListAsync(cancellationToken);

            if (pendentes.Count == 0)
            {
                _logger.LogDebug("Nenhuma mensagem pendente na Outbox.");
                return;
            }

            _logger.LogInformation("Encontradas {Quantidade} mensagens pendentes na Outbox.", pendentes.Count);

            foreach (var outbox in pendentes)
            {
                try
                {
                    await _politicasDeResiliencia.ExecuteAsync(async ct =>
                    {
                        await _publicador.PublicarAsync(outbox, ct);
                    }, cancellationToken);

                    outbox.Status = OutboxStatus.Publicada;
                    outbox.DataAtualizacao = DateTime.UtcNow;

                    _logger.LogInformation(
                        "Mensagem Outbox publicada com sucesso. OutboxId={OutboxId}",
                        outbox.Id);
                }
                catch (Exception ex)
                {
                    // Falhou mesmo após as retentativas do Polly: registra a tentativa
                    // e mantém Pendente para reprocessar no próximo ciclo (at-least-once).
                    // A deduplicação no broker (MessageId=IdempotencyKey) evita que uma
                    // eventual republicação gere processamento duplicado a jusante.
                    outbox.Tentativas++;
                    outbox.DataAtualizacao = DateTime.UtcNow;

                    _logger.LogError(ex,
                        "Falha ao publicar mensagem Outbox. OutboxId={OutboxId}, Tentativas={Tentativas}. Será tentado novamente na próxima iteração.",
                        outbox.Id,
                        outbox.Tentativas);
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
