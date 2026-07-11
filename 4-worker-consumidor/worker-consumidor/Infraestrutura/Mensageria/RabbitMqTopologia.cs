using RabbitMQ.Client;

namespace worker_consumidor.Infraestrutura.Mensageria
{
    
    public static class RabbitMqTopologia
    {   
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
