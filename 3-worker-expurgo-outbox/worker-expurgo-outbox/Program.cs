using Microsoft.EntityFrameworkCore;
using Serilog;
using worker_expurgo_outbox;
using worker_expurgo_outbox.Dominio;
using worker_expurgo_outbox.Infraestrutura.Persistencia;

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
    Log.Information("Iniciando Worker de Expurgo da Outbox...");

    var host = Host.CreateDefaultBuilder(args)
        .UseSerilog()
        .ConfigureServices((context, services) =>
        {
            var configuration = context.Configuration;

            services.Configure<OutboxCleanupSettings>(
                configuration.GetSection("OutboxCleanup"));

            services.AddDbContext<CreditoDbContext>(options =>
            {
                var connectionString = configuration.GetConnectionString("CreditoDb");
                options.UseSqlServer(connectionString);
            });

            services.AddHostedService<OutboxCleanupWorker>();
        })
        .Build();

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Worker de Expurgo encerrado devido a erro crítico.");
}
finally
{
    Log.CloseAndFlush();
}
