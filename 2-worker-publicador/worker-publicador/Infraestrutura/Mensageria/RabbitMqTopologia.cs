using RabbitMQ.Client;

namespace worker_publicador.Infraestrutura.Mensageria
{
    /// <summary>
    /// Declaração da topologia RabbitMQ (topology as code).
    /// IMPORTANTE: publicador e consumidor declaram a MESMA topologia com os
    /// MESMOS argumentos. QueueDeclare é idempotente, mas argumentos divergentes
    /// entre serviços geram PRECONDITION_FAILED. Por isso este arquivo é
    /// duplicado, idêntico, nos dois projetos.
    /// </summary>
    public static class RabbitMqTopologia
    {
        // Número de entregas antes de mandar a mensagem para a DLQ.
        // Recurso de quorum queue (fila clássica NÃO suporta x-delivery-limit).
        public const int LimiteEntregas = 3;

        public static string NomeDlx(string queueName) => $"{queueName}.dlx";
        public static string NomeDlq(string queueName) => $"{queueName}.dlq";

        public static void Declarar(IModel channel, string queueName)
        {
            var dlx = NomeDlx(queueName);
            var dlq = NomeDlq(queueName);

            // 1) Dead-letter exchange (fanout) + fila de dead-letter, com binding.
            channel.ExchangeDeclare(dlx, ExchangeType.Fanout, durable: true, autoDelete: false);
            channel.QueueDeclare(dlq, durable: true, exclusive: false, autoDelete: false, arguments: null);
            channel.QueueBind(dlq, dlx, routingKey: string.Empty);

            // 2) Fila principal como QUORUM, com dead-letter e limite de entregas.
            var argumentos = new Dictionary<string, object>
            {
                ["x-queue-type"] = "quorum",
                ["x-dead-letter-exchange"] = dlx,
                ["x-delivery-limit"] = LimiteEntregas
            };

            channel.QueueDeclare(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: argumentos);
        }
    }
}
