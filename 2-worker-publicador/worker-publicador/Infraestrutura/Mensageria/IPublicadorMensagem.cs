using RabbitMQ.Client;
using System.Diagnostics;
using System.Text;
using worker_publicador.Dominio;

namespace worker_publicador.Infraestrutura.Mensageria
{
    public interface IPublicadorMensagem
    {
        Task PublicarAsync(OutboxMessage message, CancellationToken cancellationToken = default);
    }

    public class RabbitMqMessagePublisher : IPublicadorMensagem, IDisposable
    {
        private readonly ILogger<RabbitMqMessagePublisher> _logger;
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly string _queueName;

        public RabbitMqMessagePublisher(
              IConnection connection,
              IConfiguration configuration,
              ILogger<RabbitMqMessagePublisher> logger)
        {
            _logger = logger;

            // Conexão compartilhada (singleton), injetada via DI. A mesma conexão
            // é usada pelo RabbitMqHealthCheck para o probe de readiness.
            _connection = connection;
            _queueName = configuration["RabbitMq:QueueName"] ?? string.Empty;

            if (string.IsNullOrWhiteSpace(_queueName))
                throw new InvalidOperationException("RabbitMq:QueueName não configurada.");

            _logger.LogInformation(
                "Inicializando RabbitMqMessagePublisher. Queue={QueueName}",
                _queueName);

            _channel = _connection.CreateModel();

            // Publisher confirms: só consideramos publicado após o ACK do broker.
            _channel.ConfirmSelect();

            // Declara a topologia (fila quorum + DLX/DLQ). Idêntica à do consumidor.
            RabbitMqTopologia.Declarar(_channel, _queueName);

            _logger.LogInformation(
                "RabbitMqMessagePublisher pronto. Queue={QueueName} (quorum, DLQ={Dlq}, limite={Limite} entregas)",
                _queueName,
                RabbitMqTopologia.NomeDlq(_queueName),
                RabbitMqTopologia.LimiteEntregas);
        }

        public Task PublicarAsync(OutboxMessage message, CancellationToken cancellationToken = default)
        {
            if (message is null)
            {
                _logger.LogError("Tentativa de publicar mensagem nula na fila do RabbitMQ.");
                throw new ArgumentNullException(nameof(message));
            }

            var payloadLength = message.Payload?.Length ?? 0;

            _logger.LogInformation(
               "Preparando publicação no RabbitMQ. OutboxId={OutboxId}, TipoMensagem={TipoMensagem}, IdempotencyKey={IdempotencyKey}, PayloadLength={PayloadLength}, Queue={QueueName}",
               message.Id,
               message.TipoMensagem,
               message.IdempotencyKey,
               payloadLength,
               _queueName);

            var props = _channel.CreateBasicProperties();
            props.ContentType = "application/json";
            props.DeliveryMode = 2; // mensagem persistente

            // MessageId carrega a IdempotencyKey. O RabbitMQ não deduplica no
            // broker (diferente do Service Bus), então a idempotência efetiva
            // é responsabilidade do CONSUMIDOR, que usa esta chave para
            // descartar reprocessamentos (at-least-once + consumidor idempotente).
            props.MessageId = message.IdempotencyKey;

            props.Headers = new Dictionary<string, object>
            {
                ["TipoMensagem"] = message.TipoMensagem,
                ["IdempotencyKey"] = message.IdempotencyKey,
                ["OutboxId"] = message.Id.ToString()
            };

            var body = Encoding.UTF8.GetBytes(message.Payload!);

            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Publica no default exchange, roteando pela routingKey = nome da fila.
                _channel.BasicPublish(
                    exchange: string.Empty,
                    routingKey: _queueName,
                    basicProperties: props,
                    body: body);

                // Bloqueia até o ACK do broker; lança se não confirmar (Polly retenta).
                _channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(10));

                stopwatch.Stop();

                _logger.LogInformation(
                    "Mensagem publicada e confirmada no RabbitMQ. OutboxId={OutboxId}, Queue={QueueName}, ElapsedMs={ElapsedMs}",
                    message.Id,
                    _queueName,
                    stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                _logger.LogError(ex,
                    "Erro ao publicar mensagem no RabbitMQ. OutboxId={OutboxId}, Queue={QueueName}, ElapsedMs={ElapsedMs}",
                    message.Id,
                    _queueName,
                    stopwatch.ElapsedMilliseconds);

                throw;
            }

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _logger.LogInformation("Liberando recursos do RabbitMqMessagePublisher.");

            // Fecha apenas o canal. A IConnection é um singleton gerenciado pelo
            // container de DI, que se encarrega de descartá-la no shutdown.
            if (_channel is { IsOpen: true })
                _channel.Close();

            _channel?.Dispose();

            _logger.LogInformation("Recursos do RabbitMqMessagePublisher liberados com sucesso.");
        }
    }
}
