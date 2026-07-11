using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using worker_consumidor.Services;

namespace worker_consumidor.Infraestrutura.Mensageria
{
    public interface IConsumidorSolicitacaoCredito
    {
        Task IniciarAsync(CancellationToken stoppingToken);
    }

    public class RabbitMqConsumidorSolicitacaoCredito : IConsumidorSolicitacaoCredito, IDisposable
    {
        private readonly ILogger<RabbitMqConsumidorSolicitacaoCredito> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly string _queueName;

        private IConnection? _connection;
        private IModel? _channel;

        public RabbitMqConsumidorSolicitacaoCredito(
            IConfiguration configuration,
            ILogger<RabbitMqConsumidorSolicitacaoCredito> logger,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;

            var hostName = configuration["RabbitMq:HostName"];
            _queueName = configuration["RabbitMq:QueueName"] ?? string.Empty;

            if (string.IsNullOrWhiteSpace(hostName))
                throw new InvalidOperationException("RabbitMq:HostName não configurado.");

            if (string.IsNullOrWhiteSpace(_queueName))
                throw new InvalidOperationException("RabbitMq:QueueName não configurada.");

            var factory = new ConnectionFactory
            {
                HostName = hostName,
                Port = int.TryParse(configuration["RabbitMq:Port"], out var porta) ? porta : 5672,
                UserName = configuration["RabbitMq:UserName"] ?? "guest",
                Password = configuration["RabbitMq:Password"] ?? "guest",
                VirtualHost = configuration["RabbitMq:VirtualHost"] ?? "/",

                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10),

                // Permite handlers de consumo assíncronos (AsyncEventingBasicConsumer).
                DispatchConsumersAsync = true
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            
            RabbitMqTopologia.Declarar(_channel, _queueName);

            // Processa uma mensagem por vez (backpressure).
            _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

            _logger.LogInformation(
                "RabbitMqConsumidorSolicitacaoCredito pronto. Host={HostName}, Queue={QueueName} (limite={Limite} tentativas antes da DLQ)",
                factory.HostName,
                _queueName,
                RabbitMqTopologia.LimiteEntregas);
        }

        public Task IniciarAsync(CancellationToken stoppingToken)
        {
            if (_channel is null)
                throw new InvalidOperationException("Canal RabbitMQ não inicializado.");

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.Received += OnMensagemRecebidaAsync;

            _channel.BasicConsume(
                queue: _queueName,
                autoAck: false,
                consumer: consumer);

            _logger.LogInformation("Consumo iniciado na fila {QueueName}. Aguardando mensagens...", _queueName);

            // Mantém o worker vivo até o cancelamento; o consumo roda em background.
            var espera = new TaskCompletionSource();
            stoppingToken.Register(() => espera.TrySetResult());
            return espera.Task;
        }

        private async Task OnMensagemRecebidaAsync(object sender, BasicDeliverEventArgs ea)
        {
            var payloadJson = Encoding.UTF8.GetString(ea.Body.ToArray());
            var propriedades = ea.BasicProperties?.Headers ?? new Dictionary<string, object>();
            var tentativa = ObterNumeroDaTentativa(ea);

            try
            {
                // Um escopo por mensagem: resolve o processor (scoped) e seus DbContexts.
                using var scope = _scopeFactory.CreateScope();
                var processador = scope.ServiceProvider.GetRequiredService<IProcessadorSolicitacaoCredito>();

                await processador.ProcessarAsync(payloadJson, propriedades, CancellationToken.None);

                _channel!.BasicAck(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                
                _logger.LogError(ex,
                    "Erro ao processar mensagem. DeliveryTag={DeliveryTag}, Tentativa={Tentativa}/{Limite}. NACK com requeue.",
                    ea.DeliveryTag,
                    tentativa,
                    RabbitMqTopologia.LimiteEntregas);

                _channel!.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
            }
        }
        
        private static long ObterNumeroDaTentativa(BasicDeliverEventArgs ea)
        {
            if (ea.BasicProperties?.Headers is { } headers &&
                headers.TryGetValue("x-delivery-count", out var valor) &&
                valor is not null &&
                long.TryParse(valor.ToString(), out var count))
            {
                return count + 1;
            }

            return 1;
        }

        public void Dispose()
        {
            if (_channel is { IsOpen: true })
                _channel.Close();

            _channel?.Dispose();

            if (_connection is { IsOpen: true })
                _connection.Close();

            _connection?.Dispose();
        }
    }
}
