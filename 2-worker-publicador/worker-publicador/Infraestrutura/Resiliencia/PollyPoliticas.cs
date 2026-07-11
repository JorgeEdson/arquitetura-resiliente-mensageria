using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace worker_publicador.Infraestrutura.Resiliencia
{
    public static class PollyPoliticas
    {
        public static IAsyncPolicy CriarPoliticasDeResiliencia(ILogger logger)
        {
            // 1) Retry com backoff exponencial
            AsyncRetryPolicy retryPolitica = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: tentativa =>
                        TimeSpan.FromSeconds(Math.Pow(2, tentativa)), // 2s, 4s, 8s
                    onRetry: (ex, tempoEspera, tentativa, contexto) =>
                    {
                        logger.LogWarning(ex,
                            "Falha ao publicar mensagem no RabbitMQ. Tentativa={Tentativa}, Aguardando={Aguardar}s",
                            tentativa,
                            tempoEspera.TotalSeconds);
                    });

            // 2) Circuit Breaker
            AsyncCircuitBreakerPolicy circuitBreakerPolitica = Policy
                .Handle<Exception>()
                .CircuitBreakerAsync(
                    exceptionsAllowedBeforeBreaking: 5,                // 5 falhas seguidas
                    durationOfBreak: TimeSpan.FromSeconds(30),         // 30s com circuito aberto
                    onBreak: (ex, tempo, contexto) =>
                    {
                        logger.LogError(ex,
                            "Circuit Breaker ABERTO para publicação no RabbitMQ. Tempo={Tempo}s",
                            tempo.TotalSeconds);
                    },
                    onReset: contexto =>
                    {
                        logger.LogInformation(
                            "Circuit Breaker RESETADO para publicação no RabbitMQ. Comunicação restabelecida.");
                    },
                    onHalfOpen: () =>
                    {
                        logger.LogInformation(
                            "Circuit Breaker em estado HALF-OPEN. Testando nova tentativa de publicação no RabbitMQ.");
                    });

            // 3) Combinação: primeiro Circuit Breaker, depois Retry
            //
            // Ordem da chamada:
            //   policy = circuitBreaker(retry(action))
            //
            // Ou seja:
            //   - Retry tenta algumas vezes imediatamente.
            //   - Se mesmo assim falhar, a falha conta para o Circuit Breaker.
            //   - Quando o número de falhas atingir o limite, o Circuit Breaker abre.
            //
            IAsyncPolicy politicasComposta = Policy.WrapAsync(circuitBreakerPolitica, retryPolitica);

            return politicasComposta;
        }
    }
}
