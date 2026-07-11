using Microsoft.EntityFrameworkCore;
using Serilog;
using worker_publicador;
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

    var host = Host.CreateDefaultBuilder(args)
        .UseSerilog()
        .ConfigureServices((context, services) =>
        {
            var configuration = context.Configuration;
            
            services.AddDbContext<CreditoDbContext>(options =>
            {
                var connectionString = configuration.GetConnectionString("CreditoDb");
                options.UseSqlServer(connectionString);
            });
            
            services.AddSingleton<IPublicadorMensagem, RabbitMqMessagePublisher>();
            
            services.AddHostedService<OutboxPublisherWorker>();
        })
        .Build();

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Worker Publicador encerrado devido a erro cr�tico.");
}
finally
{
    Log.CloseAndFlush();
}
