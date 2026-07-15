using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;

namespace worker_publicador.Infraestrutura.HealthChecks
{
    public class RabbitMqHealthCheck : IHealthCheck
    {
        private readonly IConnection _connection;

        public RabbitMqHealthCheck(IConnection connection)
        {
            _connection = connection;
        }

        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return Task.FromResult(_connection.IsOpen
                    ? HealthCheckResult.Healthy("Conexão com o RabbitMQ aberta.")
                    : HealthCheckResult.Unhealthy("Conexão com o RabbitMQ fechada."));
            }
            catch (Exception ex)
            {
                return Task.FromResult(
                    HealthCheckResult.Unhealthy("Falha ao verificar o RabbitMQ.", ex));
            }
        }
    }
}
