using Microsoft.EntityFrameworkCore;
using worker_expurgo_outbox.Dominio;

namespace worker_expurgo_outbox.Infraestrutura.Persistencia
{
    public class CreditoDbContext : DbContext
    {
        public CreditoDbContext(DbContextOptions<CreditoDbContext> options)
        : base(options)
        {
        }

        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(CreditoDbContext).Assembly);

            base.OnModelCreating(modelBuilder);
        }
    }
}
