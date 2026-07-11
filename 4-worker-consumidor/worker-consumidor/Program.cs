using Microsoft.EntityFrameworkCore;
using Serilog;
using worker_consumidor;
using worker_consumidor.Infraestrutura.Mensageria;
using worker_consumidor.Infraestrutura.Persistencia.Propostas;
using worker_consumidor.Infraestrutura.Persistencia.SolicitacoesRejeitadas;
using worker_consumidor.Services;

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
    Log.Information("Iniciando Worker Consumidor...");

    var host = Host.CreateDefaultBuilder(args)
        .UseSerilog()
        .ConfigureServices((context, services) =>
        {
            var configuration = context.Configuration;
            var connectionString = configuration.GetConnectionString("CreditoDb");

            services.AddDbContext<PropostasDbContext>(options =>
                options.UseSqlServer(connectionString));

            services.AddDbContext<SolicitacoesRejeitadasDbContext>(options =>
                options.UseSqlServer(connectionString));

            services.AddScoped<IProcessadorSolicitacaoCredito, ProcessadorSolicitacaoCredito>();

            services.AddSingleton<IConsumidorSolicitacaoCredito, RabbitMqConsumidorSolicitacaoCredito>();

            services.AddHostedService<ConsumidorSolicitacaoCreditoWorker>();
        })
        .Build();

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Worker Consumidor encerrado devido a erro crítico.");
}
finally
{
    Log.CloseAndFlush();
}
