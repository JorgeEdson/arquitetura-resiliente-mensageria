using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using Serilog;
using worker_publicador;
using worker_publicador.Infraestrutura.HealthChecks;
using worker_publicador.Infraestrutura.Mensageria;
using worker_publicador.Infraestrutura.Persistencia;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Iniciando Worker Publicador...");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    var configuration = builder.Configuration;
    var services = builder.Services;

    services.AddDbContext<CreditoDbContext>(options =>
    {
        var connectionString = configuration.GetConnectionString("CreditoDb");
        options.UseSqlServer(connectionString);
    });

    // Conexão única e compartilhada com o RabbitMQ, usada pelo publicador e
    // pelo health check de readiness. AutomaticRecovery mantém a conexão viva.
    services.AddSingleton<IConnection>(_ =>
    {
        var hostName = configuration["RabbitMq:HostName"]
            ?? throw new InvalidOperationException("RabbitMq:HostName não configurado.");

        var factory = new ConnectionFactory
        {
            HostName = hostName,
            Port = int.TryParse(configuration["RabbitMq:Port"], out var porta) ? porta : 5672,
            UserName = configuration["RabbitMq:UserName"] ?? "guest",
            Password = configuration["RabbitMq:Password"] ?? "guest",
            VirtualHost = configuration["RabbitMq:VirtualHost"] ?? "/",

            // Resiliência de conexão: reconecta e refaz topologia automaticamente.
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
        };

        // Resiliência de STARTUP: no docker compose o broker pode responder ao
        // healthcheck antes de o listener AMQP aceitar conexões. Em vez de deixar
        // o container morrer, tentamos conectar com backoff antes de desistir.
        var retry = Policy
            .Handle<BrokerUnreachableException>()
            .WaitAndRetry(
                retryCount: 10,
                sleepDurationProvider: tentativa =>
                    TimeSpan.FromSeconds(Math.Min(Math.Pow(2, tentativa), 30)), // 2s..30s
                onRetry: (ex, espera, tentativa, _) =>
                    Log.Warning(
                        "RabbitMQ indisponível ao iniciar. Tentativa={Tentativa}, aguardando {Espera}s...",
                        tentativa, espera.TotalSeconds));

        return retry.Execute(() => factory.CreateConnection());
    });

    services.AddSingleton<IPublicadorMensagem, RabbitMqMessagePublisher>();

    services.AddHostedService<OutboxPublisherWorker>();

    // Health checks.
    // - "self"     -> liveness: o processo está de pé (não checa dependências).
    // - "sqlserver"-> readiness: o banco responde.
    // - "rabbitmq" -> readiness: a conexão com o broker está aberta.
    services.AddHealthChecks()
        .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" })
        .AddDbContextCheck<CreditoDbContext>("sqlserver", tags: new[] { "ready" })
        .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: new[] { "ready" });

    var app = builder.Build();

    // Liveness: reinicia o container se o processo travar. Não depende de recursos externos.
    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("live")
    });

    // Readiness: só recebe tráfego quando SQL Server e RabbitMQ estão prontos.
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready")
    });

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Worker Publicador encerrado devido a erro crítico.");
}
finally
{
    Log.CloseAndFlush();
}
